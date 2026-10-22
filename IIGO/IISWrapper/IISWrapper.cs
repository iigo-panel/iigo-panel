using Microsoft.Web.Administration;
using System.Diagnostics;

namespace IISManager.Services
{
    internal sealed class IISWrapper : IDisposable
    {
        ServerManager _manager;
        public IISWrapper()
        {
            // TODO: make path more dynamic
            _manager = new IisServerManager(@"C:\Windows\System32\inetsrv\config\applicationHost.config");
        }

        public Configuration GetSettings() => _manager.GetAdministrationConfiguration();

        public List<ApplicationPool> ListAppPools()
        {
            ApplicationPoolCollection appPoolCollection = _manager.ApplicationPools;

            return [.. appPoolCollection.OrderBy(x => x.Name)];
        }

        public dynamic? GetAppPool(string name)
        {
            ApplicationPool? app = _manager.ApplicationPools.SingleOrDefault(x => x.Name == name);
            if (app == null)
                return null;

            return new { app.Name, State = app.State.ToString(), app.AutoStart, app.ProcessModel.UserName, IdentityType = app.ProcessModel.IdentityType.ToString(), Sites = GetSitesOnAppPool(name) };
        }

        public List<dynamic> GetSitesOnAppPool(string name)
        {
            List<dynamic> sites = [];
            foreach (var site in _manager.Sites.Where(x => x.Applications.FirstOrDefault()?.ApplicationPoolName == name))
                sites.Add(new { site.Id, site.Name });

            return sites;
        }

        public List<dynamic> ListWebsites()
        {
            List<dynamic> sites = [];
            var siteCollection = _manager.Sites.OrderBy(x => x.Name);
            foreach (Site site in siteCollection)
                sites.Add(new { site.Id, site.Name, State = site.State.ToString(), Bindings = site.Bindings.Select(b => b.BindingInformation).ToList(), AppPool = site.Applications[0].ApplicationPoolName });

            return sites;
        }

        public List<Site> ListSites()
        {
            _manager.Dispose();
            _manager = new IisServerManager(@"C:\Windows\System32\inetsrv\config\applicationHost.config");
            return [.. _manager.Sites.OrderBy(x => x.Name)];
        }

        public List<ApplicationPool> ListPools()
        {
            return [.. _manager.ApplicationPools.OrderBy(x => x.Name)];
        }

        public dynamic GetWebsite(long id)
        {
            var site = _manager.Sites.Where(x => x.Id == id).SingleOrDefault() ?? throw new NullReferenceException("Site not found");
            return new { site.Id, site.Name, Bindings = site.Bindings.Select(b => b.BindingInformation).ToList(), State = site.State.ToString(), AppPool = site.Applications[0].ApplicationPoolName };
        }

        public Site? GetSite(long id)
        {
            return _manager.Sites.Where(x => x.Id == id).SingleOrDefault();
        }

        public void StopSite(long id)
        {
            var site = _manager.Sites.Where(x => x.Id == id).SingleOrDefault();
            if (site != null && site.State == ObjectState.Started)
                site.Stop();
        }

        public void StartSite(long id)
        {
            var site = _manager.Sites.Where(x => x.Id == id).SingleOrDefault();
            if (site != null && site.State == ObjectState.Stopped)
                site.Start();
        }

        /// <summary>
        /// Create an application pool
        /// </summary>
        /// <param name="appPoolName">Name of the App Pool</param>
        /// <param name="userName">Name of the user it will run as</param>
        /// <param name="password">Password of the user</param>
        /// <param name="memoryLimit">Private Memory Limit (in KB) - Default: 204800</param>
        public void CreateAppPool(string appPoolName, string userName, string password, long memoryLimit = 204800, bool alwaysRunning = false)
        {
            ApplicationPool pool = _manager.ApplicationPools.Add(appPoolName);
            pool.Enable32BitAppOnWin64 = true;
            pool.Recycling.PeriodicRestart.PrivateMemory = memoryLimit;
            pool.Failure.RapidFailProtectionMaxCrashes = 10;
            pool.Failure.RapidFailProtectionInterval = TimeSpan.FromMinutes(5);
            pool.ProcessModel.IdentityType = ProcessModelIdentityType.SpecificUser;
            pool.ProcessModel.UserName = userName;
            pool.ProcessModel.Password = password;
            pool.ManagedPipelineMode = ManagedPipelineMode.Integrated;
            if (alwaysRunning)
                pool.StartMode = StartMode.AlwaysRunning;

            _manager.CommitChanges();
        }

        public void CreateAppPool(string appPoolName, bool alwaysRunning = false)
        {
            ApplicationPool pool = _manager.ApplicationPools.Add(appPoolName);
            pool.Enable32BitAppOnWin64 = true;
            pool.Failure.RapidFailProtectionMaxCrashes = 10;
            pool.ProcessModel.IdentityType = ProcessModelIdentityType.NetworkService;
            pool.ManagedPipelineMode = ManagedPipelineMode.Integrated;
            if (alwaysRunning)
                pool.StartMode = StartMode.AlwaysRunning;

            _manager.CommitChanges();
        }

        public void RecycleAppPool(string appPoolName)
        {
            var pool = _manager.ApplicationPools.Where(x => x.Name == appPoolName).SingleOrDefault() ?? throw new NullReferenceException("Application pool not found");
            pool.Recycle();
            pool.Stop();
            foreach (WorkerProcess p in pool.WorkerProcesses)
                Process.GetProcessById(p.ProcessId).Kill();

            while (_manager.ApplicationPools.Where(x => x.Name == appPoolName).SingleOrDefault()?.State != ObjectState.Stopped)
                Thread.Sleep(2_000);

            pool.Start();
        }

        public void DeleteSite(long id)
        {
            var site = _manager.Sites.Where(x => x.Id == id).SingleOrDefault();
            _manager.Sites.Remove(site);
            _manager.CommitChanges();
        }

        public void DeleteAppPool(string name)
        {
            var pool = _manager.ApplicationPools.Where(x => x.Name == name).SingleOrDefault();
            _manager.ApplicationPools.Remove(pool);
            _manager.CommitChanges();
        }

        /// <summary>
        /// Alter the application pool limits. Note: This needs to be called on a new instance of IISManager. It won't properly
        /// adjust the CPU limits if called in the same scope as CreateAppPool.
        /// </summary>
        /// <param name="appPoolName">Name of the App Pool</param>
        /// <param name="cpuLimit">CPU % Limit, in 1/1000ths of a percent (1000 == 1%)</param>
        public void UpdateAppPoolLimits(string appPoolName, long cpuLimit)
        {
            Configuration config = _manager.GetApplicationHostConfiguration();
            Microsoft.Web.Administration.ConfigurationSection applicationPoolsSection = config.GetSection("system.applicationHost/applicationPools");
            ConfigurationElementCollection applicationPoolsCollection = applicationPoolsSection.GetCollection();
            ConfigurationElement addElement = FindElement(applicationPoolsCollection, "add", "name", appPoolName) ?? throw new InvalidOperationException("Element not found!");
			ConfigurationElement cpuElement = addElement.GetChildElement("cpu");
            cpuElement["action"] = @"Throttle";
            cpuElement["limit"] = cpuLimit;

            _manager.CommitChanges();
        }

        /// <summary>
        /// Creates a website using the specified domain and path and attempts to use an application pool by the same name
        /// </summary>
        /// <param name="domain"></param>
        /// <param name="path"></param>
        //public void CreateWebsite(string domain, string path, string serverPrefix = "s1", bool addBindings = true, bool isSdw = true)
        //{
        //    var pools = ListAppPools();
        //    if (!pools.Any(x => x.Name == domain))
        //        throw new AppPoolNotFoundException(domain);

        //    Site site = _manager.Sites.Add(domain, path, 80);
        //    site.ServerAutoStart = true;
        //    site.Bindings[0].BindingInformation = $"*:80:{domain}";
        //    if (addBindings)
        //    {
        //        site.Bindings.Add($"*:80:www.{domain}", "http");
        //        site.Bindings.Add($"*:80:www.{domain}.{serverPrefix}.{(isSdw ? "sdw" : "mws")}.dev", "http");
        //    }
        //    site.Applications[0].ApplicationPoolName = domain;

        //    _manager.CommitChanges();
        //}

        /// <summary>
        /// Get Site ID based on name. Returns -1 if not found
        /// </summary>
        /// <param name="siteName">Website name in IIS</param>
        /// <returns></returns>
        public long GetSiteId(string siteName)
        {
            var site = _manager.Sites[siteName];
            if (site == null)
                return -1;

            return site.Id;
        }

        /// <summary>
        /// Creates a website using the specified domain and path and creates an application pool if it doesn't already exist
        /// </summary>
        /// <param name="siteName"></param>
        /// <param name="appPoolName"></param>
        /// <param name="physicalPath"></param>
        /// <param name="bindingInformation"></param>
        /// <param name="protocol"></param>
        public void CreateWebsite(string siteName, string appPoolName, string physicalPath, string bindingInformation, string protocol = "http")
        {
            var pools = ListAppPools();
            if (!pools.Any(x => x.Name == appPoolName))
                CreateAppPool(appPoolName);

            Site site = _manager.Sites.Add(siteName, physicalPath, 80);
            site.ServerAutoStart = true;
            site.Bindings[0].BindingInformation = bindingInformation;
            site.Bindings[0].Protocol = protocol;
            site.Applications[0].ApplicationPoolName = appPoolName;

            _manager.CommitChanges();
        }

        public void DeleteWebsite(long siteId)
        {
            _manager.Sites.Remove(_manager.Sites.SingleOrDefault(x => x.Id == siteId) ?? throw new NullReferenceException("Site not found"));
            _manager.CommitChanges();
        }

        /// <summary>
        /// Updates the physical path and pass-through credentials of the site's root application
        /// </summary>
        /// <param name="userName">Specific user to connect as. Null or empty uses pass-through authentication</param>
        /// <param name="password">Password for the specific user. Null or empty keeps the existing password</param>
        public void UpdateSitePath(long siteId, string physicalPath, string? userName, string? password)
        {
            EditSite(siteId, site =>
            {
                var app = site.Applications.FirstOrDefault(x => x.Path == "/") ?? throw new InvalidOperationException("Root application not found");
                SetRootDirectory(app, physicalPath, userName, password);
            });
        }

        public void AddApplication(long siteId, string path, string physicalPath, string appPoolName, string? userName, string? password, bool preloadEnabled)
        {
            path = NormalizeApplicationPath(path);
            EditSite(siteId, site =>
            {
                if (site.Applications.Any(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException($"An application already exists at {path}");
                if (!_manager.ApplicationPools.Any(x => x.Name == appPoolName))
                    throw new AppPoolNotFoundException(appPoolName);

                Application app = site.Applications.Add(path, physicalPath);
                app.ApplicationPoolName = appPoolName;
                app.SetAttributeValue("preloadEnabled", preloadEnabled);
                SetRootDirectory(app, physicalPath, userName, password);
            });
        }

        public void UpdateApplication(long siteId, string path, string physicalPath, string appPoolName, string? userName, string? password, bool preloadEnabled)
        {
            EditSite(siteId, site =>
            {
                var app = FindApplication(site, path);
                if (!_manager.ApplicationPools.Any(x => x.Name == appPoolName))
                    throw new AppPoolNotFoundException(appPoolName);

                app.ApplicationPoolName = appPoolName;
                app.SetAttributeValue("preloadEnabled", preloadEnabled);
                SetRootDirectory(app, physicalPath, userName, password);
            });
        }

        public void DeleteApplication(long siteId, string path)
        {
            if (path == "/")
                throw new InvalidOperationException("The root application can't be deleted");

            EditSite(siteId, site => site.Applications.Remove(FindApplication(site, path)));
        }

        /// <summary>
        /// Adds a binding to a site. For https bindings the certificate is also registered with HTTP.sys
        /// </summary>
        /// <param name="bindingInformation">IP:Port:Host</param>
        /// <param name="certificateHash">Thumbprint of the certificate (https only)</param>
        /// <param name="certificateStore">Store the certificate lives in, e.g. My or WebHosting (https only)</param>
        /// <param name="requireSni">Require Server Name Indication (https only)</param>
        public void AddBinding(long siteId, string protocol, string bindingInformation, string? certificateHash, string? certificateStore, bool requireSni)
        {
            EditSite(siteId, site =>
            {
                if (site.Bindings.Any(x => x.Protocol == protocol && string.Equals(x.BindingInformation, bindingInformation, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException($"The binding {protocol} {bindingInformation} already exists");

                if (protocol == "https")
                {
                    byte[] hash = Convert.FromHexString(certificateHash ?? throw new ArgumentException("A certificate is required for https bindings"));
                    var binding = site.Bindings.Add(bindingInformation, hash, certificateStore, requireSni ? SslFlags.Sni : SslFlags.None);
                    if (binding.EndPoint == null)
                        throw new ArgumentException($"Invalid binding information: {bindingInformation}");
                    SslCertificateBinding.Bind(binding, hash, certificateStore!);
                }
                else
                {
                    site.Bindings.Add(bindingInformation, protocol);
                }
            });
        }

        /// <summary>
        /// Replaces an existing binding, identified by its current protocol and binding information
        /// </summary>
        public void UpdateBinding(long siteId, string originalProtocol, string originalBindingInformation, string protocol, string bindingInformation, string? certificateHash, string? certificateStore, bool requireSni)
        {
            // The old HTTP.sys mapping is removed once the new binding is committed
            bool hadMapping = false;
            bool oldSni = false;
            string oldHost = "";
            System.Net.IPEndPoint? oldEndPoint = null;

            EditSite(siteId, site =>
            {
                var binding = FindBinding(site, originalProtocol, originalBindingInformation);
                if ((protocol != originalProtocol || !string.Equals(bindingInformation, originalBindingInformation, StringComparison.OrdinalIgnoreCase))
                    && site.Bindings.Any(x => x.Protocol == protocol && string.Equals(x.BindingInformation, bindingInformation, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException($"The binding {protocol} {bindingInformation} already exists");

                hadMapping = HasCertificateMapping(binding);
                oldSni = binding.SslFlags.HasFlag(SslFlags.Sni);
                oldHost = binding.Host;
                oldEndPoint = binding.EndPoint;

                // Keep the central certificate store flag, it isn't editable here
                var centralStore = binding.SslFlags & SslFlags.CentralCertStore;
                binding.Protocol = protocol;
                binding.BindingInformation = bindingInformation;
                if (protocol == "https")
                {
                    binding.SslFlags = (requireSni ? SslFlags.Sni : SslFlags.None) | centralStore;
                    if (binding.EndPoint == null)
                        throw new ArgumentException($"Invalid binding information: {bindingInformation}");
                    if (centralStore == SslFlags.None)
                    {
                        byte[] hash = Convert.FromHexString(certificateHash ?? throw new ArgumentException("A certificate is required for https bindings"));
                        binding.CertificateHash = hash;
                        binding.CertificateStoreName = certificateStore;
                        SslCertificateBinding.Bind(binding, hash, certificateStore!);
                    }
                }
                else
                {
                    binding.SslFlags = SslFlags.None;
                }
            });

            if (hadMapping)
                UnbindIfUnused(oldSni, oldHost, oldEndPoint!);
        }

        public void DeleteBinding(long siteId, string protocol, string bindingInformation)
        {
            bool hadMapping = false;
            bool sni = false;
            string host = "";
            System.Net.IPEndPoint? endPoint = null;

            EditSite(siteId, site =>
            {
                var binding = FindBinding(site, protocol, bindingInformation);
                if (site.Bindings.Count == 1)
                    throw new InvalidOperationException("A site needs at least one binding");

                hadMapping = HasCertificateMapping(binding);
                sni = binding.SslFlags.HasFlag(SslFlags.Sni);
                host = binding.Host;
                endPoint = binding.EndPoint;

                site.Bindings.Remove(binding);
            });

            if (hadMapping)
                UnbindIfUnused(sni, host, endPoint!);
        }

        /// <summary>
        /// Loads a fresh copy of the config, applies the change to the site and commits it. If anything fails
        /// the config is reloaded so the uncommitted change doesn't show up in later reads.
        /// </summary>
        private void EditSite(long siteId, Action<Site> change)
        {
            Reload();
            try
            {
                change(FindSite(siteId));
                _manager.CommitChanges();
            }
            catch
            {
                Reload();
                throw;
            }
        }

        private static bool HasCertificateMapping(Binding binding) =>
            binding.Protocol == "https" && binding.EndPoint != null && !binding.SslFlags.HasFlag(SslFlags.CentralCertStore);

        /// <summary>
        /// Removes an HTTP.sys certificate mapping unless an https binding on the server still uses it
        /// </summary>
        private void UnbindIfUnused(bool sni, string host, System.Net.IPEndPoint endPoint)
        {
            string key = SslCertificateBinding.MappingKey(sni, host, endPoint);
            bool inUse = _manager.Sites.SelectMany(s => s.Bindings)
                .Where(HasCertificateMapping)
                .Any(b => SslCertificateBinding.MappingKey(b) == key);

            if (!inUse)
                SslCertificateBinding.Unbind(sni, host, endPoint);
        }

        private void Reload()
        {
            _manager.Dispose();
            _manager = new IisServerManager(@"C:\Windows\System32\inetsrv\config\applicationHost.config");
        }

        private Site FindSite(long siteId) => _manager.Sites.SingleOrDefault(x => x.Id == siteId) ?? throw new NullReferenceException("Site not found");

        private static Application FindApplication(Site site, string path) =>
            site.Applications.SingleOrDefault(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException($"Application {path} not found");

        private static Binding FindBinding(Site site, string protocol, string bindingInformation) =>
            site.Bindings.FirstOrDefault(x => x.Protocol == protocol && string.Equals(x.BindingInformation, bindingInformation, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Binding {protocol} {bindingInformation} not found");

        /// <summary>
        /// Turns an alias like "sales" or "/sales/" into an application path like "/sales"
        /// </summary>
        private static string NormalizeApplicationPath(string path)
        {
            path = "/" + path.Trim().Replace('\\', '/').Trim('/');
            if (path == "/")
                throw new ArgumentException("An alias is required");
            if (path.IndexOfAny(ApplicationCollection.InvalidApplicationPathCharacters()) >= 0)
                throw new ArgumentException($"The alias contains invalid characters: {string.Join(" ", ApplicationCollection.InvalidApplicationPathCharacters())}");

            return path;
        }

        private static void SetRootDirectory(Application app, string physicalPath, string? userName, string? password)
        {
            var dir = app.VirtualDirectories.FirstOrDefault(x => x.Path == "/") ?? throw new InvalidOperationException("Root virtual directory not found");
            dir.PhysicalPath = physicalPath;

            if (string.IsNullOrWhiteSpace(userName))
            {
                dir.GetAttribute("userName").Delete();
                dir.GetAttribute("password").Delete();
                return;
            }

            bool sameUser = string.Equals(dir.UserName, userName, StringComparison.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(password) && !sameUser)
                throw new ArgumentException("A password is required when connecting as a specific user");

            dir.UserName = userName;
            if (!string.IsNullOrEmpty(password))
                dir.Password = password;
        }

        public Dictionary<string, string> GetIPRules()
        {
            var ips = new Dictionary<string, string>();
            Configuration config = _manager.GetApplicationHostConfiguration();
            var ipSecuritySection = config.GetSection("system.webServer/security/ipSecurity");
            ConfigurationElementCollection ipSecurityCollection = ipSecuritySection.GetCollection();
            foreach (ConfigurationElement element in ipSecurityCollection)
            {
                // Indexer, not Add: two rules can share an address with different subnet masks
                ips[element["ipAddress"].ToString()!] = element["allowed"].ToString()!;
            }

            return ips;
        }

        public void DeleteIPRule(string ipaddress)
        {
            Configuration config = _manager.GetApplicationHostConfiguration();
            var ipSecuritySection = config.GetSection("system.webServer/security/ipSecurity");
            ConfigurationElementCollection ipSecurityCollection = ipSecuritySection.GetCollection();
            ConfigurationElement addElement = FindElement(ipSecurityCollection, "add", "ipAddress", ipaddress) ?? throw new InvalidOperationException("Element not found!");
            ipSecurityCollection.Remove(addElement);
            _manager.CommitChanges();
        }

        public void AddIPRule(string ipaddress, bool denyRule)
        {
            var ips = GetIPRules();
            if (ips.ContainsKey(ipaddress))
                return;
            Configuration config = _manager.GetApplicationHostConfiguration();
            var ipSecuritySection = config.GetSection("system.webServer/security/ipSecurity");
            ConfigurationElementCollection ipSecurityCollection = ipSecuritySection.GetCollection();
            ConfigurationElement addElement = ipSecurityCollection.CreateElement("add");
            addElement["ipAddress"] = ipaddress;
            addElement["allowed"] = !denyRule;
            ipSecurityCollection.Add(addElement);
            _manager.CommitChanges();
        }

        public void Dispose()
        {
            _manager.Dispose();
        }

        private static ConfigurationElement? FindElement(ConfigurationElementCollection collection, string elementTagName, params string[] keyValues)
        {
            foreach (ConfigurationElement element in collection)
            {
                if (String.Equals(element.ElementTagName, elementTagName, StringComparison.OrdinalIgnoreCase))
                {
                    bool matches = true;
                    for (int i = 0; i < keyValues.Length; i += 2)
                    {
                        object o = element.GetAttributeValue(keyValues[i]);
                        string? value = null;
                        if (o != null)
                        {
                            value = o.ToString();
                        }
                        if (!String.Equals(value, keyValues[i + 1], StringComparison.OrdinalIgnoreCase))
                        {
                            matches = false;
                            break;
                        }
                    }
                    if (matches)
                    {
                        return element;
                    }
                }
            }
            return null;
        }
    }
}

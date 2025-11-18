using Microsoft.Web.Administration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Linq;

namespace IISManager.Services
{
    internal sealed class IISWrapper : IDisposable
    {
        readonly ServerManager _manager;
        public IISWrapper()
        {
            // TODO: make path more dynamic
            _manager = new IisServerManager(@"C:\Windows\System32\inetsrv\config\applicationHost.config");
        }

        public Configuration GetSettings() => _manager.GetAdministrationConfiguration();

        public List<ApplicationPool> ListAppPools()
        {
            ApplicationPoolCollection appPoolCollection = _manager.ApplicationPools;

            return appPoolCollection.ToList();
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
        public void CreateWebsite(string domain, string path, string serverPrefix = "s1", bool addBindings = true, bool isSdw = true)
        {
            var pools = ListAppPools();
            if (!pools.Any(x => x.Name == domain))
                throw new AppPoolNotFoundException(domain);

            Site site = _manager.Sites.Add(domain, path, 80);
            site.ServerAutoStart = true;
            site.Bindings[0].BindingInformation = $"*:80:{domain}";
            if (addBindings)
            {
                site.Bindings.Add($"*:80:www.{domain}", "http");
                site.Bindings.Add($"*:80:www.{domain}.{serverPrefix}.{(isSdw ? "sdw" : "mws")}.dev", "http");
            }
            site.Applications[0].ApplicationPoolName = domain;

            _manager.CommitChanges();
        }

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
        /// <param name="domain"></param>
        /// <param name="path"></param>
        public void CreateWebsite(string domain, string path)
        {
            var pools = ListAppPools();
            if (!pools.Any(x => x.Name == domain))
                CreateAppPool(domain);

            Site site = _manager.Sites.Add(domain, path, 80);
            site.ServerAutoStart = true;
            site.Bindings[0].BindingInformation = $"*:80:{domain}";
            site.Applications[0].ApplicationPoolName = domain;

            _manager.CommitChanges();
        }

        public Dictionary<string, string> GetIPRules()
        {
            var ips = new Dictionary<string, string>();
            Configuration config = _manager.GetApplicationHostConfiguration();
            var ipSecuritySection = config.GetSection("system.webServer/security/ipSecurity");
            ConfigurationElementCollection ipSecurityCollection = ipSecuritySection.GetCollection();
            foreach (ConfigurationElement element in ipSecurityCollection)
            {
                ips.Add(element["ipAddress"].ToString()!, element["allowed"].ToString()!);
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

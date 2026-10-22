using IISManager.Services;
using Microsoft.Web.Administration;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace IIGO.Services
{
    public class IISService
    {
        private readonly IISWrapper _iisWrapper = new IISWrapper();

        public void AddWebsite(string siteName, string appPoolName, string physicalPath, string bindingInformation, string protocol)
        {
            _iisWrapper.CreateWebsite(siteName, appPoolName, physicalPath, bindingInformation, protocol);
        }

        public void DeleteWebsite(long siteId)
        {
            _iisWrapper.DeleteWebsite(siteId);
        }

        public void UpdateWebsite(long siteId, string siteName, string appPoolName, string physicalPath, string bindingInformation)
        {
            //_iisWrapper.UpdateWebsite(siteId, siteName, appPoolName, physicalPath, bindingInformation);
        }

        public void UpdateSitePath(long siteId, string physicalPath, string? userName, string? password)
        {
            _iisWrapper.UpdateSitePath(siteId, physicalPath, userName, password);
        }

        public void AddApplication(long siteId, string alias, string physicalPath, string appPoolName, string? userName, string? password, bool preloadEnabled)
        {
            _iisWrapper.AddApplication(siteId, alias, physicalPath, appPoolName, userName, password, preloadEnabled);
        }

        public void UpdateApplication(long siteId, string path, string physicalPath, string appPoolName, string? userName, string? password, bool preloadEnabled)
        {
            _iisWrapper.UpdateApplication(siteId, path, physicalPath, appPoolName, userName, password, preloadEnabled);
        }

        public void DeleteApplication(long siteId, string path)
        {
            _iisWrapper.DeleteApplication(siteId, path);
        }

        public void AddBinding(long siteId, string protocol, string bindingInformation, string? certificateHash, string? certificateStore, bool requireSni)
        {
            _iisWrapper.AddBinding(siteId, protocol, bindingInformation, certificateHash, certificateStore, requireSni);
        }

        public void UpdateBinding(long siteId, string originalProtocol, string originalBindingInformation, string protocol, string bindingInformation, string? certificateHash, string? certificateStore, bool requireSni)
        {
            _iisWrapper.UpdateBinding(siteId, originalProtocol, originalBindingInformation, protocol, bindingInformation, certificateHash, certificateStore, requireSni);
        }

        public void DeleteBinding(long siteId, string protocol, string bindingInformation)
        {
            _iisWrapper.DeleteBinding(siteId, protocol, bindingInformation);
        }

        /// <summary>
        /// Certificates with a private key in the stores IIS can bind to
        /// </summary>
        public List<CertificateInfo> GetCertificates()
        {
            var certificates = new List<CertificateInfo>();
            foreach (var storeName in new[] { "My", "WebHosting" })
            {
                using var store = new X509Store(storeName, StoreLocation.LocalMachine);
                try
                {
                    store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                }
                catch (CryptographicException)
                {
                    // Store doesn't exist on this server
                    continue;
                }

                foreach (var cert in store.Certificates)
                {
                    using (cert)
                    {
                        if (cert.HasPrivateKey)
                            certificates.Add(new CertificateInfo(storeName, cert.Thumbprint, cert.FriendlyName, cert.GetNameInfo(X509NameType.DnsName, false), cert.NotAfter));
                    }
                }
            }

            return [.. certificates.OrderBy(x => x.Name).ThenByDescending(x => x.NotAfter)];
        }

        public void AddAppPool(string appPoolName)
        {
            _iisWrapper.CreateAppPool(appPoolName);
        }

        public void DeleteAppPool(string appPoolName)
        {
            _iisWrapper.DeleteAppPool(appPoolName);
        }

        public List<ApplicationPool> GetAppPools()
        {
            try
            {
                    return _iisWrapper.ListAppPools();
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry(Constants.EventLogSource, $"Error loading application pools\n\n{ex.Demystify()}", EventLogEntryType.Error, 1005);
                return [];
            }
        }
        public ApplicationPool? GetAppPool(string name)
        {
            try
            {
                return _iisWrapper.ListAppPools().SingleOrDefault(x => x.Name == name);
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry(Constants.EventLogSource, $"Error loading application pools\n\n{ex.Demystify()}", EventLogEntryType.Error, 1005);
                return null;
            }
        }
        public List<Site> GetSites()
        {
            try
            {
                return _iisWrapper.ListSites();
            }
            catch
            {
                return [];
            }
        }

        public Site? GetSite(long id)
        {
            try
            {
                return _iisWrapper.GetSite(id);
            }
            catch
            {
                return null;
            }
        }

        public void Recycle(string name)
        {
            var p = GetAppPools().SingleOrDefault(pool => pool.Name == name);
            p?.Recycle();
        }

        public void StartPool(string name)
        {
            var p = GetAppPools().SingleOrDefault(pool => pool.Name == name);
            if (p != null && p.State == ObjectState.Stopped)
                p?.Start();
        }

        public void StopPool(string name)
        {
            var p = GetAppPools().SingleOrDefault(pool => pool.Name == name);
            if (p != null && p.State == ObjectState.Started)
                p?.Stop();
        }

        public void RestartSite(long siteId)
        {
            var site = GetSite(siteId);
            if (site != null)
            {
                site.Stop();
                site.Start();
            }
        }

        public void StopSite(long siteId)
        {
            var site = GetSite(siteId);
            site?.Stop();
        }

        public void StartSite(long siteId)
        {
            var site = GetSite(siteId);
            site?.Start();
        }

        public Dictionary<string, string> GetIPRules() => _iisWrapper.GetIPRules();

        public void DeleteIPRule(string ipaddress)=> _iisWrapper.DeleteIPRule(ipaddress);

        public void AddIPRule(string ipaddress, bool denyRule)=> _iisWrapper.AddIPRule(ipaddress, denyRule);

        public Dictionary<string, int> GetApplicationCountsByPool()
        {
            var counts = new Dictionary<string, int>();
            try
            {
                var sites = GetSites();
                foreach (var site in sites)
                {
                    foreach (Application app in site.Applications)
                    {
                        var poolName = app.ApplicationPoolName;
                        if (counts.ContainsKey(poolName))
                            counts[poolName]++;
                        else
                            counts[poolName] = 1;
                    }
                }
            }
            catch { }
            return counts;
        }

        public List<(string SiteName, string VirtualPath, string PhysicalPath)> GetApplicationsByPool(string appPoolName)
        {
            var apps = new List<(string SiteName, string VirtualPath, string PhysicalPath)>();
            try
            {
                var sites = GetSites();
                foreach (var site in sites)
                {
                    foreach (Application app in site.Applications)
                    {
                        if (app.ApplicationPoolName == appPoolName)
                        {
                            var physicalPath = app.VirtualDirectories.Count > 0
                                ? app.VirtualDirectories[0].PhysicalPath
                                : "";
                            apps.Add((site.Name, app.Path, physicalPath));
                        }
                    }
                }
            }
            catch { }
            return apps;
        }
    }

    public record CertificateInfo(string StoreName, string Thumbprint, string FriendlyName, string Name, DateTime NotAfter)
    {
        public string DisplayName => $"{(string.IsNullOrEmpty(FriendlyName) ? Name : $"{FriendlyName} ({Name})")} - expires {NotAfter:d}";
    }
}

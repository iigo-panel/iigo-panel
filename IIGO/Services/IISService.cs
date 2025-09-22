using IISManager.Services;
using Microsoft.Web.Administration;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace IIGO.Services
{
    internal class IISService
    {
        private readonly IISWrapper _iisWrapper = new IISWrapper();

        public void AddWebsite(string siteName, string appPoolName, string physicalPath, string bindingInformation)
        {
            _iisWrapper.CreateWebsite(siteName, appPoolName);
        }

        public void DeleteWebsite(long siteId)
        {
            //_iisWrapper.DeleteWebsite(siteId);
        }

        public void UpdateWebsite(long siteId, string siteName, string appPoolName, string physicalPath, string bindingInformation)
        {
            //_iisWrapper.UpdateWebsite(siteId, siteName, appPoolName, physicalPath, bindingInformation);
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

        public Dictionary<string, string> GetIPRules()
        {
            var manager = new IisServerManager();
            var ips = new Dictionary<string, string>();
            Configuration config = manager.GetApplicationHostConfiguration();
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
            var manager = new IisServerManager();
            Configuration config = manager.GetApplicationHostConfiguration();
            var ipSecuritySection = config.GetSection("system.webServer/security/ipSecurity");
            ConfigurationElementCollection ipSecurityCollection = ipSecuritySection.GetCollection();
            ConfigurationElement addElement = FindElement(ipSecurityCollection, "add", "ipAddress", ipaddress) ?? throw new InvalidOperationException("Element not found!");
            ipSecurityCollection.Remove(addElement);
            manager.CommitChanges();
        }

        public void AddIPRule(string ipaddress, bool denyRule)
        {
            var manager = new IisServerManager();
            var ips = GetIPRules();
            if (ips.ContainsKey(ipaddress))
                return;
            Configuration config = manager.GetApplicationHostConfiguration();
            var ipSecuritySection = config.GetSection("system.webServer/security/ipSecurity");
            ConfigurationElementCollection ipSecurityCollection = ipSecuritySection.GetCollection();
            ConfigurationElement addElement = ipSecurityCollection.CreateElement("add");
            addElement["ipAddress"] = ipaddress;
            addElement["allowed"] = !denyRule;
            ipSecurityCollection.Add(addElement);
            manager.CommitChanges();
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
                            value = o.ToString()!;
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

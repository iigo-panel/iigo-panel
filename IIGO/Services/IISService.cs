using IISManager.Services;
using Microsoft.Web.Administration;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace IIGO.Services
{
    internal static class IISService
    {
        public static List<ApplicationPool> GetAppPools()
        {
            try
            {
                    return new IISWrapper().ListAppPools();
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry(Constants.EventLogSource, $"Error loading application pools\n\n{ex.Demystify()}", EventLogEntryType.Error, 1005);
                return [];
            }
        }
        public static ApplicationPool? GetAppPool(string name)
        {
            try
            {
                var manager = new IisServerManager();
                    ApplicationPoolCollection applicationPoolCollection = manager.ApplicationPools;
                    return applicationPoolCollection.SingleOrDefault(x => x.Name == name);
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry(Constants.EventLogSource, $"Error loading application pools\n\n{ex.Demystify()}", EventLogEntryType.Error, 1005);
                return null;
            }
        }
        public static List<Site> GetSites()
        {
            try
            {
                return new IISWrapper().ListSites();
            }
            catch
            {
                return [];
            }
        }

        public static Site? GetSite(long id)
        {
            try
            {
                return new IISWrapper().GetSite(id);
            }
            catch
            {
                return null;
            }
        }

        public static void Recycle(string name)
        {
            var pools = GetAppPools();
            var p = pools.SingleOrDefault(pool => pool.Name == name);
            p?.Recycle();
        }

        public static void StartPool(string name)
        {
            var pools = GetAppPools();
            var p = pools.SingleOrDefault(pool => pool.Name == name);
            if (p != null && p.State == ObjectState.Stopped)
                p?.Start();
        }

        public static void StopPool(string name)
        {
            var pools = GetAppPools();
            var p = pools.SingleOrDefault(pool => pool.Name == name);
            if (p != null && p.State == ObjectState.Started)
                p?.Stop();
        }

        public static void RestartSite(long siteId)
        {
            var site = GetSite(siteId);
            if (site != null)
            {
                site.Stop();
                site.Start();
            }
        }

        public static void StopSite(long siteId)
        {
            var site = GetSite(siteId);
            site?.Stop();
        }

        public static void StartSite(long siteId)
        {
            var site = GetSite(siteId);
            site?.Start();
        }

        public static Dictionary<string, string> GetIPRules()
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

        public static void DeleteIPRule(string ipaddress)
        {
            var manager = new IisServerManager();
            Configuration config = manager.GetApplicationHostConfiguration();
            var ipSecuritySection = config.GetSection("system.webServer/security/ipSecurity");
            ConfigurationElementCollection ipSecurityCollection = ipSecuritySection.GetCollection();
            ConfigurationElement addElement = FindElement(ipSecurityCollection, "add", "ipAddress", ipaddress) ?? throw new InvalidOperationException("Element not found!");
            ipSecurityCollection.Remove(addElement);
            manager.CommitChanges();
        }

        public static void AddIPRule(string ipaddress, bool denyRule)
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

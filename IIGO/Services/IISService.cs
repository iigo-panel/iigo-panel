using IISManager.Services;
using Microsoft.Web.Administration;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
    }
}

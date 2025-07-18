﻿using IISManager.Services;
using Microsoft.Web.Administration;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace IIGO.Services
{
    internal static class IISService
    {
        public static async Task<List<ApplicationPool>> GetAppPools()
        {
            try
            {
                return await Task.Run(() =>
                {
                    using (ServerManager manager = new ServerManager())
                    {
                        ApplicationPoolCollection applicationPoolCollection = manager.ApplicationPools;
                        return applicationPoolCollection.OrderBy(x => x.Name).ToList();
                    }
                });
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry(Constants.EventLogSource, $"Error loading application pools\n\n{ex.Demystify()}", EventLogEntryType.Error, 1005);
                return [];
            }
        }
        public static async Task<ApplicationPool?> GetAppPool(string name)
        {
            try
            {
                return await Task.Run(() =>
                {
                    using (ServerManager manager = new ServerManager())
                    {
                        ApplicationPoolCollection applicationPoolCollection = manager.ApplicationPools;
                        return applicationPoolCollection.SingleOrDefault(x => x.Name == name);
                    }
                });
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry(Constants.EventLogSource, $"Error loading application pools\n\n{ex.Demystify()}", EventLogEntryType.Error, 1005);
                return null;
            }
        }
        public static async Task<List<Site>> GetSites()
        {
            try
            {
                return await Task.Run(() =>
                {
                    return new IISWrapper().ListSites();
                });
            }
            catch
            {
                return [];
            }
        }

        public static async Task<Site?> GetSite(long id)
        {
            try
            {
                return await Task.Run(() =>
                {
                    return new IISWrapper().GetSite(id);
                });
            }
            catch
            {
                return null;
            }
        }

        public static async Task Recycle(string name)
        {
            var pools = await GetAppPools();
            var p = pools.SingleOrDefault(pool => pool.Name == name);
            p?.Recycle();
        }

        public static async Task StartPool(string name)
        {
            var pools = await GetAppPools();
            var p = pools.SingleOrDefault(pool => pool.Name == name);
            if (p != null && p.State == ObjectState.Stopped)
                p?.Start();
        }

        public static async Task StopPool(string name)
        {
            var pools = await GetAppPools();
            var p = pools.SingleOrDefault(pool => pool.Name == name);
            if (p != null && p.State == ObjectState.Started)
                p?.Stop();
        }

        public static async Task RestartSite(long siteId)
        {
            var site = await GetSite(siteId);
            if (site != null)
            {
                site.Stop();
                site.Start();
            }
        }

        public static async Task StopSite(long siteId)
        {
            var site = await GetSite(siteId);
            site?.Stop();
        }

        public static async Task StartSite(long siteId)
        {
            var site = await GetSite(siteId);
            site?.Start();
        }
    }
}

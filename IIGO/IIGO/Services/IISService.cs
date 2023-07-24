using IISManager.Services;
using Microsoft.Web.Administration;
using System.Collections.Generic;
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
            catch
            {
                return new List<ApplicationPool>();
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
                return new List<Site>();
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
            if (p.State == ObjectState.Stopped)
                p?.Start();
        }

        public static async Task StopPool(string name)
        {
            var pools = await GetAppPools();
            var p = pools.SingleOrDefault(pool => pool.Name == name);
            if (p.State == ObjectState.Started)
                p?.Stop();
        }
    }
}

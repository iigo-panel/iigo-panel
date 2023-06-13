using IISManager.Services;
using Microsoft.Web.Administration;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IIGO.Services
{
    public class IISService
    {
        public async Task<List<ApplicationPool>> GetAppPools()
        {
            try
            {
                return await Task.Run(() =>
                {
                    using (ServerManager manager = new ServerManager())
                    {
                        ApplicationPoolCollection applicationPoolCollection = manager.ApplicationPools;
                        return applicationPoolCollection.ToList();
                    }
                });
            }
            catch
            {
                return new List<ApplicationPool>();
            }
        }
        public async Task<List<dynamic>> GetSites()
        {
            try
            {
                return await Task.Run(() =>
                {
                    return new IISWrapper().ListWebsites();
                });
            }
            catch
            {
                return new List<dynamic>();
            }
        }
    }
}

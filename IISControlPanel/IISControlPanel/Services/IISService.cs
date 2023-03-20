using Microsoft.Web.Administration;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IISControlPanel.Services
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
    }
}

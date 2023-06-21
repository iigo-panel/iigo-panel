using System.Threading.Tasks;
using WUApiLib;

namespace IIGO.Services
{
    public class WindowsUpdateService
    {
        public async Task<UpdateCollection> GetUpdates()
        {
            return await Task.Run(() =>
            {
                UpdateSession uSession = new UpdateSession();
                IUpdateSearcher uSearcher = uSession.CreateUpdateSearcher();
                ISearchResult uResult = uSearcher.Search("IsInstalled=0 and Type = 'Software'");

                return uResult.Updates;
            });
        }

        public async Task DownloadUpdates(UpdateCollection updates)
        {
            await Task.Run(() =>
            {
                UpdateSession uSession = new UpdateSession();
                UpdateDownloader downloader = uSession.CreateUpdateDownloader();
                downloader.Updates = updates;
                downloader.Download();
            });
        }
    }
}

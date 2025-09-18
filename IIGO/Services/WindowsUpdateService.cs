using WUApiLib;

namespace IIGO.Services
{
    internal class WindowsUpdateService
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

        public async Task InstallUpdates(UpdateCollection updates)
        {
            await Task.Run(() =>
            {
                UpdateSession uSession = new UpdateSession();
                IUpdateInstaller installer = uSession.CreateUpdateInstaller();
                installer.Updates = updates;
                installer.Install();
            });
        }
    }
}

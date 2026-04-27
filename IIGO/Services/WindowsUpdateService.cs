using WUApiLib;

namespace IIGO.Services
{
    public record WindowsUpdateInfo(
        string UpdateId,
        int RevisionNumber,
        string Title,
        decimal SizeMB);

    public record UpdateInstallProgress(
        string Phase,
        int CurrentIndex,
        int TotalCount,
        string CurrentTitle,
        int OverallPercent,
        bool RebootRequired);

    public enum UpdateItemStatus { Pending, Downloading, Installing, Succeeded, Failed }

    internal class WindowsUpdateService
    {
        private List<WindowsUpdateInfo>? _cachedUpdates;
        private DateTime? _lastChecked;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public bool IsInstalling { get; private set; }
        public DateTime? LastChecked => _lastChecked;

        public async Task<List<WindowsUpdateInfo>> GetUpdates()
        {
            if (_cachedUpdates != null)
                return _cachedUpdates;

            return await FetchUpdates();
        }

        public async Task<List<WindowsUpdateInfo>> RefreshUpdates()
        {
            _cachedUpdates = null;
            return await FetchUpdates();
        }

        private async Task<List<WindowsUpdateInfo>> FetchUpdates()
        {
            var result = await Task.Run(() =>
            {
                UpdateSession uSession = new UpdateSession();
                IUpdateSearcher uSearcher = uSession.CreateUpdateSearcher();
                ISearchResult uResult = uSearcher.Search("IsInstalled=0 and Type = 'Software'");

                var list = new List<WindowsUpdateInfo>();
                foreach (IUpdate update in uResult.Updates)
                {
                    list.Add(new WindowsUpdateInfo(
                        update.Identity.UpdateID,
                        update.Identity.RevisionNumber,
                        update.Title,
                        update.MaxDownloadSize / (decimal)Math.Pow(2, 20)));
                }
                return list;
            });

            _cachedUpdates = result;
            _lastChecked = DateTime.Now;
            return result;
        }

        public async Task InstallWithProgressAsync(
            IReadOnlyList<WindowsUpdateInfo> toInstall,
            IProgress<UpdateInstallProgress> progress,
            Dictionary<string, UpdateItemStatus> statusMap,
            CancellationToken ct)
        {
            await _lock.WaitAsync(ct);
            IsInstalling = true;
            bool anyRebootRequired = false;
            int total = toInstall.Count;

            try
            {
                for (int i = 0; i < total; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    var info = toInstall[i];

                    // Re-search for a fresh COM object by UpdateId
                    IUpdate? freshUpdate = await Task.Run(() =>
                    {
                        UpdateSession uSession = new UpdateSession();
                        IUpdateSearcher uSearcher = uSession.CreateUpdateSearcher();
                        ISearchResult uResult = uSearcher.Search($"UpdateID='{info.UpdateId}' and IsInstalled=0 and Type = 'Software'");
                        return uResult.Updates.Count > 0 ? uResult.Updates[0] : null;
                    });

                    if (freshUpdate == null)
                    {
                        statusMap[info.UpdateId] = UpdateItemStatus.Failed;
                        int skipPct = (int)Math.Round((i + 1) * 100.0 / total);
                        progress.Report(new UpdateInstallProgress("Installing", i + 1, total, info.Title, skipPct, anyRebootRequired));
                        continue;
                    }

                    // Download
                    statusMap[info.UpdateId] = UpdateItemStatus.Downloading;
                    int dlPct = (int)Math.Round((i * 100.0 + 25) / total);
                    progress.Report(new UpdateInstallProgress("Downloading", i + 1, total, info.Title, dlPct, anyRebootRequired));

                    await Task.Run(() =>
                    {
                        UpdateSession uSession = new UpdateSession();
                        UpdateDownloader downloader = uSession.CreateUpdateDownloader();
                        UpdateCollection uc = new UpdateCollection();
                        uc.Add(freshUpdate);
                        downloader.Updates = uc;
                        downloader.Download();
                    });

                    ct.ThrowIfCancellationRequested();

                    // Install
                    statusMap[info.UpdateId] = UpdateItemStatus.Installing;
                    int instPct = (int)Math.Round((i * 100.0 + 60) / total);
                    progress.Report(new UpdateInstallProgress("Installing", i + 1, total, info.Title, instPct, anyRebootRequired));

                    bool succeeded = false;
                    await Task.Run(() =>
                    {
                        UpdateSession uSession = new UpdateSession();
                        IUpdateInstaller installer = uSession.CreateUpdateInstaller();
                        UpdateCollection uc = new UpdateCollection();
                        uc.Add(freshUpdate);
                        installer.Updates = uc;
                        IInstallationResult result = installer.Install();

                        if (result.RebootRequired)
                            anyRebootRequired = true;

                        IUpdateInstallationResult updateResult = result.GetUpdateResult(0);
                        succeeded = updateResult.HResult == 0;
                    });

                    statusMap[info.UpdateId] = succeeded ? UpdateItemStatus.Succeeded : UpdateItemStatus.Failed;
                    int donePct = (int)Math.Round((i + 1) * 100.0 / total);
                    progress.Report(new UpdateInstallProgress("Installing", i + 1, total, info.Title, donePct, anyRebootRequired));
                }
            }
            finally
            {
                _cachedUpdates = null;
                IsInstalling = false;
                _lock.Release();
            }

            progress.Report(new UpdateInstallProgress("Done", total, total, "", 100, anyRebootRequired));
        }
    }
}


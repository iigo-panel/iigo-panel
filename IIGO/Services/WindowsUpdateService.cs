using IIGO.Data;
using Microsoft.EntityFrameworkCore;
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

    /// <summary>
    /// Searches for Windows updates in the background once a week, so pages can show the last
    /// result without waiting on the search. A search can also be started on demand.
    /// The last result is saved to the database so it survives a restart.
    /// </summary>
    internal class WindowsUpdateService(IServiceScopeFactory scopeFactory, ILogger<WindowsUpdateService> logger) : BackgroundService
    {
        static readonly TimeSpan CheckInterval = TimeSpan.FromDays(7);

        // Short first delay so the app finishes starting, then a periodic look at whether a check is due.
        // Polling (rather than one long delay) means a manual check pushes the next scheduled one back.
        static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);
        static readonly TimeSpan PollInterval = TimeSpan.FromHours(1);

        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly Lock _checkLock = new();
        private Task? _currentCheck;

        public bool IsInstalling { get; private set; }

        /// <summary>True while a search is running.</summary>
        public bool IsChecking { get; private set; }

        /// <summary>Updates found by the last successful search, or null if no search has finished yet.</summary>
        public IReadOnlyList<WindowsUpdateInfo>? Updates { get; private set; }

        /// <summary>When the last successful search finished (UTC).</summary>
        public DateTime? LastChecked { get; private set; }

        /// <summary>Why the last search failed, or null if it succeeded.</summary>
        public string? LastError { get; private set; }

        /// <summary>Raised when a search starts or finishes.</summary>
        public event Action? Changed;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await LoadLastCheckAsync(stoppingToken);

            var delay = StartupDelay;
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                delay = PollInterval;
                if (IsInstalling || (LastChecked.HasValue && DateTime.UtcNow - LastChecked.Value < CheckInterval))
                    continue;

                await CheckNowAsync();
            }
        }

        /// <summary>
        /// Starts a search, or returns the one already running. Never throws; failures are reported through <see cref="LastError"/>.
        /// </summary>
        public Task CheckNowAsync()
        {
            lock (_checkLock)
            {
                if (_currentCheck == null || _currentCheck.IsCompleted)
                    _currentCheck = RunCheckAsync();
                return _currentCheck;
            }
        }

        private async Task RunCheckAsync()
        {
            IsChecking = true;
            Changed?.Invoke();
            try
            {
                var updates = await FetchUpdates();
                var checkedAt = DateTime.UtcNow;
                Updates = updates;
                LastChecked = checkedAt;
                LastError = null;

                // Inside the check so only one save runs at a time
                await SaveLastCheckAsync(updates, checkedAt);
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                logger.LogError(ex, "Windows Update search failed");
            }
            finally
            {
                IsChecking = false;
            }
            Changed?.Invoke();
        }

        private async Task LoadLastCheckAsync(CancellationToken ct)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var check = await context.WindowsUpdateCheck
                    .Include(x => x.Updates)
                    .OrderByDescending(x => x.CheckedAt)
                    .FirstOrDefaultAsync(ct);
                if (check == null)
                    return;

                // A manual check may already have finished; don't replace it with the older saved one
                if (LastChecked.HasValue)
                    return;

                Updates = [.. check.Updates.OrderBy(x => x.Id).Select(x => new WindowsUpdateInfo(x.UpdateId, x.RevisionNumber, x.Title, x.SizeMB))];
                LastChecked = DateTime.SpecifyKind(check.CheckedAt, DateTimeKind.Utc);
                Changed?.Invoke();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Not fatal: the scheduled check runs as if no check had been saved
                logger.LogError(ex, "Could not load the last Windows Update check");
            }
        }

        private async Task SaveLastCheckAsync(IReadOnlyList<WindowsUpdateInfo> updates, DateTime checkedAt)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Only the latest check is kept; its updates are removed with it
                context.WindowsUpdateCheck.RemoveRange(await context.WindowsUpdateCheck.ToListAsync());
                context.WindowsUpdateCheck.Add(new WindowsUpdateCheck
                {
                    CheckedAt = checkedAt,
                    Updates = [.. updates.Select(x => new WindowsUpdate
                    {
                        UpdateId = x.UpdateId,
                        RevisionNumber = x.RevisionNumber,
                        Title = x.Title.Length <= 500 ? x.Title : x.Title[..500],
                        SizeMB = x.SizeMB
                    })]
                });
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // The result is still shown; it just won't survive a restart
                logger.LogError(ex, "Could not save the Windows Update check");
            }
        }

        private static async Task<List<WindowsUpdateInfo>> FetchUpdates()
        {
            return await Task.Run(() =>
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
                IsInstalling = false;
                _lock.Release();

                // Installed updates drop off the list once Windows Update is searched again
                _ = CheckNowAsync();
            }

            progress.Report(new UpdateInstallProgress("Done", total, total, "", 100, anyRebootRequired));
        }
    }
}


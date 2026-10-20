using IIGO.Data;
using IIGO.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Web.Administration;

namespace IIGO.Services
{
    /// <summary>
    /// Starts application pools that have stopped, records each start, and sends a notification
    /// once a pool has been restarted too many times within the configured window.
    /// </summary>
    internal class AppPoolMonitorService(IServiceScopeFactory scopeFactory, ILogger<AppPoolMonitorService> logger) : MonitorServiceBase(scopeFactory, logger)
    {
        public static readonly IReadOnlyList<MonitorSetting> Settings =
        [
            new("AppPoolMonitorEnabled", "Monitoring enabled", MonitorSettingType.Toggle, "true"),
            new("AppPoolMonitorInterval", "Check interval (minutes)", MonitorSettingType.Number, "2"),
            new("AppPoolMonitorExclusions", "Excluded pools", MonitorSettingType.Text, "", "Comma-separated. Pools whose name contains any of these are never restarted."),
            new("AppPoolNotificationThreshold", "Notification threshold", MonitorSettingType.NumberPair, "5,30", "Restarts,days. \"5,30\" notifies from the 5th restart within 30 days."),
            new("AppPoolNotificationEmail", "Notification recipients", MonitorSettingType.EmailList, "admin@example.com", "Comma-separated email addresses, used by email notification methods."),
            new("AppPoolHistoryLength", "Keep history (days)", MonitorSettingType.Number, "365")
        ];

        protected override string EnabledSetting => "AppPoolMonitorEnabled";
        protected override string IntervalSetting => "AppPoolMonitorInterval";
        protected override int DefaultIntervalMinutes => 2;

        protected override async Task RunCheckAsync(IServiceProvider services, ApplicationDbContext context, CancellationToken ct)
        {
            var exclusions = GetSetting(context, "AppPoolMonitorExclusions").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var (threshold, days) = ParseThreshold(GetSetting(context, "AppPoolNotificationThreshold", "5,30"));
            var now = DateTime.UtcNow;
            var since = now.AddDays(-days);
            var notify = new List<string>();

            // One appcmd call for every pool's state, instead of one per pool
            var stopped = await ListAppCmdNamesAsync("list apppool /state:Stopped", ct);

            // A new manager each run so pool settings (e.g. AutoStart) are current
            var manager = new IisServerManager(true, ApplicationHostConfigPath);
            foreach (ApplicationPool pool in manager.ApplicationPools)
            {
                ct.ThrowIfCancellationRequested();

                if (!stopped.Contains(pool.Name))
                    continue;

                // Pools set to not start automatically are stopped on purpose
                if (!pool.AutoStart || exclusions.Any(x => pool.Name.Contains(x, StringComparison.OrdinalIgnoreCase)))
                    continue;

                pool.Start();
                logger.LogWarning("Started stopped application pool {AppPool}", pool.Name);

                context.AppPoolMonitoring.Add(new AppPoolMonitoring { AppPoolName = pool.Name, AppPoolStartTime = now });
                await context.SaveChangesAsync(ct);

                var starts = await context.AppPoolMonitoring.CountAsync(x => x.AppPoolName == pool.Name && x.AppPoolStartTime > since, ct);
                if (starts >= threshold)
                    notify.Add($"{pool.Name} (started {starts} times in the last {days} days)");
            }

            if (notify.Count > 0)
            {
                await SendNotificationAsync(services, context, NotificationFeature.AppPoolMonitor, "AppPoolNotificationEmail", "App Pool Notification",
                    "The following application pools were stopped and have been restarted:\n\n" + string.Join("\n", notify), ct);
            }

            var historyDays = GetIntSetting(context, "AppPoolHistoryLength", 365);
            var cutoff = now.AddDays(-historyDays);
            await context.AppPoolMonitoring.Where(x => x.AppPoolStartTime < cutoff).ExecuteDeleteAsync(ct);
        }

        // Setting format is "starts,days", e.g. "5,30" notifies on the 5th start within 30 days
        static (int Threshold, int Days) ParseThreshold(string value)
        {
            var parts = value.Split(',', StringSplitOptions.TrimEntries);
            var threshold = parts.Length > 0 && int.TryParse(parts[0], out var t) && t > 0 ? t : 5;
            var days = parts.Length > 1 && int.TryParse(parts[1], out var d) && d > 0 ? d : 30;
            return (threshold, days);
        }
    }
}

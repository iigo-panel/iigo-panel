using IIGO.Data;
using IIGO.Models;
using IIGO.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IIGO.Services
{
    /// <summary>
    /// Runs a check on a repeating interval for the lifetime of the application.
    /// Settings are re-read on every run so changes apply without a restart.
    /// </summary>
    public abstract class MonitorServiceBase(IServiceScopeFactory scopeFactory, ILogger logger) : BackgroundService
    {
        protected static string ApplicationHostConfigPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "inetsrv", "config", "applicationHost.config");

        protected abstract string EnabledSetting { get; }
        protected abstract string IntervalSetting { get; }
        protected abstract int DefaultIntervalMinutes { get; }

        // Short first delay so the app finishes starting, then the configured interval between runs
        static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);

        /// <summary>When the last check completed successfully (UTC).</summary>
        public DateTime? LastRun { get; private set; }

        /// <summary>When the last check started (UTC), whether or not it succeeded.</summary>
        public DateTime? LastAttempt { get; private set; }

        /// <summary>Why the last check failed, or null if it succeeded.</summary>
        public string? LastError { get; private set; }

        /// <summary>True while a check is running.</summary>
        public bool IsRunning => _running;
        volatile bool _running;

        protected abstract Task RunCheckAsync(IServiceProvider services, ApplicationDbContext context, CancellationToken ct);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var delay = StartupDelay;
            while (!stoppingToken.IsCancellationRequested)
            {
                // Wait before each run so a run never overlaps the next
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                delay = TimeSpan.FromMinutes(DefaultIntervalMinutes);
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    delay = TimeSpan.FromMinutes(Math.Max(1, GetIntSetting(context, IntervalSetting, DefaultIntervalMinutes)));
                    if (!GetBoolSetting(context, EnabledSetting, true))
                        continue;

                    LastAttempt = DateTime.UtcNow;
                    _running = true;
                    try
                    {
                        await RunCheckAsync(scope.ServiceProvider, context, stoppingToken);
                    }
                    finally
                    {
                        _running = false;
                    }
                    LastRun = DateTime.UtcNow;
                    LastError = null;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    logger.LogError(ex, "{Monitor} check failed", GetType().Name);
                }
            }
        }

        /// <summary>
        /// Sends through every messenger service assigned to the feature. Returns true only if all of them succeeded.
        /// </summary>
        protected async Task<bool> SendNotificationAsync(IServiceProvider services, ApplicationDbContext context, string feature, string recipientSetting, string subject, string body, CancellationToken ct)
        {
            var assigned = await context.NotificationSubscription
                .Where(x => x.Feature == feature)
                .Select(x => x.ServiceIdentifier)
                .ToListAsync(ct);

            if (assigned.Count == 0)
            {
                logger.LogInformation("{Monitor} notification not sent: no notification services are assigned to {Feature}", GetType().Name, feature);
                return false;
            }

            var message = new MessageData
            {
                Subject = $"[{Environment.MachineName}] {subject}",
                Body = body,
                Recipients = [.. GetSetting(context, recipientSetting).Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)]
            };

            var messengers = services.GetServices<IMessengerService>().ToList();
            var allSent = true;
            foreach (var identifier in assigned)
            {
                var messenger = messengers.FirstOrDefault(x => x.ServiceIdentifier == identifier);
                if (messenger == null)
                {
                    logger.LogWarning("{Monitor} notification not sent: unknown messenger service '{Service}' is assigned to {Feature}", GetType().Name, identifier, feature);
                    allSent = false;
                    continue;
                }

                // One failing service must not stop the others
                bool sent;
                try
                {
                    sent = await messenger.SendMessageAsync(message, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "{Monitor} notification threw an error in {Messenger}", GetType().Name, messenger.ServiceName);
                    sent = false;
                }

                if (!sent)
                {
                    logger.LogWarning("{Monitor} notification failed to send through {Messenger}", GetType().Name, messenger.ServiceName);
                    allSent = false;
                }
            }

            return allSent;
        }

        protected static string GetSetting(ApplicationDbContext context, string setting, string defaultValue = "") =>
            context.ConfigSetting.FirstOrDefault(x => x.SettingName == setting)?.SettingValue ?? defaultValue;

        protected static int GetIntSetting(ApplicationDbContext context, string setting, int defaultValue) =>
            int.TryParse(GetSetting(context, setting), out var value) ? value : defaultValue;

        protected static bool GetBoolSetting(ApplicationDbContext context, string setting, bool defaultValue) =>
            bool.TryParse(GetSetting(context, setting), out var value) ? value : defaultValue;
    }
}

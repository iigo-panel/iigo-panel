using IIGO.Models;
using IIGO.Services;
using IIGO.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace IIGO.Api
{
    /// <summary>
    /// The App Pool and HTTPS monitors: status, logged events, and settings, like the two monitor pages.
    /// </summary>
    [Route("api/monitors")]
    public class MonitorsController(
        ILogger<MonitorsController> logger,
        AppPoolMonitorService appPoolMonitor,
        CertificateMonitorService certificateMonitor,
        MonitorHistoryService historyService,
        ConfigurationService configService,
        SecretProtector secrets,
        IEnumerable<IMessengerService> messengers) : ApiControllerBase(logger)
    {
        const int DefaultDays = 30;
        const int MaxDays = 3650;

        // ---- App Pool monitor ----

        [HttpGet("apppool")]
        public ActionResult<MonitorStatusDto> GetAppPoolMonitor() => Status(appPoolMonitor, AppPoolMonitorService.Settings);

        /// <summary>Pools the monitor started in the last <paramref name="days"/> days, newest first, with a count per pool.</summary>
        [HttpGet("apppool/events")]
        public async Task<ActionResult<AppPoolEventsDto>> GetAppPoolEvents([FromQuery] int days = DefaultDays)
        {
            days = Math.Clamp(days, 1, MaxDays);
            var events = await historyService.GetAppPoolEventsAsync(days);
            return new AppPoolEventsDto(
                days,
                [.. events.GroupBy(x => x.AppPoolName)
                    .OrderByDescending(x => x.Count())
                    .Select(x => new AppPoolRestartSummary(x.Key, x.Count(), Utc(x.Max(e => e.AppPoolStartTime))))],
                [.. events.Select(x => new AppPoolEvent(x.AppPoolName, Utc(x.AppPoolStartTime)))]);
        }

        [HttpGet("apppool/settings")]
        public ActionResult<MonitorSettingsDto> GetAppPoolSettings() =>
            GetSettings(AppPoolMonitorService.Settings, NotificationFeature.AppPoolMonitor);

        [HttpPut("apppool/settings")]
        public ActionResult<MonitorSettingsDto> UpdateAppPoolSettings([FromBody] UpdateMonitorSettingsRequest request) =>
            UpdateSettings("App Pool", AppPoolMonitorService.Settings, NotificationFeature.AppPoolMonitor, request);

        // ---- HTTPS (certificate) monitor ----

        /// <summary>Monitor status and the issues found by the last check. CurrentIssues is null until the first check finishes.</summary>
        [HttpGet("https")]
        public ActionResult<HttpsMonitorStatusDto> GetHttpsMonitor()
        {
            var status = Status(certificateMonitor, CertificateMonitorService.Settings);
            return new HttpsMonitorStatusDto(
                status.Enabled,
                status.IsRunning,
                status.LastRun,
                status.LastAttempt,
                status.LastError,
                certificateMonitor.LastRun == null ? null : [.. certificateMonitor.LastIssues.Select(x => new CertificateIssueDto(x.SiteName, x.Binding, x.Type.ToString(), x.Description))]);
        }

        /// <summary>Issues logged in the last <paramref name="days"/> days, newest first. Each issue is logged once per notification period.</summary>
        [HttpGet("https/events")]
        public async Task<ActionResult<List<CertificateEventDto>>> GetHttpsEvents([FromQuery] int days = DefaultDays)
        {
            days = Math.Clamp(days, 1, MaxDays);
            var events = await historyService.GetCertificateEventsAsync(days);
            return events.Select(x => new CertificateEventDto(Utc(x.LoggedTime), x.SiteName, x.Binding, x.IssueType, x.Description)).ToList();
        }

        [HttpGet("https/settings")]
        public ActionResult<MonitorSettingsDto> GetHttpsSettings() =>
            GetSettings(CertificateMonitorService.Settings, NotificationFeature.CertificateMonitor);

        [HttpPut("https/settings")]
        public ActionResult<MonitorSettingsDto> UpdateHttpsSettings([FromBody] UpdateMonitorSettingsRequest request) =>
            UpdateSettings("HTTPS", CertificateMonitorService.Settings, NotificationFeature.CertificateMonitor, request);

        // ---- Shared ----

        private MonitorStatusDto Status(MonitorServiceBase monitor, IReadOnlyList<MonitorSetting> settings)
        {
            var enabledSetting = settings.First(x => x.Type == MonitorSettingType.Toggle);
            var enabled = !bool.TryParse(configService.GetSetting(enabledSetting.Name, enabledSetting.DefaultValue), out var on) || on;
            return new MonitorStatusDto(enabled, monitor.IsRunning, monitor.LastRun, monitor.LastAttempt, monitor.LastError);
        }

        private ActionResult<MonitorSettingsDto> GetSettings(IReadOnlyList<MonitorSetting> settings, string feature)
        {
            var stored = configService.GetSettings(settings.Select(x => x.Name));
            return new MonitorSettingsDto(
                [.. settings.Select(x => new MonitorSettingDto(
                    x.Name,
                    x.Label,
                    x.Type.ToString(),
                    // Secrets are never returned, only whether one is saved
                    x.Type == MonitorSettingType.Secret ? null : stored.GetValueOrDefault(x.Name, x.DefaultValue),
                    x.Type == MonitorSettingType.Secret ? !string.IsNullOrEmpty(stored.GetValueOrDefault(x.Name)) : null,
                    x.DefaultValue,
                    x.Help,
                    x.Options))],
                [.. configService.GetNotificationServices(feature).Order()],
                AvailableNotificationServices());
        }

        /// <summary>
        /// Changes only the settings named in the request, and the notification services if they're included.
        /// Values are checked the same way as on the monitor page; nothing is saved if any value is rejected.
        /// </summary>
        private ActionResult<MonitorSettingsDto> UpdateSettings(string monitorName, IReadOnlyList<MonitorSetting> settings, string feature, UpdateMonitorSettingsRequest request)
        {
            var errors = new Dictionary<string, string[]>();
            var changes = new Dictionary<string, string>();

            foreach (var (name, rawValue) in request.Settings ?? [])
            {
                var setting = settings.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
                if (setting == null)
                {
                    errors[name] = [$"Unknown setting. Valid names: {string.Join(", ", settings.Select(x => x.Name))}"];
                    continue;
                }

                var value = rawValue?.Trim() ?? "";
                // A blank secret keeps the stored value, as on the page
                if (setting.Type == MonitorSettingType.Secret && value.Length == 0)
                    continue;

                var error = setting.Validate(value);
                if (error != null)
                    errors[setting.Name] = [error];
                else
                    changes[setting.Name] = setting.Type == MonitorSettingType.Secret ? secrets.Protect(value) : value;
            }

            HashSet<string>? services = null;
            if (request.NotificationServices != null)
            {
                var available = AvailableNotificationServices().Select(x => x.Identifier).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var unknown = request.NotificationServices.Where(x => !available.Contains(x)).ToList();
                if (unknown.Count > 0)
                    errors[nameof(request.NotificationServices)] = [$"Unknown notification service: {string.Join(", ", unknown)}. Valid values: {string.Join(", ", available)}"];
                else
                    // Store the canonical spelling
                    services = [.. request.NotificationServices.Select(x => available.First(a => a.Equals(x, StringComparison.OrdinalIgnoreCase)))];
            }

            // Email has no one to send to without recipients
            var effectiveServices = services ?? configService.GetNotificationServices(feature);
            if (effectiveServices.Contains(EmailNotifications.ServiceIdentifier))
            {
                var stored = configService.GetSettings(settings.Select(x => x.Name));
                foreach (var setting in settings.Where(x => x.Type == MonitorSettingType.EmailList))
                {
                    var value = changes.TryGetValue(setting.Name, out var changed) ? changed : stored.GetValueOrDefault(setting.Name, setting.DefaultValue);
                    if (string.IsNullOrWhiteSpace(value))
                        errors.TryAdd(setting.Name, ["Enter at least one address, or remove Email from NotificationServices."]);
                }
            }

            if (errors.Count > 0)
                return ValidationProblem(new ValidationProblemDetails(errors));

            if (changes.Count > 0)
                configService.SaveSettings(changes);
            if (services != null)
                configService.SetNotificationServices(feature, services);

            Logger.LogWarning("API key {KeyName} changed {Monitor} monitor settings: {Settings}{Services}", KeyName, monitorName,
                string.Join(", ", changes.Keys), services == null ? "" : $"; notification services: {string.Join(", ", services)}");

            return GetSettings(settings, feature);
        }

        // "Email" sends through the email service chosen on the Settings page; the others are listed individually
        private List<NotificationServiceDto> AvailableNotificationServices()
        {
            var emailService = configService.GetSetting(EmailNotifications.Setting, EmailNotifications.DefaultService);
            var emailName = messengers.FirstOrDefault(x => x.ServiceIdentifier == emailService)?.ServiceName ?? emailService;
            return
            [
                new(EmailNotifications.ServiceIdentifier, $"Email ({emailName})"),
                .. messengers.Where(x => !x.IsEmail).Select(x => new NotificationServiceDto(x.ServiceIdentifier, x.ServiceName))
            ];
        }

        // SQLite returns DateTimeKind.Unspecified; the monitors store UTC
        private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

        // Times are UTC
        public record MonitorStatusDto(bool Enabled, bool IsRunning, DateTime? LastRun, DateTime? LastAttempt, string? LastError);

        public record HttpsMonitorStatusDto(bool Enabled, bool IsRunning, DateTime? LastRun, DateTime? LastAttempt, string? LastError, List<CertificateIssueDto>? CurrentIssues);

        // Type is NoCertificate, NotFound, Expired, Expiring or HostMismatch
        public record CertificateIssueDto(string SiteName, string Binding, string Type, string Description);

        public record CertificateEventDto(DateTime LoggedTime, string SiteName, string Binding, string IssueType, string Description);

        public record AppPoolEventsDto(int Days, List<AppPoolRestartSummary> Summary, List<AppPoolEvent> Events);

        public record AppPoolRestartSummary(string AppPoolName, int Restarts, DateTime LastRestart);

        public record AppPoolEvent(string AppPoolName, DateTime StartTime);

        // Value is null for secrets; HasValue says whether one is saved (null for other settings)
        public record MonitorSettingDto(string Name, string Label, string Type, string? Value, bool? HasValue, string DefaultValue, string? Help, IReadOnlyList<string>? Options);

        public record MonitorSettingsDto(List<MonitorSettingDto> Settings, List<string> NotificationServices, List<NotificationServiceDto> AvailableNotificationServices);

        public record NotificationServiceDto(string Identifier, string Name);

        public class UpdateMonitorSettingsRequest
        {
            // Setting name → new value. Settings left out are unchanged.
            public Dictionary<string, string?>? Settings { get; set; }

            // Replaces the services notifications are sent through when included. Use identifiers from AvailableNotificationServices.
            public List<string>? NotificationServices { get; set; }
        }
    }
}

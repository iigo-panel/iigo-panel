using IIGO.Data;
using IIGO.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Web.Administration;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace IIGO.Services
{
    public enum CertificateIssueType { NoCertificate, NotFound, Expired, Expiring, HostMismatch }

    public record CertificateIssue(string SiteName, string Binding, CertificateIssueType Type, string Description)
    {
        public string Key => $"{SiteName}|{Binding}|{Type}";
    }

    /// <summary>
    /// Checks the certificates on HTTPS bindings of running sites for expiration, missing
    /// certificates, and certificates that don't cover the binding's host name.
    /// </summary>
    internal class CertificateMonitorService(IServiceScopeFactory scopeFactory, ILogger<CertificateMonitorService> logger) : MonitorServiceBase(scopeFactory, logger)
    {
        // Stores IIS uses for binding certificates, checked when a binding doesn't name its store
        static readonly string[] DefaultStores = ["My", "WebHosting"];

        // Last time each issue was included in a notification, so the same issue isn't re-sent every run
        readonly Dictionary<string, DateTime> _notified = [];

        public static readonly IReadOnlyList<MonitorSetting> Settings =
        [
            new("CertificateMonitorEnabled", "Monitoring enabled", MonitorSettingType.Toggle, "true"),
            new("CertificateMonitorInterval", "Check interval (minutes)", MonitorSettingType.Number, "10"),
            new("CertificateExpirationThreshold", "Warn before expiry (days)", MonitorSettingType.Number, "30"),
            new("CertificateNotificationFrequency", "Repeat notifications (hours)", MonitorSettingType.Number, "24", "How often an unresolved issue is logged and reported again."),
            new("CertificateNotificationEmail", "Notification recipients", MonitorSettingType.EmailList, "admin@example.com", "Comma-separated email addresses, used by email notification methods."),
            new("CertificateHistoryLength", "Keep history (days)", MonitorSettingType.Number, "365")
        ];

        protected override string EnabledSetting => "CertificateMonitorEnabled";
        protected override string IntervalSetting => "CertificateMonitorInterval";
        protected override int DefaultIntervalMinutes => 10;

        public IReadOnlyList<CertificateIssue> LastIssues { get; private set; } = [];

        protected override async Task RunCheckAsync(IServiceProvider services, ApplicationDbContext context, CancellationToken ct)
        {
            var thresholdDays = GetIntSetting(context, "CertificateExpirationThreshold", 30);
            var issues = new List<CertificateIssue>();

            var started = IisStateReader.GetStates(IisStateReader.Sites, useCache: false)
                .Where(x => x.Value == "Started")
                .Select(x => x.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Not read-only: loading an SNI binding's certificate sets its SslFlags in memory, which throws on a
            // read-only manager. Nothing is saved because CommitChanges() is never called.
            var manager = new IisServerManager(false, ApplicationHostConfigPath);
            foreach (Site site in manager.Sites)
            {
                ct.ThrowIfCancellationRequested();

                if (!started.Contains(site.Name))
                    continue;

                foreach (Binding binding in site.Bindings)
                {
                    if (!string.Equals(binding.Protocol, "https", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Centralized certificates live in a file share, not a certificate store
                    if (binding.SslFlags.HasFlag(SslFlags.CentralCertStore))
                        continue;

                    issues.AddRange(CheckBinding(site.Name, binding, thresholdDays));
                }
            }

            LastIssues = issues;
            await NotifyAsync(services, context, issues, ct);
        }

        static IEnumerable<CertificateIssue> CheckBinding(string siteName, Binding binding, int thresholdDays)
        {
            var bindingInfo = binding.BindingInformation;
            if (binding.CertificateHash == null || binding.CertificateHash.Length == 0)
            {
                yield return new CertificateIssue(siteName, bindingInfo, CertificateIssueType.NoCertificate, "No certificate is assigned to the binding");
                yield break;
            }

            var thumbprint = Convert.ToHexString(binding.CertificateHash);
            using var cert = FindCertificate(binding.CertificateStoreName, thumbprint);
            if (cert == null)
            {
                yield return new CertificateIssue(siteName, bindingInfo, CertificateIssueType.NotFound, $"Certificate {thumbprint} was not found in the certificate store");
                yield break;
            }

            var daysLeft = (cert.NotAfter - DateTime.Now).TotalDays;
            if (daysLeft <= 0)
                yield return new CertificateIssue(siteName, bindingInfo, CertificateIssueType.Expired, $"Certificate expired on {cert.NotAfter:d}");
            else if (daysLeft < thresholdDays)
                yield return new CertificateIssue(siteName, bindingInfo, CertificateIssueType.Expiring, $"Certificate expires in {(int)daysLeft} days ({cert.NotAfter:d})");

            var host = binding.Host;
            if (!string.IsNullOrEmpty(host) && !CoversHost(cert, host))
                yield return new CertificateIssue(siteName, bindingInfo, CertificateIssueType.HostMismatch, $"Certificate issued to {cert.GetNameInfo(X509NameType.DnsName, false)} does not cover {host}");
        }

        static X509Certificate2? FindCertificate(string? storeName, string thumbprint)
        {
            var stores = string.IsNullOrEmpty(storeName) ? DefaultStores : [storeName, .. DefaultStores.Where(x => !x.Equals(storeName, StringComparison.OrdinalIgnoreCase))];
            foreach (var name in stores)
            {
                using var store = new X509Store(name, StoreLocation.LocalMachine);
                try
                {
                    store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                }
                catch (CryptographicException)
                {
                    // Store doesn't exist on this server
                    continue;
                }

                var found = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
                if (found.Count > 0)
                    return found[0];
            }

            return null;
        }

        static bool CoversHost(X509Certificate2 cert, string host)
        {
            // IIS wildcard bindings ("*.example.com") need a concrete name to test against
            if (host.StartsWith("*.", StringComparison.Ordinal))
                host = "wildcard-check" + host[1..];

            try
            {
                return cert.MatchesHostname(host);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        async Task NotifyAsync(IServiceProvider services, ApplicationDbContext context, List<CertificateIssue> issues, CancellationToken ct)
        {
            // Forget resolved issues so they notify again if they come back
            var currentKeys = issues.Select(x => x.Key).ToHashSet();
            foreach (var key in _notified.Keys.Where(x => !currentKeys.Contains(x)).ToList())
                _notified.Remove(key);

            var frequency = TimeSpan.FromHours(Math.Max(1, GetIntSetting(context, "CertificateNotificationFrequency", 24)));
            var now = DateTime.UtcNow;
            var due = issues.Where(x => !_notified.TryGetValue(x.Key, out var last) || now - last >= frequency).ToList();
            if (due.Count > 0)
            {
                // Each issue is logged and reported once per notification period, even if sending fails,
                // so a broken messenger doesn't add a log entry on every run
                foreach (var issue in due)
                {
                    _notified[issue.Key] = now;
                    context.CertificateMonitoring.Add(new CertificateMonitoring
                    {
                        SiteName = Truncate(issue.SiteName, 200),
                        Binding = Truncate(issue.Binding, 300),
                        IssueType = issue.Type.ToString(),
                        Description = Truncate(issue.Description, 500),
                        LoggedTime = now
                    });
                }
                await context.SaveChangesAsync(ct);

                var body = "The following HTTPS bindings need attention:\n\n" +
                    string.Join("\n", due.Select(x => $"{x.SiteName} ({x.Binding}): {x.Description}"));

                await SendNotificationAsync(services, context, NotificationFeature.CertificateMonitor, "CertificateNotificationEmail", "Certificate Notification", body, ct);
            }

            var cutoff = now.AddDays(-GetIntSetting(context, "CertificateHistoryLength", 365));
            await context.CertificateMonitoring.Where(x => x.LoggedTime < cutoff).ExecuteDeleteAsync(ct);
        }

        static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
    }
}

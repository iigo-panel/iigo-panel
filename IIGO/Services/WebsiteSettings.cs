using Microsoft.Web.Administration;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace IIGO.Services
{
    /// <summary>
    /// A group of site settings edited together, on the website details page and through the API. Apply runs
    /// against a fresh copy of the site, inside the same commit. Ranges follow the sites schema in IIS_schema.xml.
    /// </summary>
    public interface IWebsiteSettings
    {
        void Apply(Site site);

        /// <summary>
        /// Throws an ArgumentException with the first validation error, so a model that skipped form or API
        /// validation can't reach IIS
        /// </summary>
        static void Check(object settings)
        {
            var results = new List<ValidationResult>();
            if (!Validator.TryValidateObject(settings, new ValidationContext(settings), results, validateAllProperties: true))
                throw new ArgumentException(results[0].ErrorMessage);
        }
    }

    /// <summary>
    /// Protocols the site's root application accepts, e.g. "http" or "http,net.tcp"
    /// </summary>
    public partial class EnabledProtocolsSettings : IWebsiteSettings, IValidatableObject
    {
        public string EnabledProtocols { get; set; } = "http";

        public static EnabledProtocolsSettings From(Site site) => new()
        {
            EnabledProtocols = RootApplication(site).EnabledProtocols ?? ""
        };

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var protocols = Parse(EnabledProtocols);
            if (protocols.Count == 0)
                yield return new ValidationResult("Enter at least one protocol, such as http.", [nameof(EnabledProtocols)]);
            else if (protocols.FirstOrDefault(x => !ProtocolPattern().IsMatch(x)) is string invalid)
                yield return new ValidationResult($"\"{invalid}\" isn't a valid protocol name. Separate protocols with commas.", [nameof(EnabledProtocols)]);
        }

        public void Apply(Site site)
        {
            IWebsiteSettings.Check(this);
            RootApplication(site).EnabledProtocols = string.Join(",", Parse(EnabledProtocols));
        }

        private static List<string> Parse(string? text) =>
            [.. (text ?? "").Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => x.ToLowerInvariant())
                .Distinct()];

        private static Application RootApplication(Site site) =>
            site.Applications.FirstOrDefault(x => x.Path == "/") ?? throw new InvalidOperationException("Root application not found");

        [GeneratedRegex(@"^[a-z][a-z0-9.+-]*$")]
        private static partial Regex ProtocolPattern();
    }

    /// <summary>
    /// The settings in IIS Manager's Edit Site Limits dialog, plus the URL segment limit
    /// </summary>
    public class SiteLimitsSettings : IWebsiteSettings, IValidatableObject
    {
        /// <summary>
        /// The value IIS stores for "no limit"
        /// </summary>
        public const long Unlimited = uint.MaxValue;

        public bool LimitBandwidth { get; set; }

        /// <summary>
        /// Bytes per second. Used when LimitBandwidth is on
        /// </summary>
        public long MaxBandwidth { get; set; }

        public bool LimitConnections { get; set; }

        /// <summary>
        /// Used when LimitConnections is on
        /// </summary>
        public long MaxConnections { get; set; }

        [Range(0, 65535, ErrorMessage = "Enter 0 to 65,535 seconds.")]
        public long ConnectionTimeoutSeconds { get; set; } = 120;

        [Range(0, 16383, ErrorMessage = "Enter 0 to 16,383.")]
        public long MaxUrlSegments { get; set; } = 32;

        public static SiteLimitsSettings From(Site site)
        {
            var limits = site.Limits;
            return new()
            {
                LimitBandwidth = limits.MaxBandwidth != Unlimited,
                MaxBandwidth = limits.MaxBandwidth != Unlimited ? limits.MaxBandwidth : 0,
                LimitConnections = limits.MaxConnections != Unlimited,
                MaxConnections = limits.MaxConnections != Unlimited ? limits.MaxConnections : 0,
                ConnectionTimeoutSeconds = (long)limits.ConnectionTimeout.TotalSeconds,
                MaxUrlSegments = limits.MaxUrlSegments
            };
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (LimitBandwidth && (MaxBandwidth < 1024 || MaxBandwidth >= Unlimited))
                yield return new ValidationResult("Enter 1,024 to 4,294,967,294 bytes per second.", [nameof(MaxBandwidth)]);
            if (LimitConnections && (MaxConnections < 1 || MaxConnections >= Unlimited))
                yield return new ValidationResult("Enter 1 to 4,294,967,294 connections.", [nameof(MaxConnections)]);
        }

        public void Apply(Site site)
        {
            IWebsiteSettings.Check(this);
            var limits = site.Limits;
            limits.MaxBandwidth = LimitBandwidth ? MaxBandwidth : Unlimited;
            limits.MaxConnections = LimitConnections ? MaxConnections : Unlimited;
            limits.ConnectionTimeout = TimeSpan.FromSeconds(ConnectionTimeoutSeconds);
            limits.MaxUrlSegments = MaxUrlSegments;
        }
    }

    /// <summary>
    /// Where the site writes failed request trace logs. Requests are only traced when they match a tracing rule.
    /// </summary>
    public class FailedRequestTracingSettings : IWebsiteSettings
    {
        public bool Enabled { get; set; }

        [Required(ErrorMessage = "Enter a directory.")]
        public string Directory { get; set; } = "";

        [Range(1, 10000, ErrorMessage = "Enter 1 to 10,000 files.")]
        public long MaxLogFiles { get; set; } = 50;

        public static FailedRequestTracingSettings From(Site site)
        {
            var trace = site.TraceFailedRequestsLogging;
            return new()
            {
                Enabled = trace.Enabled,
                Directory = trace.Directory ?? "",
                MaxLogFiles = trace.MaxLogFiles
            };
        }

        public void Apply(Site site)
        {
            IWebsiteSettings.Check(this);
            var trace = site.TraceFailedRequestsLogging;
            trace.Enabled = Enabled;
            trace.Directory = Directory.Trim();
            trace.MaxLogFiles = MaxLogFiles;
        }
    }

    /// <summary>
    /// HTTP Strict Transport Security. Needs IIS 10.0 version 1709 (Windows Server 2019) or later.
    /// </summary>
    public class HstsSettings : IWebsiteSettings
    {
        /// <summary>
        /// One year, the shortest max-age browser preload lists accept
        /// </summary>
        public const long PreloadMinimumMaxAge = 31536000;

        public bool Enabled { get; set; }

        /// <summary>
        /// Seconds
        /// </summary>
        [Range(0, uint.MaxValue, ErrorMessage = "Enter 0 to 4,294,967,295 seconds.")]
        public long MaxAge { get; set; }

        public bool IncludeSubDomains { get; set; }

        public bool Preload { get; set; }

        public bool RedirectHttpToHttps { get; set; }

        /// <summary>
        /// Whether this server's IIS schema has the site hsts element
        /// </summary>
        public static bool IsSupported(Site site) => site.Schema?.ChildElementSchemas["hsts"] != null;

        /// <summary>
        /// The site's HSTS settings, or null when this version of IIS doesn't support HSTS
        /// </summary>
        public static HstsSettings? From(Site site)
        {
            if (!IsSupported(site))
                return null;

            var hsts = site.GetChildElement("hsts");
            return new()
            {
                Enabled = hsts["enabled"] is true,
                MaxAge = Convert.ToInt64(hsts["max-age"]),
                IncludeSubDomains = hsts["includeSubDomains"] is true,
                Preload = hsts["preload"] is true,
                RedirectHttpToHttps = hsts["redirectHttpToHttps"] is true
            };
        }

        public void Apply(Site site)
        {
            if (!IsSupported(site))
                throw new InvalidOperationException("HSTS needs IIS 10.0 version 1709 or later. This server's IIS doesn't support it.");
            IWebsiteSettings.Check(this);
            var hsts = site.GetChildElement("hsts");
            hsts["enabled"] = Enabled;
            hsts["max-age"] = Convert.ToUInt32(MaxAge);
            hsts["includeSubDomains"] = IncludeSubDomains;
            hsts["preload"] = Preload;
            hsts["redirectHttpToHttps"] = RedirectHttpToHttps;
        }
    }
}

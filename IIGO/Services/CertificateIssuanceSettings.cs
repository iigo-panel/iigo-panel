using IIGO.Models;

namespace IIGO.Services
{
    /// <summary>
    /// Settings for requesting certificates with simple-acme, edited on the Settings page
    /// </summary>
    internal static class CertificateIssuanceSettings
    {
        public const string WacsPath = "WacsPath";
        public const string AccountEmail = "AcmeAccountEmail";
        public const string BaseUri = "AcmeBaseUri";
        public const string CustomArguments = "WacsCustomArguments";

        public const string LetsEncryptStaging = "https://acme-staging-v02.api.letsencrypt.org/directory";
        public const string LetsEncryptProduction = "https://acme-v02.api.letsencrypt.org/directory";

        public static readonly IReadOnlyList<MonitorSetting> Settings =
        [
            new(WacsPath, "Path to wacs.exe", MonitorSettingType.Text, "",
                @"The full path to simple-acme's wacs.exe, e.g. C:\Program Files\simple-acme\wacs.exe. Leave it blank to turn off certificate requests."),
            new(AccountEmail, "ACME account email", MonitorSettingType.Email, "",
                "Passed to simple-acme with --emailaddress. The certificate authority sends expiry warnings here. Requests are turned off until it's set."),
            new(BaseUri, "ACME server", MonitorSettingType.Text, LetsEncryptStaging,
                $"The certificate authority's directory URL, passed with --baseuri. The default is Let's Encrypt staging, whose certificates browsers don't trust. Use {LetsEncryptProduction} for real certificates. Leave it blank to use simple-acme's own setting."),
            new(CustomArguments, "Extra arguments", MonitorSettingType.Text, "",
                "Added to the end of every wacs.exe command, e.g. --validation filesystem. Put values with spaces in double quotes. Shown to users as [extra arguments], so they can hold keys.")
        ];

        /// <summary>
        /// The stored values, with the default for any that aren't stored yet
        /// </summary>
        public static Dictionary<string, string> Load(ConfigurationService config)
        {
            var stored = config.GetSettings(Settings.Select(x => x.Name));
            return Settings.ToDictionary(x => x.Name, x => stored.GetValueOrDefault(x.Name, x.DefaultValue));
        }

        /// <summary>
        /// Splits a command line into arguments. Whitespace separates them, except inside double quotes.
        /// </summary>
        public static List<string> SplitArguments(string commandLine)
        {
            var args = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false, hasArg = false;
            foreach (var c in commandLine)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    hasArg = true;
                }
                else if (char.IsWhiteSpace(c) && !inQuotes)
                {
                    if (hasArg)
                        args.Add(current.ToString());
                    current.Clear();
                    hasArg = false;
                }
                else
                {
                    current.Append(c);
                    hasArg = true;
                }
            }
            if (hasArg)
                args.Add(current.ToString());
            return args;
        }

        /// <summary>
        /// Why certificate requests can't run with these settings, or null when they can
        /// </summary>
        public static string? Problem(IReadOnlyDictionary<string, string> values)
        {
            var wacsPath = values.GetValueOrDefault(WacsPath, "").Trim();
            if (wacsPath == "")
                return "The path to wacs.exe is not set.";
            if (!File.Exists(wacsPath))
                return $"wacs.exe was not found at {wacsPath}.";
            if (values.GetValueOrDefault(AccountEmail, "").Trim() == "")
                return "The ACME account email is not set.";
            var baseUri = values.GetValueOrDefault(BaseUri, "").Trim();
            if (baseUri != "" && !(Uri.TryCreate(baseUri, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps))
                return $"The ACME server {baseUri} is not an https URL.";
            if (values.GetValueOrDefault(CustomArguments, "").Count(c => c == '"') % 2 != 0)
                return "The extra arguments have an unclosed double quote.";
            return null;
        }
    }
}

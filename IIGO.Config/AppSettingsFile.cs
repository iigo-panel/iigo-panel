using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Dynamic;

namespace IIGO.Config
{
    internal enum CertificateSource
    {
        Store,
        File
    }

    /// <summary>
    /// The panel's HTTPS endpoint, as set in Kestrel:Endpoints in appsettings.json
    /// </summary>
    internal sealed class HttpsEndpoint
    {
        public string Host { get; set; } = "*";
        public int Port { get; set; } = 8443;

        public CertificateSource Source { get; set; } = CertificateSource.Store;
        public string Subject { get; set; } = "";
        public string StoreName { get; set; } = "My";
        public string StoreLocation { get; set; } = "LocalMachine";
        public bool AllowInvalid { get; set; }
        public string FilePath { get; set; } = "";
        public string FilePassword { get; set; } = "";

        public string Url => $"https://{FormatHost(Host)}:{Port}";

        /// <summary>
        /// IPv6 addresses need brackets in a URL
        /// </summary>
        public static string FormatHost(string host)
        {
            host = host.Trim();
            if (host.Length == 0)
                return "*";
            return host.Contains(':') && !host.StartsWith('[') ? $"[{host}]" : host;
        }
    }

    /// <summary>
    /// Reads and changes appsettings.json through ExpandoObject, so every setting the panel doesn't need changed here,
    /// and every other Kestrel endpoint, is written back as it was. Comments in the file are not kept.
    /// </summary>
    internal sealed class AppSettingsFile
    {
        private const string DefaultEndpointName = "Https";

        // DateParseHandling.None keeps date-like strings exactly as they were written
        private static readonly JsonSerializerSettings SerializerSettings = new()
        {
            DateParseHandling = DateParseHandling.None,
            Formatting = Formatting.Indented,
            Converters = { new ExpandoObjectConverter() }
        };

        private readonly IDictionary<string, object?> root;
        private readonly string endpointName;

        public string Path { get; }

        /// <summary>
        /// The HTTPS endpoint found in the file, or defaults when the file has none
        /// </summary>
        public HttpsEndpoint Endpoint { get; }

        /// <summary>
        /// False when the file has no HTTPS endpoint yet
        /// </summary>
        public bool HasHttpsEndpoint { get; }

        private AppSettingsFile(string path, IDictionary<string, object?> root, string endpointName, HttpsEndpoint endpoint, bool hasHttpsEndpoint)
        {
            Path = path;
            this.root = root;
            this.endpointName = endpointName;
            Endpoint = endpoint;
            HasHttpsEndpoint = hasHttpsEndpoint;
        }

        /// <summary>
        /// appsettings.json next to this program (where the installer puts it), else in the folder the installer recorded
        /// </summary>
        public static string FindDefaultPath()
        {
            var local = System.IO.Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (File.Exists(local))
                return local;

            try
            {
                using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using var key = hklm.OpenSubKey(@"SOFTWARE\IIGO Panel");
                if (key?.GetValue("InstallFolder") is string folder)
                {
                    var installed = System.IO.Path.Combine(folder, "appsettings.json");
                    if (File.Exists(installed))
                        return installed;
                }
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
            {
            }

            return local;
        }

        public static AppSettingsFile Load(string path)
        {
            var json = File.ReadAllText(path);
            var root = (IDictionary<string, object?>?)JsonConvert.DeserializeObject<ExpandoObject>(json, SerializerSettings)
                ?? throw new InvalidDataException($"{path} is empty.");

            var endpoint = new HttpsEndpoint();
            var name = DefaultEndpointName;
            var found = false;

            // Configuration keys are case-insensitive, so the file may spell them any way
            if (Child(root, "Kestrel") is { } kestrel && Child(kestrel, "Endpoints") is { } endpoints)
            {
                foreach (var (key, value) in endpoints)
                {
                    if (value is IDictionary<string, object?> settings
                        && GetString(settings, "Url") is { } url
                        && url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        name = key;
                        found = true;
                        ReadEndpoint(settings, url, endpoint);
                        break;
                    }
                }
            }

            return new AppSettingsFile(path, root, name, endpoint, found);
        }

        private static void ReadEndpoint(IDictionary<string, object?> settings, string url, HttpsEndpoint endpoint)
        {
            (endpoint.Host, endpoint.Port) = ParseUrl(url);

            if (Child(settings, "Certificate") is not { } certificate)
                return;

            if (GetString(certificate, "Path") is { Length: > 0 } filePath)
            {
                endpoint.Source = CertificateSource.File;
                endpoint.FilePath = filePath;
                endpoint.FilePassword = GetString(certificate, "Password") ?? "";
            }
            else
            {
                endpoint.Source = CertificateSource.Store;
                endpoint.Subject = GetString(certificate, "Subject") ?? "";
                endpoint.StoreName = GetString(certificate, "Store") is { Length: > 0 } store ? store : "My";
                // Kestrel's own default is CurrentUser, which the service's LocalSystem account doesn't use
                endpoint.StoreLocation = GetString(certificate, "Location") is { Length: > 0 } location ? location : "CurrentUser";
                endpoint.AllowInvalid = bool.TryParse(GetString(certificate, "AllowInvalid"), out var allowInvalid) && allowInvalid;
            }
        }

        /// <summary>
        /// Splits https://host:port. Kestrel URLs may use * or + as the host, which System.Uri won't parse.
        /// </summary>
        internal static (string Host, int Port) ParseUrl(string url)
        {
            var rest = url["https://".Length..];
            var slash = rest.IndexOf('/');
            if (slash >= 0)
                rest = rest[..slash];

            var host = rest;
            var portText = "";
            if (rest.StartsWith('['))
            {
                var end = rest.IndexOf(']');
                if (end > 0)
                {
                    host = rest[1..end];
                    var after = rest[(end + 1)..];
                    if (after.StartsWith(':'))
                        portText = after[1..];
                }
            }
            else
            {
                var colon = rest.LastIndexOf(':');
                if (colon >= 0)
                {
                    host = rest[..colon];
                    portText = rest[(colon + 1)..];
                }
            }

            var port = int.TryParse(portText, out var parsed) && parsed is > 0 and <= 65535 ? parsed : 443;
            return (host.Length == 0 ? "*" : host, port);
        }

        /// <summary>
        /// Writes the endpoint into the file. Settings on the endpoint other than Url and Certificate are kept.
        /// The previous file is kept as appsettings.json.bak.
        /// </summary>
        public void Save(HttpsEndpoint endpoint)
        {
            var kestrel = GetOrAddChild(root, "Kestrel");
            var endpoints = GetOrAddChild(kestrel, "Endpoints");
            var settings = GetOrAddChild(endpoints, endpointName);

            Set(settings, "Url", endpoint.Url);

            IDictionary<string, object?> certificate = new ExpandoObject();
            if (endpoint.Source == CertificateSource.Store)
            {
                certificate["Subject"] = endpoint.Subject;
                certificate["Store"] = endpoint.StoreName;
                certificate["Location"] = endpoint.StoreLocation;
                if (endpoint.AllowInvalid)
                    certificate["AllowInvalid"] = true;
            }
            else
            {
                certificate["Path"] = endpoint.FilePath;
                if (endpoint.FilePassword.Length > 0)
                    certificate["Password"] = endpoint.FilePassword;
            }
            Set(settings, "Certificate", certificate);

            var json = JsonConvert.SerializeObject(root, SerializerSettings);

            // Write a new file and swap it in, so a failure part way never leaves a broken appsettings.json.
            // ReplaceFile keeps the original file's permissions.
            var temp = Path + ".tmp";
            File.WriteAllText(temp, json);
            if (File.Exists(Path))
                File.Replace(temp, Path, Path + ".bak");
            else
                File.Move(temp, Path);
        }

        private static string? FindKey(IDictionary<string, object?> parent, string name) =>
            parent.Keys.FirstOrDefault(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase));

        private static IDictionary<string, object?>? Child(IDictionary<string, object?> parent, string name) =>
            FindKey(parent, name) is { } key ? parent[key] as IDictionary<string, object?> : null;

        private static string? GetString(IDictionary<string, object?> parent, string name) =>
            FindKey(parent, name) is { } key ? Convert.ToString(parent[key], System.Globalization.CultureInfo.InvariantCulture) : null;

        private static IDictionary<string, object?> GetOrAddChild(IDictionary<string, object?> parent, string name)
        {
            if (Child(parent, name) is { } existing)
                return existing;

            IDictionary<string, object?> child = new ExpandoObject();
            Set(parent, name, child);
            return child;
        }

        /// <summary>
        /// Replaces a value under its existing spelling, which also keeps its place in the file
        /// </summary>
        private static void Set(IDictionary<string, object?> parent, string name, object? value) =>
            parent[FindKey(parent, name) ?? name] = value;
    }
}

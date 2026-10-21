#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.Web.Administration
{
    /// <summary>
    /// Reads the runtime state of every application pool or site with a single appcmd call.
    /// Results are cached briefly so rendering a list of N objects costs one appcmd process instead of N.
    /// </summary>
    public static class IisStateReader
    {
        public const string AppPools = "apppool";
        public const string Sites = "site";

        static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(2);
        static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);
        static readonly object _lock = new();
        static readonly Dictionary<string, (DateTime Read, Dictionary<string, string> States)> _cache = [];

        static string AppCmdPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "inetsrv", "appcmd.exe");

        /// <summary>
        /// Returns object name → state ("Started", "Stopped", "Starting", "Stopping" or "Unknown").
        /// Throws if appcmd is missing, times out, or reports an error (e.g. access denied).
        /// </summary>
        public static IReadOnlyDictionary<string, string> GetStates(string objectType, bool useCache = true)
        {
            lock (_lock)
            {
                if (useCache && _cache.TryGetValue(objectType, out var cached) && DateTime.UtcNow - cached.Read < CacheLifetime)
                    return cached.States;

                var states = ReadStates(objectType);
                _cache[objectType] = (DateTime.UtcNow, states);
                return states;
            }
        }

        /// <summary>
        /// Clears cached states, e.g. after starting or stopping a pool or site.
        /// </summary>
        public static void Invalidate()
        {
            lock (_lock)
            {
                _cache.Clear();
            }
        }

        static Dictionary<string, string> ReadStates(string objectType)
        {
            if (!File.Exists(AppCmdPath))
                throw new FileNotFoundException("IIS appcmd.exe was not found", AppCmdPath);

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = AppCmdPath,
                    Arguments = $"list {objectType} /xml",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            process.Start();

            // Read both streams at once so a full stderr buffer can't block appcmd
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(Timeout))
            {
                try
                {
                    process.Kill();
                }
                catch (InvalidOperationException)
                {
                    // Already exited
                }
                throw new TimeoutException($"appcmd list {objectType} did not finish within {Timeout.TotalSeconds} seconds");
            }
            Task.WaitAll(outputTask, errorTask);

            return ParseStates(objectType, outputTask.Result, errorTask.Result, process.ExitCode);
        }

        internal static Dictionary<string, string> ParseStates(string objectType, string output, string error, int exitCode)
        {
            // The exit code isn't reliable (an empty list can be non-zero), so judge by the output instead.
            // With /xml, errors come back as <appcmd><ERROR message="..." /></appcmd>; anything that isn't
            // XML at all is also an error.
            XDocument document;
            try
            {
                document = XDocument.Parse(output);
            }
            catch (XmlException)
            {
                throw new InvalidOperationException($"appcmd list {objectType} failed with exit code {exitCode}: {(error + output).Trim()}");
            }

            var errorElement = document.Root?.Element("ERROR");
            if (document.Root?.Name != "appcmd" || errorElement != null)
            {
                var message = (string?)errorElement?.Attribute("message") ?? (error + output).Trim();
                throw new InvalidOperationException($"appcmd list {objectType} failed with exit code {exitCode}: {message.Trim()}");
            }

            var elementName = objectType.ToUpperInvariant();
            var nameAttribute = elementName + ".NAME";
            var states = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var element in document.Root?.Elements(elementName) ?? [])
            {
                var name = (string?)element.Attribute(nameAttribute);
                if (name != null)
                    states[name] = (string?)element.Attribute("state") ?? "Unknown";
            }
            return states;
        }
    }
}

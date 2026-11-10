using Microsoft.Web.Administration;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;

namespace IIGO.Services
{
    /// <summary>
    /// Runs simple-acme (wacs.exe) to request a certificate for an IIS site and keeps its output so pages can show it as it arrives.
    /// One request runs at a time, because simple-acme shares its cache and settings between runs.
    /// </summary>
    internal sealed partial class CertificateIssuanceService(IServiceScopeFactory scopeFactory, ILogger<CertificateIssuanceService> logger) : IDisposable
    {
        // A request that takes longer than this is stopped
        static readonly TimeSpan Timeout = TimeSpan.FromMinutes(20);

        readonly Lock sync = new();

        /// <summary>
        /// The running request, or the last one to finish
        /// </summary>
        public IssuanceRun? Current { get; private set; }

        /// <summary>
        /// Raised on a background thread when a request starts, writes output, or finishes
        /// </summary>
        public event Action? Changed;

        /// <summary>
        /// The host names on a site's http and https bindings, with the reason any of them can't go on a certificate
        /// </summary>
        public static List<HostOption> GetHostOptions(Site site)
        {
            return [.. site.Bindings
                .Where(x => x.Protocol is "http" or "https")
                .Where(x => !string.IsNullOrWhiteSpace(x.Host))
                .GroupBy(x => x.Host.Trim().ToLowerInvariant())
                .Select(g => new HostOption(g.Key, [.. g.Select(b => $"{b.Protocol} {b.BindingInformation}")], IneligibleReason(g.Key)))
                .OrderBy(x => x.Reason != null)
                .ThenBy(x => x.Host)];
        }

        static string? IneligibleReason(string host)
        {
            if (host.StartsWith('*'))
                return "Wildcards need DNS validation";
            if (IPAddress.TryParse(host, out _))
                return "IP addresses can't be validated";
            if (!HostNameRegex().IsMatch(host) || !host.Contains('.'))
                return "Not a public host name";
            return null;
        }

        /// <summary>
        /// Starts a request. Throws InvalidOperationException with a message for the user when it can't start.
        /// </summary>
        public IssuanceRun Start(long siteId, IReadOnlyCollection<string> hosts, string commonName, bool verbose, string actor)
        {
            Dictionary<string, string> settings;
            Site site;
            using (var scope = scopeFactory.CreateScope())
            {
                settings = CertificateIssuanceSettings.Load(scope.ServiceProvider.GetRequiredService<ConfigurationService>());
                site = scope.ServiceProvider.GetRequiredService<IISService>().GetSite(siteId)
                    ?? throw new InvalidOperationException("The website was not found.");
            }

            if (CertificateIssuanceSettings.Problem(settings) is string problem)
                throw new InvalidOperationException($"{problem} Check the Certificates settings on the Settings page.");
            var wacsPath = settings[CertificateIssuanceSettings.WacsPath].Trim();
            var baseUri = settings[CertificateIssuanceSettings.BaseUri].Trim();
            var customArguments = CertificateIssuanceSettings.SplitArguments(settings[CertificateIssuanceSettings.CustomArguments]);

            // Only names that are on the site's bindings now, whatever the page sent
            var allowed = GetHostOptions(site).Where(x => x.Reason == null).Select(x => x.Host).ToHashSet();
            var selected = hosts.Select(x => x.Trim().ToLowerInvariant()).Distinct().ToList();
            if (selected.Count == 0)
                throw new InvalidOperationException("Select at least one host name.");
            var unknown = selected.Where(x => !allowed.Contains(x)).ToList();
            if (unknown.Count > 0)
                throw new InvalidOperationException($"These host names aren't on the site's bindings: {string.Join(", ", unknown)}");
            commonName = commonName.Trim().ToLowerInvariant();
            if (!selected.Contains(commonName))
                commonName = selected[0];

            var startInfo = new ProcessStartInfo(wacsPath)
            {
                WorkingDirectory = Path.GetDirectoryName(wacsPath)!,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            // ArgumentList quotes each value, so nothing from the page can add arguments. These answer
            // every question simple-acme would ask, so it runs unattended. The renewal is still saved, and
            // the scheduled task set up with simple-acme renews it.
            // Validation and the certificate store come from simple-acme's own settings.json unless the extra arguments set them.
            var args = new List<string>
            {
                "--source", "iis",
                "--siteid", site.Id.ToString(),
                "--host", string.Join(",", selected),
                "--commonname", commonName,
                "--installation", "iis",
                "--accepttos",
                "--emailaddress", settings[CertificateIssuanceSettings.AccountEmail].Trim(),
                "--notaskscheduler"
            };
            if (baseUri != "")
                args.AddRange(["--baseuri", baseUri]);
            if (verbose)
                args.Add("--verbose");
            args.ForEach(startInfo.ArgumentList.Add);
            // Set by administrators on the Settings page. Not shown in the output, since they can hold keys.
            customArguments.ForEach(startInfo.ArgumentList.Add);
            var commandLine = $"wacs.exe {string.Join(" ", args.Select(QuoteForDisplay))}{(customArguments.Count > 0 ? " [extra arguments]" : "")}";

            IssuanceRun run;
            lock (sync)
            {
                if (Current?.IsRunning == true)
                    throw new InvalidOperationException($"A certificate request for {Current.SiteName} is already running. Wait for it to finish.");

                run = new IssuanceRun(site.Id, site.Name, selected, commonName, actor, commandLine);
                Current = run;
            }

            logger.LogInformation("{Actor} requested a certificate for site {SiteId} ({SiteName}) with host names {Hosts}", actor, site.Id, site.Name, string.Join(", ", selected));
            Task.Run(() => RunAsync(run, startInfo));
            Changed?.Invoke();
            return run;
        }

        /// <summary>
        /// Stops the running request, if there is one
        /// </summary>
        public void Cancel(string actor)
        {
            var run = Current;
            if (run?.IsRunning != true)
                return;

            logger.LogInformation("{Actor} cancelled the certificate request for site {SiteId}", actor, run.SiteId);
            run.Append($"Cancelled by {actor}.", true);
            run.Cancellation.Cancel();
        }

        async Task RunAsync(IssuanceRun run, ProcessStartInfo startInfo)
        {
            run.Append($"> {run.CommandLine}", false);
            Changed?.Invoke();

            using var process = new Process { StartInfo = startInfo };
            process.OutputDataReceived += (_, e) => OnOutput(run, e.Data, false);
            process.ErrorDataReceived += (_, e) => OnOutput(run, e.Data, true);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(run.Cancellation.Token);
            timeout.CancelAfter(Timeout);

            try
            {
                process.Start();
                // simple-acme has every answer it needs on the command line. Closing input makes any
                // unexpected question fail straight away instead of waiting forever.
                process.StandardInput.Close();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                try
                {
                    await process.WaitForExitAsync(timeout.Token);
                    // Lets the last of the output arrive
                    process.WaitForExit();
                }
                catch (OperationCanceledException)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync();
                    if (!run.Cancellation.IsCancellationRequested)
                        run.Append($"Stopped after {Timeout.TotalMinutes:0} minutes.", true);
                }

                run.Finish(run.Cancellation.IsCancellationRequested ? IssuanceState.Cancelled
                    : timeout.IsCancellationRequested ? IssuanceState.Failed
                    : process.ExitCode == 0 ? IssuanceState.Succeeded
                    : IssuanceState.Failed, process.ExitCode);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error running wacs.exe for site {SiteId}", run.SiteId);
                run.Append($"Could not run wacs.exe: {ex.Message}", true);
                run.Finish(IssuanceState.Failed, null);
            }

            if (run.State == IssuanceState.Succeeded)
                logger.LogInformation("Certificate request for site {SiteId} ({SiteName}) finished", run.SiteId, run.SiteName);
            else
                logger.LogWarning("Certificate request for site {SiteId} ({SiteName}) ended as {State} with exit code {ExitCode}", run.SiteId, run.SiteName, run.State, run.ExitCode);

            Changed?.Invoke();
        }

        static string QuoteForDisplay(string arg) => arg.Any(char.IsWhiteSpace) ? $"\"{arg}\"" : arg;

        void OnOutput(IssuanceRun run, string? line, bool error)
        {
            if (line == null)
                return;
            run.Append(AnsiEscapeRegex().Replace(line, ""), error);
            Changed?.Invoke();
        }

        public void Dispose()
        {
            if (Current?.IsRunning == true)
                Current.Cancellation.Cancel();
        }

        [GeneratedRegex(@"\x1B\[[0-9;?]*[ -/]*[@-~]")]
        private static partial Regex AnsiEscapeRegex();

        [GeneratedRegex(@"^([a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?\.)*[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?$")]
        private static partial Regex HostNameRegex();
    }

    public record HostOption(string Host, IReadOnlyList<string> Bindings, string? Reason);

    public enum IssuanceState
    {
        Running,
        Succeeded,
        Failed,
        Cancelled
    }

    public record OutputLine(string Text, bool IsError);

    /// <summary>
    /// One run of wacs.exe and the output it has written so far
    /// </summary>
    public sealed class IssuanceRun(long siteId, string siteName, IReadOnlyList<string> hosts, string commonName, string startedBy, string commandLine)
    {
        // Output past this many lines is dropped from the start
        const int MaxLines = 5000;

        readonly Lock sync = new();
        readonly List<OutputLine> lines = [];

        public long SiteId { get; } = siteId;
        public string SiteName { get; } = siteName;
        public IReadOnlyList<string> Hosts { get; } = hosts;
        public string CommonName { get; } = commonName;
        public string StartedBy { get; } = startedBy;
        public string CommandLine { get; } = commandLine;
        public DateTime StartedAt { get; } = DateTime.Now;
        public DateTime? FinishedAt { get; private set; }
        public IssuanceState State { get; private set; } = IssuanceState.Running;
        public int? ExitCode { get; private set; }
        public bool IsRunning => State == IssuanceState.Running;

        internal CancellationTokenSource Cancellation { get; } = new();

        public OutputLine[] GetLines()
        {
            lock (sync)
                return [.. lines];
        }

        internal void Append(string text, bool isError)
        {
            lock (sync)
            {
                lines.Add(new OutputLine(text, isError));
                if (lines.Count > MaxLines)
                    lines.RemoveRange(0, lines.Count - MaxLines);
            }
        }

        internal void Finish(IssuanceState state, int? exitCode)
        {
            ExitCode = exitCode;
            FinishedAt = DateTime.Now;
            State = state;
        }
    }
}

using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.ServiceProcess;

namespace IIGO.Config
{
    internal sealed record ServiceResult(bool Success, string Message);

    /// <summary>
    /// Starts and stops the IIGOPanel service, and checks that it is listening afterwards
    /// </summary>
    internal static class PanelService
    {
        // Matches ServiceInstall in IIGO.Installer\Package.wxs and UseWindowsService in IIGO\Program.cs
        public const string ServiceName = "IIGOPanel";
        private const string EventLogName = "IIGO";

        private static readonly TimeSpan StatusTimeout = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan ListenTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Null when the service isn't installed
        /// </summary>
        public static ServiceControllerStatus? GetStatus()
        {
            try
            {
                using var service = new ServiceController(ServiceName);
                return service.Status;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        public static Task StopAsync() => Task.Run(Stop);

        /// <summary>
        /// Starts the service and waits until it answers on the endpoint's port. Returns what happened, for the user.
        /// </summary>
        public static Task<ServiceResult> StartAsync(HttpsEndpoint? endpoint) => Task.Run(() => Start(endpoint));

        public static Task<ServiceResult> RestartAsync(HttpsEndpoint? endpoint) => Task.Run(() =>
        {
            Stop();

            // The panel has let go of its port now, so anything still listening there is another program
            if (endpoint != null && IsPortInUse(endpoint.Port))
                return new(false, $"Port {endpoint.Port} is in use by another program, so the panel can't listen on it. " +
                    "The service was left stopped. Pick another port, save, and start the service.");

            return Start(endpoint);
        });

        private static void Stop()
        {
            using var service = new ServiceController(ServiceName);
            if (service.Status == ServiceControllerStatus.Stopped)
                return;

            if (service.Status != ServiceControllerStatus.StopPending)
                service.Stop();

            service.WaitForStatus(ServiceControllerStatus.Stopped, StatusTimeout);
        }

        private static ServiceResult Start(HttpsEndpoint? endpoint)
        {
            using var service = new ServiceController(ServiceName);
            var startedAt = DateTime.Now;

            if (service.Status != ServiceControllerStatus.Running)
            {
                try
                {
                    if (service.Status != ServiceControllerStatus.StartPending)
                        service.Start();
                    service.WaitForStatus(ServiceControllerStatus.Running, StatusTimeout);
                }
                catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or System.ServiceProcess.TimeoutException)
                {
                    return new(false, "The service did not start. " + (LastError(startedAt) ?? ex.Message));
                }
            }

            if (endpoint == null)
                return new(true, "The service is running.");

            // The service reports Running before Kestrel opens its endpoints. A bad certificate or a taken port
            // makes it stop again a moment later, so wait until the port answers or the service stops.
            var watch = Stopwatch.StartNew();
            while (watch.Elapsed < ListenTimeout)
            {
                service.Refresh();
                if (service.Status is ServiceControllerStatus.Stopped or ServiceControllerStatus.StopPending)
                    return StoppedAgain(startedAt);

                if (CanConnect(endpoint))
                {
                    // Give a failing start a moment to show itself before calling it a success
                    Thread.Sleep(2000);
                    service.Refresh();
                    if (service.Status != ServiceControllerStatus.Running)
                        return StoppedAgain(startedAt);

                    return new(true, $"The service is running and listening on {endpoint.Url}.");
                }

                Thread.Sleep(500);
            }

            return new(false, $"The service is running, but nothing answered on port {endpoint.Port} after {ListenTimeout.TotalSeconds:0} seconds. " +
                "Check the IIGO event log.");
        }

        private static ServiceResult StoppedAgain(DateTime startedAt) =>
            new(false, "The service started but stopped again. " + (LastError(startedAt) ?? "See the IIGO event log for the reason."));

        private static bool IsPortInUse(int port) =>
            IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(listener => listener.Port == port);

        private static bool CanConnect(HttpsEndpoint endpoint)
        {
            // A specific address only listens there; anything else listens on every address, loopback included
            var host = endpoint.Host.Trim().Trim('[', ']');
            var address = IPAddress.TryParse(host, out var parsed)
                ? parsed.Equals(IPAddress.Any) ? IPAddress.Loopback
                : parsed.Equals(IPAddress.IPv6Any) ? IPAddress.IPv6Loopback
                : parsed
                : IPAddress.Loopback;

            try
            {
                using var client = new TcpClient(address.AddressFamily);
                var connect = client.ConnectAsync(address, endpoint.Port);
                return connect.Wait(TimeSpan.FromSeconds(1)) && client.Connected;
            }
            catch (Exception ex) when (ex is SocketException or AggregateException)
            {
                return false;
            }
        }

        /// <summary>
        /// The newest error the panel wrote to its event log since the start, such as Kestrel failing to load the certificate
        /// </summary>
        private static string? LastError(DateTime since)
        {
            try
            {
                if (!EventLog.Exists(EventLogName))
                    return null;

                using var log = new EventLog(EventLogName);
                for (var i = log.Entries.Count - 1; i >= 0; i--)
                {
                    var entry = log.Entries[i];
                    if (entry.TimeGenerated < since.AddSeconds(-1))
                        break;
                    if (entry.EntryType == EventLogEntryType.Error)
                        return entry.Message;
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or ArgumentException)
            {
            }

            return null;
        }
    }
}

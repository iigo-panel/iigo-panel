using System.Diagnostics;

namespace IIGO.Services
{
    /// <summary>
    /// One reading of the dashboard counters. A value is null when its counter is not available.
    /// </summary>
    /// <param name="Timestamp">Unix time in milliseconds.</param>
    public sealed record MetricSample(
        long Timestamp,
        double? Cpu,
        double? MemoryPercent,
        long MemoryUsedMiB,
        long MemoryTotalMiB,
        double? NetworkReceived,
        double? NetworkSent);

    /// <summary>
    /// Reads CPU, memory and network counters every 2 seconds for the lifetime of the application and keeps the
    /// last 2 minutes, so the dashboard charts start full instead of empty. One set of counters serves every viewer.
    /// </summary>
    public class SystemMetricsService(ILogger<SystemMetricsService> logger) : BackgroundService
    {
        public static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);
        public const int HistoryLength = 60;

        private readonly SystemStatusService status = new();
        private readonly Queue<MetricSample> history = new();
        private readonly Lock historyLock = new();
        private readonly TaskCompletionSource initialized = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<(PerformanceCounter Received, PerformanceCounter Sent)> networkCounters = [];
        private PerformanceCounter? cpuCounter;

        public bool CpuAvailable { get; private set; }
        public bool MemoryAvailable { get; private set; }
        public bool NetworkAvailable { get; private set; }

        /// <summary>Completes once the counters are set up and the availability flags are known.</summary>
        public Task Initialized => initialized.Task;

        /// <summary>Raised after each reading is added to the history. Handlers run on a background thread.</summary>
        public event Action<MetricSample>? Sampled;

        /// <summary>The readings from the last 2 minutes, oldest first.</summary>
        public IReadOnlyList<MetricSample> GetHistory()
        {
            lock (historyLock)
            {
                return [.. history];
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Setting up the counters can take a few seconds, so keep it off the startup path
            await Task.Yield();
            try
            {
                SetUpCounters();
            }
            finally
            {
                initialized.TrySetResult();
            }

            if (!CpuAvailable && !MemoryAvailable && !NetworkAvailable)
            {
                return;
            }

            using var timer = new PeriodicTimer(Interval);
            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    var sample = Sample();
                    lock (historyLock)
                    {
                        history.Enqueue(sample);
                        while (history.Count > HistoryLength)
                        {
                            history.Dequeue();
                        }
                    }

                    try
                    {
                        Sampled?.Invoke(sample);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "A dashboard metrics subscriber failed");
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void SetUpCounters()
        {
            try
            {
                cpuCounter = new PerformanceCounter("Processor Information", "% Processor Utility", "_Total");
                cpuCounter.NextValue(); // the first read is always 0
                CpuAvailable = true;
            }
            catch (Exception ex)
            {
                cpuCounter?.Dispose();
                cpuCounter = null;
                logger.LogWarning(ex, "The CPU counter is not available");
            }

            try
            {
                foreach (var instance in new PerformanceCounterCategory("Network Interface").GetInstanceNames())
                {
                    var received = new PerformanceCounter("Network Interface", "Bytes Received/sec", instance);
                    var sent = new PerformanceCounter("Network Interface", "Bytes Sent/sec", instance);
                    received.NextValue();
                    sent.NextValue();
                    networkCounters.Add((received, sent));
                }
                NetworkAvailable = networkCounters.Count > 0;
            }
            catch (Exception ex)
            {
                DisposeNetworkCounters();
                logger.LogWarning(ex, "The network counters are not available");
            }

            try
            {
                MemoryAvailable = status.GetTotalMemoryInMiB(true) > 0;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Memory information is not available");
            }
        }

        // Reads every available counter once. A counter that throws is switched off for the rest of the run.
        private MetricSample Sample()
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            double? cpu = null;
            if (CpuAvailable && cpuCounter != null)
            {
                try
                {
                    cpu = Math.Clamp(cpuCounter.NextValue(), 0f, 100f);
                }
                catch (Exception ex)
                {
                    CpuAvailable = false;
                    logger.LogWarning(ex, "The CPU counter stopped working");
                }
            }

            double? memoryPercent = null;
            long used = 0;
            long total = 0;
            if (MemoryAvailable)
            {
                try
                {
                    total = status.GetTotalMemoryInMiB(true);
                    var available = status.GetPhysicalAvailableMemoryInMiB();
                    if (total > 0 && available >= 0)
                    {
                        used = total - available;
                        memoryPercent = (1d - (double)available / total) * 100d;
                    }
                }
                catch (Exception ex)
                {
                    MemoryAvailable = false;
                    logger.LogWarning(ex, "Memory information stopped working");
                }
            }

            double? received = null;
            double? sent = null;
            if (NetworkAvailable)
            {
                try
                {
                    double r = 0;
                    double s = 0;
                    foreach (var (rc, sc) in networkCounters)
                    {
                        r += rc.NextValue();
                        s += sc.NextValue();
                    }
                    received = r;
                    sent = s;
                }
                catch (Exception ex)
                {
                    NetworkAvailable = false;
                    logger.LogWarning(ex, "The network counters stopped working");
                }
            }

            return new MetricSample(now, cpu, memoryPercent, used, total, received, sent);
        }

        private void DisposeNetworkCounters()
        {
            foreach (var (received, sent) in networkCounters)
            {
                received.Dispose();
                sent.Dispose();
            }
            networkCounters.Clear();
        }

        public override void Dispose()
        {
            base.Dispose();
            cpuCounter?.Dispose();
            DisposeNetworkCounters();
            GC.SuppressFinalize(this);
        }
    }
}

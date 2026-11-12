using IIGO.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Web.Administration;
using System.ComponentModel.DataAnnotations;

namespace IIGO.Api
{
    /// <summary>
    /// IIS application pools: the same actions as the App Pools and application pool pages.
    /// </summary>
    [Route("api/apppools")]
    public class AppPoolsController(ILogger<AppPoolsController> logger, IISService IISService) : ApiControllerBase(logger)
    {
        [HttpGet]
        public ActionResult<List<AppPoolSummary>> GetAppPools()
        {
            try
            {
                var counts = IISService.GetApplicationCountsByPool();
                return IISService.GetAppPools().Select(pool => new AppPoolSummary(
                    pool.Name,
                    pool.State.ToString(),
                    RuntimeText(pool.ManagedRuntimeVersion),
                    pool.ManagedPipelineMode.ToString(),
                    pool.AutoStart,
                    pool.ProcessModel.IdentityType.ToString(),
                    counts.GetValueOrDefault(pool.Name, 0))).ToList();
            }
            catch (Exception ex)
            {
                return Failed(ex, "loading application pools");
            }
        }

        [HttpGet("{name}")]
        public ActionResult<AppPoolDetail> GetAppPool(string name)
        {
            try
            {
                var pool = IISService.GetAppPool(name);
                return pool == null ? NotFoundProblem($"Application pool {name} was not found") : ToDetail(pool);
            }
            catch (Exception ex)
            {
                return Failed(ex, $"loading application pool {name}");
            }
        }

        [HttpGet("{name}/applications")]
        public ActionResult<List<AppPoolApplication>> GetApplications(string name)
        {
            if (IISService.GetAppPool(name) == null)
                return NotFoundProblem($"Application pool {name} was not found");

            return IISService.GetApplicationsByPool(name).Select(x => new AppPoolApplication(x.SiteName, x.VirtualPath, x.PhysicalPath)).ToList();
        }

        /// <summary>
        /// Adds a pool the way IIS Manager's Add Application Pool dialog does. The identity is ApplicationPoolIdentity.
        /// </summary>
        [HttpPost]
        public ActionResult<AppPoolDetail> AddAppPool([FromBody] AddAppPoolRequest request)
        {
            var name = request.Name.Trim();
            try
            {
                IISService.AddAppPool(name, request.ManagedRuntimeVersion ?? "", request.PipelineMode, request.AutoStart);
                Logger.LogInformation("API key {KeyName} added application pool {PoolName}", KeyName, name);

                var pool = IISService.GetAppPool(name);
                return pool == null
                    ? StatusCode(StatusCodes.Status201Created)
                    : CreatedAtAction(nameof(GetAppPool), new { name }, ToDetail(pool));
            }
            catch (Exception ex)
            {
                return Failed(ex, $"adding application pool {name}");
            }
        }

        /// <summary>Deletes a pool. Pools that still have applications are refused with 409.</summary>
        [HttpDelete("{name}")]
        public IActionResult DeleteAppPool(string name)
        {
            try
            {
                IISService.DeleteAppPool(name);
                Logger.LogWarning("API key {KeyName} deleted application pool {PoolName}", KeyName, name);
                return NoContent();
            }
            catch (Exception ex)
            {
                return Failed(ex, $"deleting application pool {name}");
            }
        }

        [HttpPost("{name}/start")]
        public IActionResult StartAppPool(string name) => ChangeState(name, "started", IISService.StartPool);

        [HttpPost("{name}/stop")]
        public IActionResult StopAppPool(string name) => ChangeState(name, "stopped", IISService.StopPool);

        [HttpPost("{name}/recycle")]
        public IActionResult RecycleAppPool(string name) => ChangeState(name, "recycled", IISService.Recycle);

        private IActionResult ChangeState(string name, string done, Action<string> action)
        {
            var pool = IISService.GetAppPool(name);
            if (pool == null)
                return NotFoundProblem($"Application pool {name} was not found");

            try
            {
                action(pool.Name);
                Logger.LogInformation("API key {KeyName} {Action} application pool {PoolName}", KeyName, done, pool.Name);
                return NoContent();
            }
            catch (Exception ex)
            {
                return Failed(ex, $"changing the state of application pool {name}");
            }
        }

        private AppPoolDetail ToDetail(ApplicationPool pool)
        {
            List<string> schedule = [];
            try
            {
                schedule = [.. pool.Recycling.PeriodicRestart.Schedule.Select(x => x.Time).OrderBy(x => x).Select(x => x.ToString(@"hh\:mm"))];
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading recycle schedule for application pool {Name}", pool.Name);
            }

            List<WorkerProcessInfo> workers = [];
            try
            {
                workers = [.. pool.WorkerProcesses.Select(x => new WorkerProcessInfo(x.ProcessId, x.State.ToString()))];
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading worker processes for application pool {Name}", pool.Name);
            }

            var process = pool.ProcessModel;
            var recycling = pool.Recycling;
            return new AppPoolDetail(
                pool.Name,
                pool.State.ToString(),
                RuntimeText(pool.ManagedRuntimeVersion),
                pool.ManagedPipelineMode.ToString(),
                pool.Enable32BitAppOnWin64,
                pool.StartMode.ToString(),
                pool.AutoStart,
                IISService.GetApplicationCountsByPool().GetValueOrDefault(pool.Name, 0),
                new ProcessModelInfo(
                    process.IdentityType.ToString(),
                    process.IdentityType == ProcessModelIdentityType.SpecificUser ? process.UserName : null,
                    process.LoadUserProfile,
                    process.IdleTimeout,
                    process.IdleTimeoutAction.ToString(),
                    process.MaxProcesses,
                    process.PingingEnabled,
                    process.PingInterval,
                    process.PingResponseTime,
                    process.StartupTimeLimit,
                    process.ShutdownTimeLimit),
                new RecyclingInfo(
                    recycling.PeriodicRestart.Time,
                    schedule,
                    recycling.PeriodicRestart.Requests,
                    recycling.PeriodicRestart.Memory,
                    recycling.PeriodicRestart.PrivateMemory,
                    recycling.DisallowOverlappingRotation,
                    recycling.DisallowRotationOnConfigChange),
                new CpuInfo(pool.Cpu.Limit, pool.Cpu.Action.ToString(), pool.Cpu.ResetInterval),
                workers);
        }

        // "" is shown as "No Managed Code" in IIS Manager
        private static string RuntimeText(string version) => string.IsNullOrEmpty(version) ? "No Managed Code" : version;

        public record AppPoolSummary(string Name, string State, string ManagedRuntimeVersion, string ManagedPipelineMode, bool AutoStart, string IdentityType, int ApplicationCount);

        public record AppPoolDetail(
            string Name,
            string State,
            string ManagedRuntimeVersion,
            string ManagedPipelineMode,
            bool Enable32BitAppOnWin64,
            string StartMode,
            bool AutoStart,
            int ApplicationCount,
            ProcessModelInfo ProcessModel,
            RecyclingInfo Recycling,
            CpuInfo Cpu,
            List<WorkerProcessInfo> WorkerProcesses);

        // Time spans are "hh:mm:ss"; zero means disabled. UserName is only set for SpecificUser identities.
        public record ProcessModelInfo(
            string IdentityType,
            string? UserName,
            bool LoadUserProfile,
            TimeSpan IdleTimeout,
            string IdleTimeoutAction,
            long MaxProcesses,
            bool PingingEnabled,
            TimeSpan PingInterval,
            TimeSpan PingResponseTime,
            TimeSpan StartupTimeLimit,
            TimeSpan ShutdownTimeLimit);

        // Limits of 0 mean no limit; memory limits are in KB. SpecificTimes are "hh:mm".
        public record RecyclingInfo(
            TimeSpan RegularTimeInterval,
            List<string> SpecificTimes,
            long RequestLimit,
            long VirtualMemoryLimit,
            long PrivateMemoryLimit,
            bool DisableOverlappedRecycle,
            bool DisableRecyclingOnConfigChange);

        // Limit is in 1/1000ths of a percent (1000 = 1%); 0 means no limit
        public record CpuInfo(long Limit, string Action, TimeSpan ResetInterval);

        public record WorkerProcessInfo(int ProcessId, string State);

        public record AppPoolApplication(string SiteName, string VirtualPath, string PhysicalPath);

        public class AddAppPoolRequest
        {
            [Required]
            public string Name { get; set; } = "";

            // "v4.0", "v2.0", or "" for No Managed Code
            public string? ManagedRuntimeVersion { get; set; } = "v4.0";

            public ManagedPipelineMode PipelineMode { get; set; } = ManagedPipelineMode.Integrated;

            // Start the pool now and when IIS starts
            public bool AutoStart { get; set; } = true;
        }
    }
}

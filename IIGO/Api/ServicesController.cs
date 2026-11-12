using IIGO.Services;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;

namespace IIGO.Api
{
    /// <summary>
    /// Windows services: view, start and restart. Stopping, adding, changing and deleting services are only on the Services page.
    /// </summary>
    [Route("api/services")]
    public class ServicesController(ILogger<ServicesController> logger, WindowsServiceManager serviceManager) : ApiControllerBase(logger)
    {
        /// <summary>Lists services. Every word in <paramref name="search"/> has to match somewhere in the service, like the page's filter.</summary>
        [HttpGet]
        public async Task<ActionResult<List<ServiceDto>>> GetServices([FromQuery] string? search = null)
        {
            try
            {
                var terms = (search ?? "").ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var services = await Task.Run(serviceManager.GetServices);
                return services.Where(x => terms.All(x.SearchText.Contains)).Select(ServiceDto.From).ToList();
            }
            catch (Exception ex)
            {
                return Failed(ex, "loading services");
            }
        }

        /// <summary>Gets a service by its service name (not its display name).</summary>
        [HttpGet("{name}")]
        public ActionResult<ServiceDto> GetService(string name)
        {
            try
            {
                var service = serviceManager.GetService(name);
                return service == null ? NotFoundProblem($"Service {name} was not found") : ServiceDto.From(service);
            }
            catch (Exception ex)
            {
                return Failed(ex, $"loading service {name}");
            }
        }

        /// <summary>Starts the service and waits up to 30 seconds for it to be running. A running service is returned unchanged.</summary>
        [HttpPost("{name}/start")]
        public async Task<ActionResult<ServiceDto>> StartService(string name)
        {
            var service = serviceManager.GetService(name);
            if (service == null)
                return NotFoundProblem($"Service {name} was not found");
            if (service.IsRunning)
                return ServiceDto.From(service);

            Logger.LogWarning("API key {KeyName} is starting service {ServiceName}", KeyName, service.Name);
            return await Run(service, "start", () => serviceManager.StartServiceAsync(service.Name));
        }

        /// <summary>
        /// Stops the service and the services that depend on it, then starts it and the dependents that were running.
        /// The panel's own service can't be restarted here.
        /// </summary>
        [HttpPost("{name}/restart")]
        public async Task<ActionResult<ServiceDto>> RestartService(string name)
        {
            var service = serviceManager.GetService(name);
            if (service == null)
                return NotFoundProblem($"Service {name} was not found");
            if (service.IsSelf)
                return ConflictProblem("The panel's own service can't be restarted through the panel");

            Logger.LogWarning("API key {KeyName} is restarting service {ServiceName}", KeyName, service.Name);
            return await Run(service, "restart", () => serviceManager.RestartServiceAsync(service.Name));
        }

        private async Task<ActionResult<ServiceDto>> Run(WindowsServiceInfo service, string action, Func<Task> work)
        {
            try
            {
                await work();
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or System.ServiceProcess.TimeoutException)
            {
                // Windows refused or the service didn't reach the new state in time. Same message as the page shows.
                Logger.LogError(ex, "API error running {Action} on service {ServiceName}", action, service.Name);
                return ConflictProblem($"Could not {action} {service.DisplayName}: {ex.InnerException?.Message ?? ex.Message}");
            }
            catch (Exception ex)
            {
                return Failed(ex, $"running {action} on service {service.Name}");
            }

            var current = serviceManager.GetService(service.Name);
            return current == null ? NoContent() : ServiceDto.From(current);
        }

        public record ServiceDto(
            string Name,
            string DisplayName,
            string Description,
            string Status,
            string StartMode,
            bool DelayedStart,
            string Startup,
            string LogOnAs,
            string PathName,
            bool CanStop,
            bool IsSelf)
        {
            public static ServiceDto From(WindowsServiceInfo info) => new(
                info.Name,
                info.DisplayName,
                info.Description,
                info.Status.ToString(),
                info.StartMode.ToString(),
                info.DelayedStart,
                info.StartupText,
                info.LogOnText,
                info.PathName,
                info.CanStop,
                info.IsSelf);
        }
    }
}

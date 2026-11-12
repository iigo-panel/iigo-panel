using IIGO.Services;
using Microsoft.AspNetCore.Mvc;

namespace IIGO.Api
{
    /// <summary>
    /// Available Windows updates. Read-only: updates can only be installed from the Check Updates page.
    /// </summary>
    [Route("api/updates")]
    public class UpdatesController(ILogger<UpdatesController> logger, WindowsUpdateService updateService) : ApiControllerBase(logger)
    {
        /// <summary>
        /// The result of the last search. Searches run weekly in the background; Updates is null until the first one finishes.
        /// </summary>
        [HttpGet]
        public ActionResult<UpdateStatus> GetUpdates() => CurrentStatus();

        /// <summary>
        /// Starts a search, or joins the one already running, and returns at once with 202 Accepted.
        /// Poll GET /api/updates until IsChecking is false.
        /// </summary>
        [HttpPost("check")]
        public ActionResult<UpdateStatus> CheckForUpdates()
        {
            Logger.LogInformation("API key {KeyName} started a Windows Update search", KeyName);
            _ = updateService.CheckNowAsync();
            return Accepted(CurrentStatus());
        }

        private UpdateStatus CurrentStatus() => new(
            updateService.IsChecking,
            updateService.IsInstalling,
            updateService.LastChecked,
            updateService.LastError,
            updateService.Updates);

        // LastChecked is UTC
        public record UpdateStatus(bool IsChecking, bool IsInstalling, DateTime? LastChecked, string? LastError, IReadOnlyList<WindowsUpdateInfo>? Updates);
    }
}

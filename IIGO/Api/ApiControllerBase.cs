using IISManager;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IIGO.Api
{
    /// <summary>
    /// Shared setup for the API controllers. Every action needs a key from the API Keys page in the X-API-KEY header.
    /// </summary>
    // The fallback policy requires a signed-in user; the API uses keys instead, checked by RequireApiKey
    [AllowAnonymous]
    [ApiController]
    [RequireApiKey]
    public abstract class ApiControllerBase(ILogger logger) : ControllerBase
    {
        protected ILogger Logger { get; } = logger;

        /// <summary>Name of the API key that made the request, for logging.</summary>
        protected string KeyName => HttpContext.Items[RequireApiKeyAttribute.KeyNameItem] as string ?? "(unknown)";

        /// <summary>
        /// Turns an exception from IIS, the firewall or the service manager into a response. Validation and conflict
        /// errors carry their message, the same text the pages show. Anything else is logged and hidden.
        /// </summary>
        protected ObjectResult Failed(Exception ex, string action)
        {
            switch (ex)
            {
                case AppPoolNotFoundException:
                // IISWrapper's "Application /x not found" and "Binding ... not found"
                case InvalidOperationException when ex.InnerException is null && ex.Message.EndsWith("not found", StringComparison.OrdinalIgnoreCase):
                    return Problem(statusCode: StatusCodes.Status404NotFound, detail: ex.Message);
                case ArgumentException:
                case FormatException:
                    return Problem(statusCode: StatusCodes.Status400BadRequest, detail: ex.Message);
                case InvalidOperationException when ex.InnerException is null:
                    return Problem(statusCode: StatusCodes.Status409Conflict, detail: ex.Message);
                default:
                    Logger.LogError(ex, "API error: {Action}", action);
                    return Problem(statusCode: StatusCodes.Status500InternalServerError, detail: $"Error: {action}");
            }
        }

        protected ObjectResult NotFoundProblem(string detail) => Problem(statusCode: StatusCodes.Status404NotFound, detail: detail);

        protected ObjectResult ConflictProblem(string detail) => Problem(statusCode: StatusCodes.Status409Conflict, detail: detail);
    }
}

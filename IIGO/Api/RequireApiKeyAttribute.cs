using IIGO.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace IIGO.Api
{
    /// <summary>
    /// Requires a key from the API Keys page in the X-API-KEY header. Requests without an enabled,
    /// unexpired key get 401 Unauthorized before the action runs.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequireApiKeyAttribute : Attribute, IAsyncAuthorizationFilter
    {
        public const string HeaderName = "X-API-KEY";

        // HttpContext.Items key holding the name of the API key that made the request, for logging
        public const string KeyNameItem = "ApiKeyName";

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var apiKeyService = context.HttpContext.RequestServices.GetRequiredService<ApiKeyService>();
            var providedKey = context.HttpContext.Request.Headers[HeaderName].FirstOrDefault();

            var apiKey = await apiKeyService.ValidateAsync(providedKey);
            if (apiKey != null)
            {
                context.HttpContext.Items[KeyNameItem] = apiKey.Name;
            }
            else
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<RequireApiKeyAttribute>>();
                logger.LogWarning("Rejected API request to {Path} from {RemoteIp}: {Reason}",
                    context.HttpContext.Request.Path,
                    context.HttpContext.Connection.RemoteIpAddress,
                    string.IsNullOrWhiteSpace(providedKey) ? "no API key" : "invalid, disabled or expired API key");

                context.Result = new UnauthorizedResult();
            }
        }
    }
}

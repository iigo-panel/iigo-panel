using IIGO.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace IIGO.Api
{
    /// <summary>
    /// Requires a key from the API Keys page in the X-API-KEY header. Requests without an enabled,
    /// unexpired key get 401 Unauthorized before the action runs. Keys without access to the endpoint's
    /// <see cref="ApiAreaAttribute"/> area get 403 Forbidden.
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

                // The most specific area wins: class attributes come before action attributes in the metadata
                var area = context.ActionDescriptor.EndpointMetadata.OfType<ApiAreaAttribute>().LastOrDefault()?.Area;
                var method = context.HttpContext.Request.Method;
                var needed = context.ActionDescriptor.EndpointMetadata.OfType<ApiAccessLevelAttribute>().LastOrDefault()?.Access
                    ?? (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method)
                        ? ApiAccess.Read
                        : ApiAccess.Write);

                if (!ApiScopes.Allows(apiKey.Scopes, area, needed))
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<RequireApiKeyAttribute>>();
                    logger.LogWarning("API key {KeyName} was refused {Method} {Path}: it needs {Area} {Access} access",
                        apiKey.Name, method, context.HttpContext.Request.Path, area ?? "full", needed.ToString().ToLowerInvariant());

                    var detail = area == null
                        ? "This endpoint needs an API key with full access."
                        : $"This API key doesn't have {area}:{needed.ToString().ToLowerInvariant()} access.";
                    var problems = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
                    context.Result = new ObjectResult(problems.CreateProblemDetails(context.HttpContext, StatusCodes.Status403Forbidden, detail: detail))
                    {
                        StatusCode = StatusCodes.Status403Forbidden
                    };
                }
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

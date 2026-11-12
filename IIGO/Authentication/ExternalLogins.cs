using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;

namespace IIGO.Authentication
{
    /// <summary>
    /// Helpers for external login providers
    /// </summary>
    public static class ExternalLogins
    {
        /// <summary>Page that finishes an external sign-in</summary>
        public const string CallbackPath = "/Account/ExternalLogin";

        /// <summary>
        /// Sends provider errors, such as the user cancelling, to the panel's sign-in callback page, which shows them.
        /// Without this a failed sign-in ends on the error page. Call it from the provider's options:
        /// <c>AddOpenIdConnect("google", o => { ...; ExternalLogins.HandleRemoteFailures(o); })</c>.
        /// </summary>
        public static void HandleRemoteFailures(RemoteAuthenticationOptions options)
        {
            // Google, OpenID Connect and other OAuth providers create their own events; a plain remote handler may not
            options.Events ??= new RemoteAuthenticationEvents();
            var previous = options.Events.OnRemoteFailure;
            options.Events.OnRemoteFailure = async context =>
            {
                await previous(context);
                if (context.Result != null)
                    return;

                context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger(typeof(ExternalLogins))
                    .LogWarning(context.Failure, "External sign-in through {Scheme} failed", context.Scheme.Name);

                var message = context.Failure?.Message ?? "The sign-in was not completed.";
                context.Response.Redirect($"{context.Request.PathBase}{CallbackPath}?RemoteError={Uri.EscapeDataString(message)}");
                context.HandleResponse();
            };
        }

        /// <summary>
        /// The external provider the user signed in through in this session, or null for a panel password.
        /// Identity records it as the authentication method claim.
        /// </summary>
        public static string? GetLoginProvider(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.AuthenticationMethod);
    }
}

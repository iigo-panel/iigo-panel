namespace IIGO.Components.Account
{
    /// <summary>
    /// Sends a signed-in user whose password must be changed to the change password page,
    /// so a default or temporary password can't be used to work in the panel.
    /// </summary>
    internal sealed class MustChangePasswordMiddleware(RequestDelegate next)
    {
        public const string ChangePasswordPath = "/Account/Manage/ChangePassword";

        // Paths a flagged user still needs: the account pages (to change the password or sign out),
        // Blazor's own endpoints, and the API, which uses keys rather than the sign-in cookie
        static readonly PathString[] AllowedPrefixes =
        [
            "/Account",
            "/_blazor",
            "/_framework",
            "/_content",
            "/api"
        ];

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.User.Identity?.IsAuthenticated == true
                && context.User.HasClaim(ApplicationUserClaimsPrincipalFactory.MustChangePasswordClaim, "true")
                && !AllowedPrefixes.Any(prefix => context.Request.Path.StartsWithSegments(prefix)))
            {
                context.Response.Redirect(ChangePasswordPath);
                return;
            }

            await next(context);
        }
    }
}

using IIGO.Data;
using IIGO.Services;
using Microsoft.AspNetCore.Identity;

namespace IIGO.Components.Account
{
    /// <summary>
    /// Sends a signed-in user to the authenticator setup page when the Settings page requires 2FA
    /// for them and they haven't turned it on, so the panel can't be used with a password alone.
    /// </summary>
    internal sealed class RequireTwoFactorMiddleware(RequestDelegate next)
    {
        public const string EnableAuthenticatorPath = "/Account/Manage/EnableAuthenticator";

        public async Task InvokeAsync(HttpContext context, ConfigurationService configService, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
        {
            if (context.User.Identity?.IsAuthenticated == true
                && !context.User.HasClaim(ApplicationUserClaimsPrincipalFactory.TwoFactorEnabledClaim, "true")
                && !MustChangePasswordMiddleware.AllowedPrefixes.Any(prefix => context.Request.Path.StartsWithSegments(prefix))
                && configService.IsTwoFactorRequired(context.User))
            {
                // The claim is missing from sign-ins made before 2FA was turned on, so check the account before redirecting
                var user = await userManager.GetUserAsync(context.User);
                if (user?.TwoFactorEnabled == true)
                {
                    await signInManager.RefreshSignInAsync(user);
                }
                else
                {
                    context.Response.Redirect(EnableAuthenticatorPath);
                    return;
                }
            }

            await next(context);
        }
    }
}

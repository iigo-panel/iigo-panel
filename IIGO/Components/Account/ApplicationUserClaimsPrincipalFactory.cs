using IIGO.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace IIGO.Components.Account
{
    /// <summary>
    /// Adds claims for the user's password and 2FA state, so the checks can run on every request
    /// without a database lookup. The claims change when the sign-in is refreshed after the user changes them.
    /// </summary>
    internal sealed class ApplicationUserClaimsPrincipalFactory(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IOptions<IdentityOptions> options)
        : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>(userManager, roleManager, options)
    {
        public const string MustChangePasswordClaim = "iigo:must_change_password";
        public const string TwoFactorEnabledClaim = "iigo:two_factor_enabled";

        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
        {
            var identity = await base.GenerateClaimsAsync(user);
            if (user.MustChangePassword)
                identity.AddClaim(new Claim(MustChangePasswordClaim, "true"));
            if (user.TwoFactorEnabled)
                identity.AddClaim(new Claim(TwoFactorEnabledClaim, "true"));
            return identity;
        }
    }
}

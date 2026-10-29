using IIGO.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace IIGO.Components.Account
{
    /// <summary>
    /// Adds a claim when the user has to change their password, so the check can run on every
    /// request without a database lookup. The claim goes away when the sign-in is refreshed after the change.
    /// </summary>
    internal sealed class ApplicationUserClaimsPrincipalFactory(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IOptions<IdentityOptions> options)
        : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>(userManager, roleManager, options)
    {
        public const string MustChangePasswordClaim = "iigo:must_change_password";

        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
        {
            var identity = await base.GenerateClaimsAsync(user);
            if (user.MustChangePassword)
                identity.AddClaim(new Claim(MustChangePasswordClaim, "true"));
            return identity;
        }
    }
}

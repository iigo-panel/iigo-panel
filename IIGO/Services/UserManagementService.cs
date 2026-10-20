using IIGO.Data;
using Microsoft.AspNetCore.Identity;

namespace IIGO.Services
{
    public class UserManagementService(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
    {
        public const string AdministratorRole = "Administrator";

        static readonly IdentityError LastAdminError = new()
        {
            Code = "LastAdministrator",
            Description = "At least one active administrator is required. Add or enable another administrator first."
        };

        public async Task<IdentityResult> CreateUserAsync(ApplicationUser user, string password, IEnumerable<string> roles)
        {
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
                return result;

            var roleList = roles.ToList();
            if (roleList.Count > 0)
                result = await userManager.AddToRolesAsync(user, roleList);

            return result;
        }

        public async Task<IdentityResult> UpdateDetailsAsync(ApplicationUser user, string userName, string? email, bool emailConfirmed, string? phoneNumber)
        {
            var result = await ApplyDetailsAsync(user, userName, email, emailConfirmed, phoneNumber);
            if (!result.Succeeded)
            {
                // Failed validation leaves the rejected values on the tracked entity, so discard them
                await context.Entry(user).ReloadAsync();
            }
            return result;
        }

        async Task<IdentityResult> ApplyDetailsAsync(ApplicationUser user, string userName, string? email, bool emailConfirmed, string? phoneNumber)
        {
            if (user.UserName != userName)
            {
                var result = await userManager.SetUserNameAsync(user, userName);
                if (!result.Succeeded)
                    return result;
            }

            if (user.Email != email)
            {
                // SetEmailAsync clears EmailConfirmed, so the flag is applied afterwards
                var result = await userManager.SetEmailAsync(user, email);
                if (!result.Succeeded)
                    return result;
            }

            if (user.PhoneNumber != phoneNumber)
            {
                var result = await userManager.SetPhoneNumberAsync(user, phoneNumber);
                if (!result.Succeeded)
                    return result;
            }

            user.EmailConfirmed = emailConfirmed;
            return await userManager.UpdateAsync(user);
        }

        public async Task<IdentityResult> SetRolesAsync(ApplicationUser user, IEnumerable<string> roles)
        {
            var current = await userManager.GetRolesAsync(user);
            var selected = roles.ToList();

            if (current.Contains(AdministratorRole) && !selected.Contains(AdministratorRole) && await IsLastActiveAdminAsync(user))
                return IdentityResult.Failed(LastAdminError);

            var toRemove = current.Except(selected).ToList();
            if (toRemove.Count > 0)
            {
                var result = await userManager.RemoveFromRolesAsync(user, toRemove);
                if (!result.Succeeded)
                    return result;
            }

            var toAdd = selected.Except(current).ToList();
            if (toAdd.Count > 0)
                return await userManager.AddToRolesAsync(user, toAdd);

            return IdentityResult.Success;
        }

        public async Task<IdentityResult> DeleteUserAsync(ApplicationUser user)
        {
            if (await IsLastActiveAdminAsync(user))
                return IdentityResult.Failed(LastAdminError);

            return await userManager.DeleteAsync(user);
        }

        public async Task<IdentityResult> DisableMfaAsync(ApplicationUser user)
        {
            var result = await userManager.SetTwoFactorEnabledAsync(user, false);
            if (!result.Succeeded)
                return result;

            // Clear the old authenticator key and recovery codes so the lost device can't be used if MFA is re-enabled
            result = await userManager.ResetAuthenticatorKeyAsync(user);
            if (!result.Succeeded)
                return result;

            await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 0);
            return IdentityResult.Success;
        }

        public async Task<IdentityResult> ResetPasswordAsync(ApplicationUser user, string newPassword)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            return await userManager.ResetPasswordAsync(user, token, newPassword);
        }

        public async Task<bool> IsLastActiveAdminAsync(ApplicationUser user)
        {
            if (!await userManager.IsInRoleAsync(user, AdministratorRole) || IsLockedOut(user))
                return false;

            var admins = await userManager.GetUsersInRoleAsync(AdministratorRole);
            return !admins.Any(a => a.Id != user.Id && !IsLockedOut(a));
        }

        static bool IsLockedOut(ApplicationUser user) =>
            user.LockoutEnabled && user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;
    }
}

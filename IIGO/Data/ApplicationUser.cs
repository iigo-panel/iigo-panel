using Microsoft.AspNetCore.Identity;

namespace IIGO.Data
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
        /// <summary>
        /// True while the user is on a default or temporary password. The panel sends them to the
        /// change password page until they pick their own.
        /// </summary>
        public bool MustChangePassword { get; set; }
    }

}

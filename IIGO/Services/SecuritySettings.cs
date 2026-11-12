using IIGO.Models;
using System.Security.Claims;

namespace IIGO.Services
{
    /// <summary>
    /// Panel-wide sign-in settings, edited on the Settings page
    /// </summary>
    internal static class SecuritySettings
    {
        public const string RequireTwoFactor = "RequireTwoFactor";

        // RequireTwoFactor values. Stored in ConfigSetting, so they must not change.
        public const string TwoFactorOff = "Off";
        public const string TwoFactorAdministrators = "Administrators";
        public const string TwoFactorEveryone = "Everyone";

        public const string ExternalLoginSatisfiesTwoFactor = "ExternalLoginSatisfiesTwoFactor";

        public static readonly IReadOnlyList<MonitorSetting> Settings =
        [
            new(RequireTwoFactor, "Require two-factor authentication", MonitorSettingType.Choice, TwoFactorOff,
                "Users it applies to must set up an authenticator app before they can use the panel, and can't turn 2FA off.",
                [TwoFactorOff, TwoFactorAdministrators, TwoFactorEveryone]),
            new(ExternalLoginSatisfiesTwoFactor, "Single sign-on counts as two-factor", MonitorSettingType.Toggle, "false",
                "Users who sign in through an external provider, such as Google Workspace, skip the panel's authenticator app. " +
                "Turn this on only if the provider enforces its own two-factor authentication.")
        ];

        public static bool IsTwoFactorRequired(string setting, ClaimsPrincipal user) => setting switch
        {
            TwoFactorEveryone => true,
            TwoFactorAdministrators => user.IsInRole("Administrator"),
            _ => false
        };
    }
}

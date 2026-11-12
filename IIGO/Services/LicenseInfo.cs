namespace IIGO.Services
{
    /// <summary>
    /// The license as checked at startup. Registered as a singleton and handed to plugins.
    /// </summary>
    public sealed class LicenseInfo
    {
        public LicenseInfo(bool isValid, LicenseData data)
        {
            IsValid = isValid;
            Data = data;
        }

        public bool IsValid { get; }
        public LicenseData Data { get; }

        /// <summary>
        /// True when the license is valid, paid, and lists the entitlement (case does not matter)
        /// </summary>
        public bool HasEntitlement(string entitlement) =>
            IsValid
            && Data.LicenseType != LicenseType.Community
            && Data.Entitlements?.Contains(entitlement, StringComparer.OrdinalIgnoreCase) == true;

        internal static LicenseInfo Load()
        {
            var isValid = LicenseKeyService.ValidateLicense(out var data);
            return new LicenseInfo(isValid, data ?? new LicenseData());
        }
    }
}

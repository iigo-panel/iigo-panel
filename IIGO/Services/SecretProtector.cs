using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;

namespace IIGO.Services
{
    /// <summary>
    /// Encrypts secrets (SMTP passwords, API keys, webhook URLs) before they're stored in the ConfigSetting table,
    /// using the ASP.NET Core data protection keys. Protected values carry a prefix so plain text written
    /// straight into the database can still be recognised and encrypted on first use.
    /// </summary>
    public sealed class SecretProtector(IDataProtectionProvider provider, ILogger<SecretProtector> logger)
    {
        const string Prefix = "dp:";

        readonly IDataProtector _protector = provider.CreateProtector("IIGO.ConfigSetting.v1");

        public bool IsProtected(string? value) => value?.StartsWith(Prefix, StringComparison.Ordinal) == true;

        public string Protect(string plaintext) => Prefix + _protector.Protect(plaintext);

        /// <summary>
        /// Returns the plain text, or null when the value can't be decrypted (for example after the data protection keys were lost)
        /// </summary>
        public string? Unprotect(string value)
        {
            try
            {
                return _protector.Unprotect(value[Prefix.Length..]);
            }
            catch (CryptographicException ex)
            {
                logger.LogError(ex, "A stored secret could not be decrypted. Enter it again so it can be encrypted with the current keys.");
                return null;
            }
        }
    }
}

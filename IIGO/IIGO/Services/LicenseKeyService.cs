using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace IIGO.Services
{
    internal static class LicenseKeyService
    {
        static readonly string p = "-----BEGIN RSA PUBLIC KEY-----\nMIICCgKCAgEA15+X9GNxKknb5qYha1LjAKV4DuoHSBSIqtqCP6u9Nzz3t5/T8LBa\nXhHDZ2dEHD1rOjBo0WtEeCD8mz2oTsSm1TjUr75lWUXu7xUXjVraXPXdWVKqlbVv\nhYupBlrTLixxiCUJj+cAnSrtaDR1tChSVzcq8I8bWRWiSmf+5XjDeeIAI6p2jMpW\nzZhBrMRorgxY6cf51wniIMHSSOfs6mz30ThpRN9kHrmha+4IxpYOz3WoH2gJiDZo\nPGCEbmqEnokPPJ90Hq5zW/fx9eXpkKdCRIXa8J6v31yT/7pnPO/hPvn/64yArAiP\nhZaa6Af+HAkHk+3TumNKJFVgx0Et/VOf5lJEqzDDl8nWAU+Dv1gfX5GQvFJx0LZC\nkOEoJMpcxiXSiniV6dJJTMV+iyXqHPJWYVrJ7KJFQkeC+X2yixLOeiz+mPe1inJP\n1A6UkZNZ286XKpGoEe4APTruERz4+Iu8PBA558LoM4twM8AK26fmQsZx6NCE1RNJ\nMnw24o0IGb4FDVqNJ2qSqy14vWI8IGf+q9jSpQkIB2tSokbwy/CBc2J+3ANJXdpm\nVkRv1U1yG+q2Dw6XF18OD8swxR5r2MnHBvsZOh8fi2bYn8eKdsUsYNCofakNx5iv\nVnSMsYJCZvuB1nrGxoCVf0mv7RD7fmRnK/Rds6B1FT6EnsY9flkKbgECAwEAAQ==\n-----END RSA PUBLIC KEY-----";

        public static bool ValidateLicense(out LicenseData licenseData)
        {
            licenseData = new LicenseData();
            string path = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "Data", "license.lic");
            if (!File.Exists(path))
                return false;
            string license = File.ReadAllText(path);
            var rsaVerify = new RSACryptoServiceProvider(4096);
            rsaVerify.ImportFromPem(p);

            string[] data = license.Split('.');
            if (data.Length < 2)
                return false;

            try
            {
                var isValid = rsaVerify.VerifyData(Convert.FromBase64String(data[0]), SHA512.Create(), Convert.FromBase64String(data[1]));
                licenseData = JsonSerializer.Deserialize<LicenseData>(Encoding.UTF8.GetString(Convert.FromBase64String(data[0])));

                return isValid;
            }
            catch
            {
                return false;
            }
        }
    }

    internal record LicenseData
    {
        public string CompanyName { get; init; }
        public string LicenseType { get; init; }
        public string InitialVersion { get; init; }
        public string LicenseTerm { get; init; }
        public string LicenseDate { get; init; }
        public string[] Entitlements { get; init; }
    }
}

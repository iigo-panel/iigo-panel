using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace IIGO.Config
{
    /// <summary>
    /// A certificate in the list. Kestrel finds store certificates by subject, so the subject's common name is what gets saved.
    /// </summary>
    internal sealed class CertificateItem(X509Certificate2 certificate)
    {
        public X509Certificate2 Certificate { get; } = certificate;

        public string SubjectName { get; } = certificate.GetNameInfo(X509NameType.SimpleName, false);

        public override string ToString()
        {
            var expiry = Certificate.NotAfter < DateTime.Now ? "expired" : "expires";
            return $"{SubjectName}  ({expiry} {Certificate.NotAfter:yyyy-MM-dd}, {Certificate.Thumbprint[..8]})";
        }
    }

    internal static class CertificateStore
    {
        private const string ServerAuthenticationOid = "1.3.6.1.5.5.7.3.1";

        /// <summary>
        /// Certificates in a local machine store that Kestrel could serve: they have a private key and, if they list
        /// enhanced key usages, server authentication is one of them
        /// </summary>
        public static List<CertificateItem> List(string storeName)
        {
            using var store = new X509Store(storeName, StoreLocation.LocalMachine);
            try
            {
                store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
            }
            catch (CryptographicException)
            {
                // The store doesn't exist on this server, such as WebHosting before IIS is installed
                return [];
            }

            return [.. store.Certificates
                .Where(c => c.HasPrivateKey && IsAllowedForServerAuth(c))
                .OrderBy(c => c.GetNameInfo(X509NameType.SimpleName, false), StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(c => c.NotAfter)
                .Select(c => new CertificateItem(c))];
        }

        /// <summary>
        /// The certificate Kestrel will load for these settings, found the way Kestrel's CertificateLoader does: every
        /// certificate whose subject contains the name (valid ones only unless AllowInvalid is set), then the one that
        /// expires last. Null when Kestrel would find none and the panel would fail to start.
        /// </summary>
        public static X509Certificate2? FindKestrelChoice(string subject, string storeName, string storeLocation, bool allowInvalid)
        {
            if (!Enum.TryParse<StoreLocation>(storeLocation, true, out var location))
                return null;

            using var store = new X509Store(storeName, location);
            try
            {
                store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
            }
            catch (CryptographicException)
            {
                return null;
            }

            return store.Certificates
                .Find(X509FindType.FindBySubjectName, subject, !allowInvalid)
                .Where(c => c.HasPrivateKey && IsAllowedForServerAuth(c))
                .OrderByDescending(c => c.NotAfter)
                .FirstOrDefault();
        }

        private static bool IsAllowedForServerAuth(X509Certificate2 certificate)
        {
            foreach (var extension in certificate.Extensions)
            {
                if (extension is X509EnhancedKeyUsageExtension usage)
                    return usage.EnhancedKeyUsages.Cast<Oid>().Any(oid => oid.Value == ServerAuthenticationOid);
            }

            // No enhanced key usage extension means any use is allowed
            return true;
        }
    }
}

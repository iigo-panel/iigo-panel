using Microsoft.Win32;
using System.Security.Cryptography;
using System.Text;

namespace IIGO.Services
{
    /// <summary>
    /// A stable ID for this server, used to tie a license to the servers it covers.
    /// </summary>
    /// <remarks>
    /// Derived from the Windows MachineGuid, which stays the same until Windows is reinstalled or sysprepped.
    /// The GUID is hashed so the ID a customer sends doesn't reveal it. IIGO Pro computes the same ID itself,
    /// so a change here must be made there too.
    /// </remarks>
    public static class ServerIdentity
    {
        static readonly Lazy<string?> id = new(Compute);

        /// <summary>This server's ID, such as 3F2A-91C0-7B4E-D218, or null when Windows doesn't provide a MachineGuid</summary>
        public static string? Id => id.Value;

        static string? Compute()
        {
            try
            {
                using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                    .OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                if (key?.GetValue("MachineGuid") is not string guid || string.IsNullOrWhiteSpace(guid))
                    return null;

                var hash = SHA256.HashData(Encoding.UTF8.GetBytes("IIGO server " + guid.Trim().ToLowerInvariant()));
                return string.Join('-', Convert.ToHexString(hash, 0, 8).Chunk(4).Select(c => new string(c)));
            }
            catch
            {
                return null;
            }
        }
    }
}

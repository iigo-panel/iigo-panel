using System;
using System.Net;

namespace Microsoft.Web.Administration
{
    /// <summary>
    /// Registers and removes HTTP.sys certificate mappings for https bindings. Committing
    /// a binding to applicationHost.config does not register its certificate, so callers
    /// must do it separately.
    /// </summary>
    public static class SslCertificateBinding
    {
        // The application ID IIS uses for its own HTTP.sys certificate mappings
        public static readonly Guid IisAppId = new Guid("4dc3e181-e14b-4a21-b022-59fc669b0914");

        public static void Bind(Binding binding, byte[] hash, string storeName)
        {
            if (binding.SslFlags.HasFlag(SslFlags.Sni))
            {
                NativeMethods.BindSni(new Tuple<string, int>(binding.Host, binding.EndPoint.Port), hash, storeName, IisAppId);
            }
            else
            {
                NativeMethods.BindCertificate(binding.EndPoint, hash, storeName, IisAppId);
            }
        }

        public static void Unbind(Binding binding)
        {
            Unbind(binding.SslFlags.HasFlag(SslFlags.Sni), binding.Host, binding.EndPoint);
        }

        public static void Unbind(bool sni, string host, IPEndPoint endPoint)
        {
            if (sni)
            {
                var key = new Tuple<string, int>(host, endPoint.Port);
                if (NativeMethods.QuerySslSniInfo(key) != null)
                {
                    NativeMethods.DeleteSniBinding(key);
                }
            }
            else if (NativeMethods.QuerySslCertificateInfo(endPoint) != null)
            {
                NativeMethods.DeleteCertificateBinding(endPoint);
            }
        }

        /// <summary>
        /// Key used to tell whether two https bindings share the same HTTP.sys mapping
        /// </summary>
        public static string MappingKey(Binding binding)
        {
            return MappingKey(binding.SslFlags.HasFlag(SslFlags.Sni), binding.Host, binding.EndPoint);
        }

        public static string MappingKey(bool sni, string host, IPEndPoint endPoint)
        {
            return sni
                ? $"sni:{host.ToLowerInvariant()}:{endPoint.Port}"
                : $"ip:{endPoint}";
        }
    }
}

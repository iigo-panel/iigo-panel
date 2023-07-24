using System;
using Whois;

namespace IIGO.Services
{
    internal static class WhoisService
    {
        static readonly WhoisLookup whois = new WhoisLookup();

        public static (string registrar, DateTime expiration) DomainLookup(string domain)
        {
            var resp = whois.Lookup(domain);
            if (resp.Status == WhoisStatus.NotFound)
            {
                return ("NotFound", DateTime.UtcNow);
            }
            if ((resp.Status == WhoisStatus.Throttled || resp.Expiration == null) && resp.Referrer.Status == WhoisStatus.Found && resp.Referrer.Expiration != null)
                resp = resp.Referrer;
            if (resp.Status == WhoisStatus.Found)
            {
                return (resp.Registrar.Name, resp.Expiration.Value);
            }

            return ("ERROR", DateTime.UtcNow);
        }
    }
}

using IIGO.Data;
using Microsoft.EntityFrameworkCore;

namespace IIGO.Services
{
    public class MonitorHistoryService(ApplicationDbContext context)
    {
        public Task<List<AppPoolMonitoring>> GetAppPoolEventsAsync(int days)
        {
            var since = DateTime.UtcNow.AddDays(-days);
            return context.AppPoolMonitoring
                .AsNoTracking()
                .Where(x => x.AppPoolStartTime > since)
                .OrderByDescending(x => x.AppPoolStartTime)
                .ToListAsync();
        }

        public Task<List<CertificateMonitoring>> GetCertificateEventsAsync(int days)
        {
            var since = DateTime.UtcNow.AddDays(-days);
            return context.CertificateMonitoring
                .AsNoTracking()
                .Where(x => x.LoggedTime > since)
                .OrderByDescending(x => x.LoggedTime)
                .ToListAsync();
        }
    }
}

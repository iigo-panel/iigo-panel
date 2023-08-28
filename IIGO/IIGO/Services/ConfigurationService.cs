using IIGO.Data;
using System.Collections.Generic;
using System.Linq;

namespace IIGO.Services
{
    internal class ConfigurationService
    {
        private readonly ApplicationDbContext _context;

        public ConfigurationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<ConfigSetting> GetSettings()
        {
            return _context.ConfigSetting.ToList();
        }
    }
}

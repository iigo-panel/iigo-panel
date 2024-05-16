using IIGO.Data;
using System.Collections.Generic;
using System.Linq;

namespace IIGO.Services
{
    internal class ConfigurationService(ApplicationDbContext context)
    {
        public List<ConfigSetting> GetSettings() => [.. context.ConfigSetting];
    }
}

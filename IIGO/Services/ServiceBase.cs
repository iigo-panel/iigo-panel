using IIGO.Data;
using System.Linq;
using System.Reflection;

namespace IIGO.Services
{
    internal abstract class ServiceBase
    {
        private readonly ApplicationDbContext _context;

        public ServiceBase(ApplicationDbContext context)
        {
            _context = context;
        }

        protected string GetSetting(string setting, string defaultValue = "")
        {
            if (_context.ConfigSetting.FirstOrDefault(x => x.SettingName == setting) == null)
            {
                _context.ConfigSetting.Add(new ConfigSetting { SettingName = setting, SettingValue = defaultValue });
                _context.SaveChanges();
            }

            return _context.ConfigSetting.FirstOrDefault(x => x.SettingName == setting)?.SettingValue ?? defaultValue;
        }
    }
}

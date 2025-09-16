using IIGO.Data;
using System.Linq;
using System.Reflection;

namespace IIGO.Services
{
    internal abstract class ServiceBase(ApplicationDbContext context)
    {
        protected string GetSetting(string setting, string defaultValue = "")
        {
            if (context.ConfigSetting.FirstOrDefault(x => x.SettingName == setting) == null)
            {
                context.ConfigSetting.Add(new ConfigSetting { SettingName = setting, SettingValue = defaultValue });
                context.SaveChanges();
            }

            return context.ConfigSetting.FirstOrDefault(x => x.SettingName == setting)?.SettingValue ?? defaultValue;
        }
    }
}

using IIGO.Data;
using System.Collections.Generic;
using System.Linq;

namespace IIGO.Services
{
    internal class ConfigurationService(ApplicationDbContext context)
    {
        public List<ConfigSetting> GetSettings() => [.. context.ConfigSetting];

        public Dictionary<string, string> GetSettings(IEnumerable<string> names)
        {
            var list = names.ToList();
            return context.ConfigSetting
                .Where(x => list.Contains(x.SettingName))
                .ToDictionary(x => x.SettingName, x => x.SettingValue);
        }

        public void SaveSettings(IDictionary<string, string> values)
        {
            var names = values.Keys.ToList();
            var existing = context.ConfigSetting.Where(x => names.Contains(x.SettingName)).ToDictionary(x => x.SettingName);
            foreach (var (name, value) in values)
            {
                if (existing.TryGetValue(name, out var setting))
                    setting.SettingValue = value;
                else
                    context.ConfigSetting.Add(new ConfigSetting { SettingName = name, SettingValue = value });
            }
            context.SaveChanges();
        }
    }
}

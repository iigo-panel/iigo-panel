using IIGO.Data;
using System.Linq;
using System.Reflection;

namespace IIGO.Services
{
    public abstract class ServiceBase(ApplicationDbContext context, SecretProtector secrets)
    {
        protected string GetSetting(string setting, string defaultValue = "") => GetOrCreate(setting, defaultValue).SettingValue;

        /// <summary>
        /// Reads a setting that holds a secret. The value is stored encrypted; plain text found in the
        /// database (entered by hand, or from an earlier version) is encrypted the first time it's read.
        /// </summary>
        protected string GetSecret(string setting, string defaultValue = "")
        {
            var row = GetOrCreate(setting, defaultValue);
            if (secrets.IsProtected(row.SettingValue))
                return secrets.Unprotect(row.SettingValue) ?? "";

            if (!string.IsNullOrEmpty(row.SettingValue))
            {
                var plaintext = row.SettingValue;
                row.SettingValue = secrets.Protect(plaintext);
                context.SaveChanges();
                return plaintext;
            }

            return row.SettingValue;
        }

        ConfigSetting GetOrCreate(string setting, string defaultValue)
        {
            var row = context.ConfigSetting.FirstOrDefault(x => x.SettingName == setting);
            if (row == null)
            {
                row = new ConfigSetting { SettingName = setting, SettingValue = defaultValue };
                context.ConfigSetting.Add(row);
                context.SaveChanges();
            }

            return row;
        }
    }
}

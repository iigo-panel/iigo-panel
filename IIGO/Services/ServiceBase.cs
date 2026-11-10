using IIGO.Data;
using IIGO.Models;
using System.Linq;
using System.Reflection;

namespace IIGO.Services
{
    public abstract class ServiceBase(ApplicationDbContext context, SecretProtector secrets)
    {
        /// <summary>
        /// The service's settings, shown on the Settings page
        /// </summary>
        public abstract IReadOnlyList<MonitorSetting> Settings { get; }

        /// <summary>
        /// Adds any of the service's settings that aren't in the database yet, with their default values
        /// </summary>
        public void Initialize()
        {
            var names = Settings.Select(x => x.Name).ToList();
            var existing = context.ConfigSetting.Where(x => names.Contains(x.SettingName)).Select(x => x.SettingName).ToHashSet();
            foreach (var setting in Settings.Where(x => !existing.Contains(x.Name)))
                context.ConfigSetting.Add(new ConfigSetting { SettingName = setting.Name, SettingValue = setting.DefaultValue });
            context.SaveChanges();
        }

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

        /// <summary>
        /// Reads a required setting, with an error naming it when it's empty
        /// </summary>
        protected string GetRequired(string setting, bool secret = false)
        {
            var value = secret ? GetSecret(setting) : GetSetting(setting);
            if (string.IsNullOrWhiteSpace(value))
            {
                var label = Settings.FirstOrDefault(x => x.Name == setting)?.Label ?? setting;
                throw new InvalidOperationException($"{label} is not set.");
            }
            return value.Trim();
        }

        protected static void RequireRecipients(MessageData message)
        {
            if (message.Recipients.Count == 0)
                throw new InvalidOperationException("The message has no recipients.");
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

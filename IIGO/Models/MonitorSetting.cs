namespace IIGO.Models
{
    public enum MonitorSettingType
    {
        Toggle,
        Number,
        Text,
        EmailList,
        // Two positive numbers separated by a comma, e.g. "5,30". Shown as two boxes,
        // each followed by its entry in MonitorSetting.PartLabels
        NumberPair,
        // A single email address; may be empty
        Email,
        // Stored encrypted and never shown; a blank entry keeps the saved value
        Secret,
        // One of MonitorSetting.Options
        Choice
    }

    public record MonitorSetting(string Name, string Label, MonitorSettingType Type, string DefaultValue, string? Help = null, IReadOnlyList<string>? Options = null)
    {
        /// <summary>
        /// Returns why <paramref name="value"/> (already trimmed) can't be saved, or null if it can.
        /// Shared by the settings form and the API.
        /// </summary>
        public string? Validate(string value)
        {
            // ConfigSetting.SettingValue is varchar(300)
            if (value.Length > 300)
                return "Must be 300 characters or fewer.";

            switch (Type)
            {
                case MonitorSettingType.Toggle:
                    return bool.TryParse(value, out _) ? null : "Enter true or false.";
                case MonitorSettingType.Number:
                    return int.TryParse(value, out var n) && n > 0 ? null : "Enter a whole number greater than 0.";
                case MonitorSettingType.NumberPair:
                    var parts = value.Split(',', StringSplitOptions.TrimEntries);
                    return parts.Length == 2 && parts.All(p => int.TryParse(p, out var x) && x > 0) ? null : "Enter two whole numbers separated by a comma, e.g. 5,30.";
                case MonitorSettingType.Email:
                    return value.Length == 0 || System.Net.Mail.MailAddress.TryCreate(value, out _) ? null : "Enter a valid email address.";
                case MonitorSettingType.Choice:
                    return Options?.Contains(value) == true ? null : "Choose one of the options.";
                case MonitorSettingType.EmailList:
                    var addresses = value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    var invalid = addresses.Where(a => !System.Net.Mail.MailAddress.TryCreate(a, out _)).ToList();
                    return invalid.Count == 0 ? null : $"Not a valid email address: {string.Join(", ", invalid)}";
                default:
                    return null;
            }
        }
    }
}

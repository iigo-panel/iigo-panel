namespace IIGO.Models
{
    public enum MonitorSettingType
    {
        Toggle,
        Number,
        Text,
        EmailList,
        Messenger,
        // Two positive numbers separated by a comma, e.g. "5,30"
        NumberPair
    }

    public record MonitorSetting(string Name, string Label, MonitorSettingType Type, string DefaultValue, string? Help = null);
}

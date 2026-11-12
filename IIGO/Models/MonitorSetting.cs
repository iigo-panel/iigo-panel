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

    public record MonitorSetting(string Name, string Label, MonitorSettingType Type, string DefaultValue, string? Help = null, IReadOnlyList<string>? Options = null, IReadOnlyList<string>? PartLabels = null);
}

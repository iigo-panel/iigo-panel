using System.Reflection;

namespace IIGO.Services
{
    internal static class Constants
    {
        public const string EventLogSource = "IIGO";
        public const string EventLogName= "IIGO";
        public readonly static string IIGOVersion = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.0-beta";
        public readonly static DateTime BuildDate = DateTime.Parse("2026/10/27");
    }
}

using IIGO.Services;

namespace IIGO.Plugins
{
    /// <summary>
    /// What the panel tells a plugin about its environment
    /// </summary>
    public sealed class PluginContext
    {
        public required IConfiguration Configuration { get; init; }
        public required IHostEnvironment Environment { get; init; }
        public required LicenseInfo License { get; init; }

        /// <summary>
        /// SQLite connection string of the panel's database, with the full path resolved.
        /// A plugin with its own tables should use its own DbContext and migrations history table on this database.
        /// </summary>
        public required string ConnectionString { get; init; }

        /// <summary>Folder the plugin's assembly was loaded from</summary>
        public required string PluginDirectory { get; init; }
    }
}

using IIGO.Services;
using System.Reflection;

namespace IIGO.Plugins
{
    /// <summary>
    /// A plugin that loaded and configured its services
    /// </summary>
    public sealed class LoadedPlugin(IIgoPlugin plugin, Assembly assembly, PluginContext context)
    {
        public IIgoPlugin Plugin { get; } = plugin;
        public Assembly Assembly { get; } = assembly;
        public PluginContext Context { get; } = context;
        public string Name => Plugin.Name;
        public string Version { get; } =
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "";
    }

    /// <summary>
    /// Permissions shown together on the Role Permissions page
    /// </summary>
    public sealed record FeatureGroup(string Name, IReadOnlyList<string> Features);

    /// <summary>
    /// Something worth logging from plugin loading. Collected before logging is available and written after startup.
    /// </summary>
    public sealed record PluginLoadMessage(LogLevel Level, string Message, Exception? Exception = null);

    /// <summary>
    /// A plugin folder served as static files at an address
    /// </summary>
    public sealed record PluginStaticFiles(string Directory, string RequestPath);

    /// <summary>
    /// The plugins loaded at startup and everything they add to the panel. Registered as a singleton.
    /// </summary>
    public sealed class PluginRegistry
    {
        public const string CoreFeatureGroup = "IIGO";

        internal PluginRegistry(
            IReadOnlyList<LoadedPlugin> plugins,
            IReadOnlyList<PluginLoadMessage> messages,
            IReadOnlyList<NavMenuItem> menuItems,
            IReadOnlyList<FeatureGroup> featureGroups,
            IReadOnlyList<string> stylesheets,
            IReadOnlyList<PluginStaticFiles> staticFileRoots)
        {
            Plugins = plugins;
            Messages = messages;
            MenuItems = menuItems;
            FeatureGroups = featureGroups;
            Stylesheets = stylesheets;
            StaticFileRoots = staticFileRoots;
            Assemblies = plugins.Select(p => p.Assembly).Distinct().ToList();
            AllFeatures = featureGroups.SelectMany(g => g.Features).ToList();
        }

        /// <summary>A registry with no plugins, holding only the panel's own features</summary>
        public static PluginRegistry Empty { get; } = new([], [], [], [new FeatureGroup(CoreFeatureGroup, Feature.All)], [], []);

        public IReadOnlyList<LoadedPlugin> Plugins { get; }

        /// <summary>What happened while loading, including plugins that were skipped and why</summary>
        public IReadOnlyList<PluginLoadMessage> Messages { get; }

        /// <summary>Plugin assemblies, for the router and endpoint mapping to find their pages</summary>
        public IReadOnlyList<Assembly> Assemblies { get; }

        public IReadOnlyList<NavMenuItem> MenuItems { get; }

        /// <summary>The panel's own features first, then each plugin's</summary>
        public IReadOnlyList<FeatureGroup> FeatureGroups { get; }

        /// <summary>Every feature name, each of which has an authorization policy of the same name</summary>
        public IReadOnlyList<string> AllFeatures { get; }

        /// <summary>Stylesheet addresses added to every page</summary>
        public IReadOnlyList<string> Stylesheets { get; }

        /// <summary>Plugin wwwroot folders, each served at _content/{assembly name}</summary>
        public IReadOnlyList<PluginStaticFiles> StaticFileRoots { get; }

        public IEnumerable<NavMenuItem> MenuItemsFor(string section) =>
            MenuItems.Where(i => string.Equals(i.Section, section, StringComparison.OrdinalIgnoreCase))
                .OrderBy(i => i.Order).ThenBy(i => i.Title, StringComparer.OrdinalIgnoreCase);

        /// <summary>Sections plugins added that the panel doesn't have, in order of their first link</summary>
        public IEnumerable<string> ExtraSections =>
            MenuItems.Where(i => !NavSections.All.Contains(i.Section, StringComparer.OrdinalIgnoreCase))
                .GroupBy(i => i.Section, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Min(i => i.Order)).ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First().Section);
    }
}

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

        // What the plugin added, so it can be taken out again if the plugin fails to start
        internal IReadOnlyList<NavMenuItem> MenuItems { get; init; } = [];
        internal IReadOnlyList<string> Stylesheets { get; init; } = [];
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

        readonly List<LoadedPlugin> plugins;
        readonly List<PluginLoadMessage> messages;
        readonly List<NavMenuItem> menuItems;
        readonly List<FeatureGroup> featureGroups;
        readonly List<string> stylesheets;

        internal PluginRegistry(
            IReadOnlyList<LoadedPlugin> plugins,
            IReadOnlyList<PluginLoadMessage> messages,
            IReadOnlyList<NavMenuItem> menuItems,
            IReadOnlyList<FeatureGroup> featureGroups,
            IReadOnlyList<string> stylesheets,
            IReadOnlyList<PluginStaticFiles> staticFileRoots)
        {
            this.plugins = [.. plugins];
            this.messages = [.. messages];
            this.menuItems = [.. menuItems];
            this.featureGroups = [.. featureGroups];
            this.stylesheets = [.. stylesheets];
            StaticFileRoots = staticFileRoots;
            AllFeatures = featureGroups.SelectMany(g => g.Features).ToList();
        }

        /// <summary>A registry with no plugins, holding only the panel's own features</summary>
        public static PluginRegistry Empty { get; } = new([], [], [], [new FeatureGroup(CoreFeatureGroup, Feature.All)], [], []);

        public IReadOnlyList<LoadedPlugin> Plugins => plugins;

        /// <summary>What happened while loading, including plugins that were skipped and why</summary>
        public IReadOnlyList<PluginLoadMessage> Messages => messages;

        /// <summary>Plugin assemblies, for the router and endpoint mapping to find their pages</summary>
        public IReadOnlyList<Assembly> Assemblies => plugins.Select(p => p.Assembly).Distinct().ToList();

        public IReadOnlyList<NavMenuItem> MenuItems => menuItems;

        /// <summary>The panel's own features first, then each plugin's</summary>
        public IReadOnlyList<FeatureGroup> FeatureGroups => featureGroups;

        /// <summary>
        /// Every feature name, each of which has an authorization policy of the same name.
        /// Fixed at startup, since the policies are registered from it.
        /// </summary>
        public IReadOnlyList<string> AllFeatures { get; }

        /// <summary>Stylesheet addresses added to every page</summary>
        public IReadOnlyList<string> Stylesheets => stylesheets;

        /// <summary>Plugin wwwroot folders, each served at _content/{assembly name}</summary>
        public IReadOnlyList<PluginStaticFiles> StaticFileRoots { get; }

        /// <summary>
        /// Takes a plugin that failed to start out of the panel: its pages, menu links, permissions and stylesheets.
        /// Only for use during startup, before the app runs. Services it registered stay registered.
        /// </summary>
        internal void Disable(LoadedPlugin plugin, string message, Exception exception)
        {
            plugins.Remove(plugin);
            messages.Add(new PluginLoadMessage(LogLevel.Error, message, exception));
            menuItems.RemoveAll(item => plugin.MenuItems.Contains(item));
            featureGroups.RemoveAll(group => group.Name == plugin.Name);
            stylesheets.RemoveAll(sheet => plugin.Stylesheets.Contains(sheet));

            // The assembly's own scoped CSS, unless another plugin from the same assembly still runs
            if (!plugins.Any(p => p.Assembly == plugin.Assembly))
            {
                var prefix = "_content/" + plugin.Assembly.GetName().Name + "/";
                stylesheets.RemoveAll(sheet => sheet.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            }
        }

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

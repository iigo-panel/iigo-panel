namespace IIGO.Plugins
{
    /// <summary>
    /// Entry point of a plugin. The panel loads each assembly from the Plugins folder, creates every public,
    /// non-abstract class that implements this interface with a parameterless constructor, and calls it at startup.
    /// Plugins load once per start; installing, updating or removing one needs a service restart.
    /// </summary>
    public interface IIgoPlugin
    {
        /// <summary>Name shown in the panel, also used to group the plugin's permissions</summary>
        string Name { get; }

        /// <summary>
        /// Lowest panel version this plugin works with, as a semantic version ("1.0.0-beta8").
        /// The plugin is skipped on older panels.
        /// </summary>
        string MinHostVersion => "1.0.0-beta1";

        /// <summary>
        /// License entitlement the plugin needs, or null if it runs on any license.
        /// The panel skips the plugin when the license doesn't list it. This is a convenience only, since the
        /// panel is open source: a paid plugin should also check <see cref="PluginContext.License"/> itself.
        /// </summary>
        string? RequiredEntitlement => null;

        /// <summary>Called before the app is built. Register services, hosted services and DbContexts here.</summary>
        void ConfigureServices(IServiceCollection services, PluginContext context) { }

        /// <summary>
        /// Called after the panel's middleware is set up and before its endpoints are mapped.
        /// Map extra endpoints or run database migrations here.
        /// </summary>
        void Configure(WebApplication app, PluginContext context) { }

        /// <summary>Links added to the sidebar</summary>
        IEnumerable<NavMenuItem> MenuItems => [];

        /// <summary>
        /// Permissions the plugin adds. Each becomes an authorization policy of the same name and a switch on the
        /// Role Permissions page. Prefix them with the plugin name ("Pro.ViewBackups") to avoid clashes.
        /// </summary>
        IEnumerable<string> Features => [];

        /// <summary>
        /// Extra stylesheets, relative to the site root ("_content/IIGO.Pro/pro.css").
        /// The plugin's scoped CSS bundle is added automatically.
        /// </summary>
        IEnumerable<string> Stylesheets => [];
    }
}

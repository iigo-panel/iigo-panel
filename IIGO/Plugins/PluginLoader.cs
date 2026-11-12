using IIGO.Services;
using Semver;
using System.Reflection;
using System.Runtime.Loader;

namespace IIGO.Plugins
{
    /// <summary>
    /// Finds, loads and configures plugins at startup.
    /// </summary>
    /// <remarks>
    /// Plugins come from two places:
    /// <list type="bullet">
    /// <item>Each subfolder of the Plugins folder (setting Plugins:Directory, default ~\Plugins) holding an assembly
    /// of the same name: Plugins\IIGO.Pro\IIGO.Pro.dll. Static files go in Plugins\IIGO.Pro\wwwroot and are served
    /// at _content/IIGO.Pro, with the scoped CSS bundle (IIGO.Pro.bundle.scp.css) at the top of that folder.</item>
    /// <item>The Plugins:Paths setting, a list of assembly files or folders, for running a plugin from its build output.</item>
    /// </list>
    /// Assemblies load into the default load context, so a plugin shares the panel's own copy of IIGO, ASP.NET Core
    /// and Blazor. Anything the panel doesn't have comes from the plugin's folder. The panel runs as LocalSystem,
    /// so only administrators may be able to write to these folders.
    /// </remarks>
    internal static class PluginLoader
    {
        static readonly List<(string Directory, AssemblyDependencyResolver? Resolver)> resolvers = [];
        static bool resolvingHooked;

        public static PluginRegistry Load(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment,
            LicenseInfo license, string connectionString)
        {
            var messages = new List<PluginLoadMessage>();
            var plugins = new List<LoadedPlugin>();
            var menuItems = new List<NavMenuItem>();
            var featureGroups = new List<FeatureGroup> { new(PluginRegistry.CoreFeatureGroup, Feature.All) };
            var knownFeatures = new HashSet<string>(Feature.All, StringComparer.OrdinalIgnoreCase);
            var stylesheets = new List<string>();
            var staticRoots = new List<PluginStaticFiles>();

            if (!SemVersion.TryParse(Constants.IIGOVersion, SemVersionStyles.Any, out var hostVersion))
                hostVersion = null;

            foreach (var path in FindAssemblies(configuration, messages))
            {
                Assembly assembly;
                try
                {
                    assembly = LoadAssembly(path);
                }
                catch (Exception ex)
                {
                    messages.Add(new(LogLevel.Error, $"Could not load plugin assembly {path}", ex));
                    continue;
                }

                var directory = Path.GetDirectoryName(path)!;
                var found = false;
                foreach (var type in FindPluginTypes(assembly, messages))
                {
                    found = true;
                    IIgoPlugin plugin;
                    try
                    {
                        plugin = (IIgoPlugin)Activator.CreateInstance(type)!;
                    }
                    catch (Exception ex)
                    {
                        messages.Add(new(LogLevel.Error, $"Could not create plugin {type.FullName} from {path}", ex));
                        continue;
                    }

                    var name = string.IsNullOrWhiteSpace(plugin.Name) ? type.FullName! : plugin.Name;

                    if (plugins.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
                    {
                        messages.Add(new(LogLevel.Warning, $"Skipped plugin {name} from {path}: a plugin with that name is already loaded."));
                        continue;
                    }

                    if (!SemVersion.TryParse(plugin.MinHostVersion, SemVersionStyles.Any, out var minVersion))
                    {
                        messages.Add(new(LogLevel.Error, $"Skipped plugin {name}: its minimum panel version '{plugin.MinHostVersion}' is not a valid version."));
                        continue;
                    }
                    if (hostVersion != null && hostVersion.ComparePrecedenceTo(minVersion) < 0)
                    {
                        messages.Add(new(LogLevel.Warning, $"Skipped plugin {name}: it needs panel version {minVersion} or newer, and this is {hostVersion}."));
                        continue;
                    }

                    if (!string.IsNullOrEmpty(plugin.RequiredEntitlement) && !license.HasEntitlement(plugin.RequiredEntitlement))
                    {
                        messages.Add(new(LogLevel.Warning, $"Skipped plugin {name}: the license does not include '{plugin.RequiredEntitlement}'."));
                        continue;
                    }

                    var context = new PluginContext
                    {
                        Configuration = configuration,
                        Environment = environment,
                        License = license,
                        ConnectionString = connectionString,
                        PluginDirectory = directory
                    };

                    // Everything the plugin adds is collected first, so a plugin that fails part way adds nothing
                    List<NavMenuItem> pluginMenu;
                    List<string> pluginFeatures;
                    List<string> pluginStylesheets;
                    var pluginServices = new ServiceCollection();
                    try
                    {
                        pluginMenu = plugin.MenuItems.ToList();
                        pluginFeatures = plugin.Features.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                        pluginStylesheets = plugin.Stylesheets.ToList();
                        plugin.ConfigureServices(pluginServices, context);
                    }
                    catch (Exception ex)
                    {
                        messages.Add(new(LogLevel.Error, $"Skipped plugin {name}: it failed while setting up.", ex));
                        continue;
                    }

                    foreach (var feature in pluginFeatures.ToList())
                    {
                        if (!knownFeatures.Add(feature))
                        {
                            pluginFeatures.Remove(feature);
                            messages.Add(new(LogLevel.Warning, $"Plugin {name} declares feature '{feature}', which already exists. Its declaration was ignored."));
                        }
                    }

                    foreach (var descriptor in pluginServices)
                        services.Add(descriptor);

                    plugins.Add(new LoadedPlugin(plugin, assembly, context));
                    menuItems.AddRange(pluginMenu);
                    if (pluginFeatures.Count > 0)
                        featureGroups.Add(new FeatureGroup(name, pluginFeatures));
                    stylesheets.AddRange(pluginStylesheets);

                    messages.Add(new(LogLevel.Information, $"Loaded plugin {name} {plugins[^1].Version} from {path}"));
                }

                if (!found)
                {
                    messages.Add(new(LogLevel.Warning, $"{path} has no public class implementing {nameof(IIgoPlugin)} with a parameterless constructor."));
                    continue;
                }

                // Each assembly's static files and scoped CSS are added once, however many plugins it holds
                if (plugins.Any(p => p.Assembly == assembly))
                    AddStaticFiles(assembly, directory, stylesheets, staticRoots);
            }

            return new PluginRegistry(plugins, messages, menuItems, featureGroups, stylesheets.Distinct().ToList(), staticRoots);
        }

        static List<string> FindAssemblies(IConfiguration configuration, List<PluginLoadMessage> messages)
        {
            var found = new List<string>();

            var pluginsDirectory = AppDomain.CurrentDomain.MapPath(configuration["Plugins:Directory"] ?? "~\\Plugins");
            if (Directory.Exists(pluginsDirectory))
            {
                foreach (var directory in Directory.GetDirectories(pluginsDirectory).Order(StringComparer.OrdinalIgnoreCase))
                {
                    var file = Path.Combine(directory, Path.GetFileName(directory) + ".dll");
                    if (File.Exists(file))
                        found.Add(file);
                    else
                        messages.Add(new(LogLevel.Warning, $"Plugin folder {directory} has no {Path.GetFileName(file)}."));
                }
            }

            foreach (var configured in configuration.GetSection("Plugins:Paths").Get<string[]>() ?? [])
            {
                var path = AppDomain.CurrentDomain.MapPath(configured);
                if (File.Exists(path))
                    found.Add(Path.GetFullPath(path));
                else if (Directory.Exists(path) && File.Exists(Path.Combine(path, Path.GetFileName(Path.TrimEndingDirectorySeparator(path)) + ".dll")))
                    found.Add(Path.GetFullPath(Path.Combine(path, Path.GetFileName(Path.TrimEndingDirectorySeparator(path)) + ".dll")));
                else
                    messages.Add(new(LogLevel.Warning, $"Plugin path {path} from Plugins:Paths is not an assembly or a folder holding one of the same name."));
            }

            // The same assembly can only load once
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<string>();
            foreach (var path in found)
            {
                var name = Path.GetFileNameWithoutExtension(path);
                if (names.Add(name))
                    result.Add(path);
                else
                    messages.Add(new(LogLevel.Warning, $"Skipped {path}: an assembly named {name} is already being loaded as a plugin."));
            }
            return result;
        }

        static Assembly LoadAssembly(string path)
        {
            var name = AssemblyName.GetAssemblyName(path);
            if (AssemblyLoadContext.Default.Assemblies.Any(a => AssemblyName.ReferenceMatchesDefinition(a.GetName(), name)))
                throw new InvalidOperationException($"An assembly named {name.Name} is already loaded, so {path} can't be loaded as a plugin.");

            var directory = Path.GetDirectoryName(path)!;
            AssemblyDependencyResolver? resolver = null;
            try
            {
                resolver = new AssemblyDependencyResolver(path);
            }
            catch
            {
                // Without a .deps.json file, dependencies are looked up by name in the plugin's folder
            }

            lock (resolvers)
            {
                resolvers.Add((directory, resolver));
                if (!resolvingHooked)
                {
                    AssemblyLoadContext.Default.Resolving += ResolvePluginDependency;
                    AssemblyLoadContext.Default.ResolvingUnmanagedDll += ResolvePluginNativeDependency;
                    resolvingHooked = true;
                }
            }

            return AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
        }

        // Only called for assemblies the panel itself doesn't have
        static Assembly? ResolvePluginDependency(AssemblyLoadContext context, AssemblyName name)
        {
            lock (resolvers)
            {
                foreach (var (directory, resolver) in resolvers)
                {
                    var path = resolver?.ResolveAssemblyToPath(name);
                    if (path == null)
                    {
                        var candidate = Path.Combine(directory, name.Name + ".dll");
                        if (File.Exists(candidate))
                            path = candidate;
                    }
                    if (path != null)
                        return context.LoadFromAssemblyPath(path);
                }
            }
            return null;
        }

        static IntPtr ResolvePluginNativeDependency(Assembly assembly, string name)
        {
            lock (resolvers)
            {
                foreach (var (_, resolver) in resolvers)
                {
                    var path = resolver?.ResolveUnmanagedDllToPath(name);
                    if (path != null)
                        return System.Runtime.InteropServices.NativeLibrary.Load(path);
                }
            }
            return IntPtr.Zero;
        }

        static IEnumerable<Type> FindPluginTypes(Assembly assembly, List<PluginLoadMessage> messages)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                messages.Add(new(LogLevel.Warning, $"Some types in {assembly.GetName().Name} could not be loaded: {string.Join("; ", ex.LoaderExceptions.Select(e => e?.Message).Distinct())}"));
                types = ex.Types.Where(t => t != null).ToArray()!;
            }

            return types
                .Where(t => t is { IsClass: true, IsAbstract: false, IsPublic: true }
                    && typeof(IIgoPlugin).IsAssignableFrom(t)
                    && t.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(t => t.FullName, StringComparer.Ordinal);
        }

        // The plugin's wwwroot folder is served at _content/{assembly name}, the address a Razor class library's
        // files have when referenced at build time, so its pages can use the usual paths
        static void AddStaticFiles(Assembly assembly, string directory, List<string> stylesheets, List<PluginStaticFiles> staticRoots)
        {
            var webRoot = Path.Combine(directory, "wwwroot");
            if (!Directory.Exists(webRoot))
                return;

            var name = assembly.GetName().Name!;
            var requestPath = "/_content/" + name;
            staticRoots.Add(new PluginStaticFiles(webRoot, requestPath));

            // The scoped CSS of the plugin's components: the library bundle, or the app bundle if it was published on its own
            foreach (var bundle in new[] { name + ".bundle.scp.css", name + ".styles.css" })
            {
                if (File.Exists(Path.Combine(webRoot, bundle)))
                {
                    stylesheets.Add($"{requestPath[1..]}/{bundle}");
                    break;
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wsla
{
    public abstract class PluginDefinitionAttribute : Attribute
    {
        /// <summary>
        /// Order of plugin loading, smaller values will get loaded before higher values
        /// </summary>
        public int Order { get; set; }

        public abstract IPlugin Create();
    }

    public interface IPlugin
    {
        void Load(PluginLoadContext context);
    }

    public class PluginSystem<TDefinition, TPlugin>
        where TDefinition : PluginDefinitionAttribute
        where TPlugin : class, IPlugin
    {
        const string PluginDirectoryName = "Plugins";
        const string PluginConfigFileName = "plugin-config.json";

        JsonSerializerOptions JsonOptions;
        public PluginSystem()
        {
            JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.General);
            JsonOptions.Converters.Add(new NetworkVersionJsonConverter());
        }

        /// <summary>
        /// Loads all plugins from the default Plugins folder
        /// </summary>
        public void LoadAll()
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), PluginDirectoryName);
            LoadAll(path);
        }
        /// <summary>
        /// Load all from specified path
        /// </summary>
        /// <param name="path"></param>
        public void LoadAll(string path)
        {
            var info = new DirectoryInfo(path);
            if (info.Exists is false)
            {
                NetworkLog.Warning($"No Plugins Directory Exists, Creating...");
                info.Create();
                return;
            }

            var subs = info.GetDirectories();
            var definitions = new List<DefinitionContext>(subs.Length);

            foreach (var sub in subs)
            {
                var definition = LoadDirectory(sub);

                if (definition == null)
                    continue;

                definitions.Add(definition.Value);
            }

            definitions.Sort((x, y) => x.Order.CompareTo(y.Order));

            foreach (var definition in definitions)
            {
                var plugin = definition.Definition.Create();
                var context = new PluginLoadContext(definition.Entrypoint);

                NetworkLog.Info($"Loading Plugin ({Path.GetFileName(context.DirectoryPath)})");

                plugin.Load(context);
            }
        }

        DefinitionContext? LoadDirectory(DirectoryInfo directory)
        {
            if (TryReadConfig(directory, out var configuration) is false)
                return default;

            if (configuration.ApiVersions.Contains(Constants.ApiVersion) is false)
            {
                NetworkLog.Warning($"Plugin ({directory.Name}) Doesn't Support API Version {Constants.ApiVersion}, Skipping");
                return default;
            }

            var entrypoint = Path.Combine(directory.FullName, configuration.Entrypoint);
            if (File.Exists(entrypoint) is false)
            {
                NetworkLog.Warning($"No EntryPoint ({configuration.Entrypoint}) File Found For ({directory.Name}) Plugin");
                return default;
            }

            var definition = LoadDLL(entrypoint);

            return new(definition, entrypoint);
        }
        record struct DefinitionContext(TDefinition Definition, string Entrypoint)
        {
            public int Order => Definition.Order;
        }

        bool TryReadConfig(DirectoryInfo directory, out PluginConfigurationFile configuration)
        {
            var file = new FileInfo(Path.Combine(directory.FullName, PluginConfigFileName));
            if (file.Exists is false)
            {
                NetworkLog.Warning($"No {PluginConfigFileName} Configuration File Found For ({directory.Name}) Plugin");
                configuration = default;
                return false;
            }

            try
            {
                using var stream = file.OpenRead();
                configuration = JsonSerializer.Deserialize<PluginConfigurationFile>(stream, options: JsonOptions);
                return true;
            }
            catch (Exception ex)
            {
                NetworkLog.Error($"Exception Reading ({file}) Plugin Configuration");
                NetworkLog.Error(ex);

                configuration = default;
                return false;
            }
        }

        TDefinition LoadDLL(string path)
        {
            var assembly = LoadAssembly(path);
            return assembly.GetCustomAttribute<TDefinition>();
        }
        Assembly LoadAssembly(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var context = new LoadContext(path);
            return context.LoadFromAssemblyName(new(Path.GetFileNameWithoutExtension(name)));
        }
        class LoadContext : AssemblyLoadContext
        {
            AssemblyDependencyResolver Resolver;

            protected override Assembly Load(AssemblyName name)
            {
                try
                {
                    return Default.LoadFromAssemblyName(name);
                }
                catch
                {
                    var path = Resolver.ResolveAssemblyToPath(name);
                    if (path == null)
                        return null;

                    return LoadFromAssemblyPath(path);
                }
            }

            public LoadContext(string path)
            {
                Resolver = new AssemblyDependencyResolver(path);
            }
        }
    }

    public class PluginConfigurationFile
    {
        [JsonRequired]
        public string Entrypoint { get; set; }

        [JsonRequired]
        public NetworkVersion[] ApiVersions { get; set; }
    }

    public struct PluginLoadContext
    {
        /// <summary>
        /// Path to the entrypoint dll, (../Plugins/You-Plugin/Your-Dll.dll)
        /// </summary>
        public string EntrypointPath { get; }

        /// <summary>
        /// Path to the entrypoint's directory (../Plugins/Your-Plugin)
        /// </summary>
        public ReadOnlySpan<char> DirectoryPath => Path.GetDirectoryName(EntrypointPath);

        public PluginLoadContext(string Entrypoint)
        {
            this.EntrypointPath = Entrypoint;
        }
    }
}
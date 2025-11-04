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
        public abstract Type Type { get; }
    }

    public interface IPlugin
    {
        void Load();
    }

    public class PluginSystem<TAttribute, TContract>
        where TAttribute : PluginDefinitionAttribute
        where TContract : class, IPlugin
    {
        const string PluginDirectoryName = "Plugins";
        const string PluginConfigFileName = "plugin-config.json";

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

            foreach (var sub in subs)
                LoadDirectory(sub);
        }

        void LoadDirectory(DirectoryInfo directory)
        {
            var file = new FileInfo(Path.Combine(directory.FullName, PluginConfigFileName));
            if (file.Exists is false)
            {
                NetworkLog.Warning($"No {PluginConfigFileName} Configuration File Found For ({directory.Name}) Plugin");
                return;
            }

            using var stream = file.OpenRead();
            var config = JsonSerializer.Deserialize<PluginConfigurationFile>(stream);

            var path = Path.Combine(directory.FullName, config.Entrypoint);
            if (File.Exists(path) is false)
            {
                NetworkLog.Warning($"No EntryPoint ({config.Entrypoint}) File Found For ({directory.Name}) Plugin ");
                return;
            }

            var contracts = LoadDLL(path).ToList();
            if (contracts.Count == 0)
            {
                NetworkLog.Warning($"No Contracts Defined For ({directory.Name}) Plugin ");
                return;
            }

            foreach (var contract in contracts)
                contract.Load();
        }

        IEnumerable<TContract> LoadDLL(string path)
        {
            var assembly = LoadAssembly(path);

            foreach (var attribute in LoadAttributes(assembly))
            {
                var instance = Activator.CreateInstance(attribute.Type) as TContract;
                yield return instance;
            }
        }
        Assembly LoadAssembly(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var context = new LoadContext(path);
            return context.LoadFromAssemblyName(new(Path.GetFileNameWithoutExtension(name)));
        }
        IEnumerable<TAttribute> LoadAttributes(Assembly assembly)
        {
            return assembly.GetCustomAttributes<TAttribute>();
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
    }
}
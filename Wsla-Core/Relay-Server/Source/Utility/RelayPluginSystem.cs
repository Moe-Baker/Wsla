using System;

namespace Wsla.Server
{
    public abstract class RelayPluginDefinitionAttribute : PluginDefinitionAttribute { }
    [AttributeUsage(AttributeTargets.Assembly, Inherited = true, AllowMultiple = false)]
    public sealed class RelayPluginDefinitionAttribute<TPlugin> : RelayPluginDefinitionAttribute
        where TPlugin : IRelayPlugin, new()
    {
        public override IPlugin Create() => new TPlugin();
    }

    public interface IRelayPlugin : IPlugin { }

    public class RelayPluginSystem : PluginSystem<RelayPluginDefinitionAttribute, IPlugin> { }
}
using System;

namespace Wsla.Server
{
    public abstract class RelayPluginDefinitionAttribute : PluginDefinitionAttribute
    {

    }
    [AttributeUsage(AttributeTargets.Assembly, Inherited = true, AllowMultiple = false)]
    public sealed class RelayPluginDefinitionAttribute<T> : RelayPluginDefinitionAttribute
    {
        public override Type Type => typeof(T);
    }

    public interface IRelayPlugin : IPlugin
    {

    }

    public class RelayPluginSystem : PluginSystem<RelayPluginDefinitionAttribute, IPlugin>
    {

    }
}
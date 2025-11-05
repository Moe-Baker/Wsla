using System;

namespace Wsla.Server
{
    public abstract class CoordinatorPluginDefinitionAttribute : PluginDefinitionAttribute { }
    [AttributeUsage(AttributeTargets.Assembly, Inherited = true, AllowMultiple = false)]
    public sealed class CoordinatorPluginDefinitionAttribute<TPlugin> : CoordinatorPluginDefinitionAttribute
        where TPlugin : ICoordinatorPlugin, new()
    {
        public override IPlugin Create() => new TPlugin();
    }

    public interface ICoordinatorPlugin : IPlugin { }

    public class CoordinatorPluginSystem : PluginSystem<CoordinatorPluginDefinitionAttribute, IPlugin> { }
}
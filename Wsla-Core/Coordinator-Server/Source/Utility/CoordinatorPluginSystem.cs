using System;

namespace Wsla.Server
{
    public abstract class CoordinatorPluginDefinitionAttribute : PluginDefinitionAttribute
    {

    }
    [AttributeUsage(AttributeTargets.Assembly, Inherited = true, AllowMultiple = false)]
    public sealed class CoordinatorPluginDefinitionAttribute<T> : CoordinatorPluginDefinitionAttribute
    {
        public override Type Type => typeof(T);
    }

    public interface ICoordinatorPlugin : IPlugin
    {

    }

    public class CoordinatorPluginSystem : PluginSystem<CoordinatorPluginDefinitionAttribute, IPlugin>
    {

    }
}
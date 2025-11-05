using Wsla.Server;

[assembly: CoordinatorPluginDefinition<CoordinatorPluginSample>]

namespace Wsla.Server
{
    public class CoordinatorPluginSample : ICoordinatorPlugin
    {
        public void Load(PluginLoadContext context)
        {
            NetworkLog.Info($"Hello From Coordinator Plugin, Entrypoint: {context.EntrypointPath}");
        }
    }
}
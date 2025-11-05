using Wsla.Server;

[assembly: RelayPluginDefinition<CoordinatorPluginSample>]

namespace Wsla.Server
{
    public class CoordinatorPluginSample : IRelayPlugin
    {
        public void Load(PluginLoadContext context)
        {
            NetworkLog.Info($"Hello From Relay Plugin, Entrypoint: {context.EntrypointPath}");
        }
    }
}
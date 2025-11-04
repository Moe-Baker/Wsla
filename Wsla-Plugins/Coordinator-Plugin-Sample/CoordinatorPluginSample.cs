using Wsla.Server;

[assembly: CoordinatorPluginDefinition<CoordinatorPluginSample>]

namespace Wsla.Server
{
    public class CoordinatorPluginSample : ICoordinatorPlugin
    {
        public void Load()
        {
            NetworkLog.Info("Hello From Coordinator Plugin");
        }
    }
}
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Net;
using System.Text.Json.Serialization;

namespace Wsla.Server
{
    public class CoordinatorServer
    {
        [AllowNull]
        public static ConfigurationData Configuration { get; private set; }
        public class ConfigurationData : ServerConfigurationData
        {
            public async Task Initialize()
            {

            }
        }

        public static class Messaging
        {
            [AllowNull]
            public static MessagingServer Server { get; private set; }

            public static void Start()
            {
                Server = new MessagingServer();

                Server.Dispatcher.Register<CreateRoomRequest>(CreateRoomHandler);

                Server.Start(Constants.CoordinatorMessagingPort);
            }

            static void CreateRoomHandler(MessagingPeer peer, ref CreateRoomRequest message)
            {

            }
        }

        static async Task Main(string[] args)
        {
            NetworkLog.UseConsole();

            await LoadConfig();

            ParseArguments(args);

            Messaging.Start();

            while (true) Console.ReadKey();
        }

        static async Task LoadConfig()
        {
            NetworkLog.Info($"Loading Configuration Data");

            Configuration = ServerConfigurationLoader.Load<ConfigurationData>();
            await Configuration.Initialize();
        }

        static void ParseArguments(string[] args) { }
    }
}
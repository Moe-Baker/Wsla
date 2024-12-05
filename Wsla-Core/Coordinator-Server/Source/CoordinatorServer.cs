using System.Net;

namespace Wsla.Server
{
    public class CoordinatorServer
    {
        public static ConfigurationData Configuration { get; private set; }
        public class ConfigurationData : ServerConfigurationData
        {
            public Task Initialize() => Task.CompletedTask;
        }

        public static class Messaging
        {
            public static MessagingServer Server { get; private set; }

            public static void Initialize()
            {
                Server = new MessagingServer();
            }

            public static void Start()
            {
                Server.Start(Constants.CoordinatorMessagingPort);
            }
        }

        public static class Matchmaking
        {
            public static Dictionary<ServerRegion, IPAddress> Regions { get; private set; }

            public static void Initialize()
            {
                Regions = new();

                Messaging.Server.Dispatcher.Register<RegisterRelayRequest>(RegisterRelayHandler);
                Messaging.Server.Dispatcher.Register<ListRelaysRequest>(ListRelaysHandler);
            }

            static void RegisterRelayHandler(MessagingPeer peer, ref RegisterRelayRequest message)
            {
                var address = (peer.Socket.RemoteEndPoint as IPEndPoint).Address;

                NetworkLog.Info($"Registering ({message.Region}) Server on Address: {address}");

                lock (Regions)
                {
                    Regions[message.Region] = address;
                }

                var response = new RegisterRelayResponse(address);
                peer.Send(response);
            }

            static void ListRelaysHandler(MessagingPeer peer, ref ListRelaysRequest message)
            {
                Dictionary<ServerRegion, IPAddress> Dictionary;

                lock (Regions)
                {
                    Dictionary = new(Regions);
                }

                var response = new ListRelaysResponse(Dictionary);
                peer.Send(response);
            }
        }

        static async Task Main(string[] args)
        {
            NetworkLog.UseConsole();

            await LoadConfig();

            ParseArguments(args);

            //Initialize
            {
                Messaging.Initialize();
                Matchmaking.Initialize();
            }

            //Start
            {
                Messaging.Start();
            }

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
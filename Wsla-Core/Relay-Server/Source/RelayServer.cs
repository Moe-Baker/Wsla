using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Text.Json.Serialization;

namespace Wsla.Server
{
    public static class RelayServer
    {
        [AllowNull]
        public static ConfigurationData Configuration { get; private set; }
        public class ConfigurationData : ServerConfigurationData
        {
            [JsonInclude, JsonPropertyName("Coordinator Hostname"), AllowNull]
            string CoordinatorHostname;

            [AllowNull]
            public IPAddress CoordinatorAddress { get; private set; }

            [JsonPropertyName("Realtime Thread Allowance")]
            public int RealtimeThreadAllowance { get; set; }

            public async Task Initialize()
            {
                //Resolve Hostname
                {
                    if (IPAddress.TryParse(CoordinatorHostname, out var IP) is false)
                    {
                        var collection = await Dns.GetHostAddressesAsync(CoordinatorHostname, AddressFamily.InterNetwork);
                        IP = collection[0];
                    }

                    CoordinatorAddress = IP;
                }

                if (RealtimeThreadAllowance is 0)
                    RealtimeThreadAllowance = Environment.ProcessorCount;
            }
        }

        public static class Realtime
        {
            [AllowNull]
            public static RoomThreadDispatcher ThreadDispatcher { get; private set; }

            public static void Start()
            {
                NetworkLog.Info($"Realtime Thread Allowance: {Configuration.RealtimeThreadAllowance}");

                ThreadDispatcher = new RoomThreadDispatcher(Configuration.RealtimeThreadAllowance, TimeSpan.FromMilliseconds(10));
            }

            public static Room CreateRoom(CreateRoomRequest request)
            {
                var instance = new Room(request);

                instance.Start(ThreadDispatcher);

                return instance;
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

                Server.Start(Constants.RelayMessagingPort);
            }

            static void CreateRoomHandler(MessagingPeer peer, ref CreateRoomRequest message)
            {
                var room = Realtime.CreateRoom(message);

                var response = new CreateRoomResponse(room.Transport.Port);

                peer.Send(response);
            }
        }

        static async Task Main(string[] args)
        {
            NetworkLog.UseConsole();

            await LoadConfig();

            ParseArguments(args);

            Messaging.Start();
            Realtime.Start();

            while (true) Console.ReadKey();
        }

        static async Task LoadConfig()
        {
            NetworkLog.Info($"Loading Configuration Data");

            Configuration = ServerConfigurationLoader.Load<ConfigurationData>();
            await Configuration.Initialize();

            NetworkLog.Info($"Coordinator Address: {Configuration.CoordinatorAddress}");
        }

        static void ParseArguments(string[] args) { }
    }
}
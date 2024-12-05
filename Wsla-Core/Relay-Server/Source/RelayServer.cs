using System.Net;
using System.Net.Sockets;
using System.Text.Json.Serialization;

namespace Wsla.Server
{
    public static class RelayServer
    {
        public static ConfigurationData Configuration { get; private set; }
        public class ConfigurationData : ServerConfigurationData
        {
            [JsonInclude, JsonPropertyName("Coordinator Hostname")]
            string CoordinatorHostname;

            public IPAddress CoordinatorAddress { get; private set; }

            [JsonPropertyName("Realtime Thread Allowance")]
            public int RealtimeThreadAllowance { get; set; }

            [JsonPropertyName("Realtime Fixed Time")]
            public ushort RealtimeFixedTime { get; set; }

            public ServerRegion Region { get; set; }

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
            public static RoomThreadDispatcher ThreadDispatcher { get; private set; }

            public static void Initialize()
            {
                NetworkLog.Info($"Realtime Thread Allowance: {Configuration.RealtimeThreadAllowance}");
                NetworkLog.Info($"Realtime Fixed Time: {Configuration.RealtimeFixedTime}ms");

                ThreadDispatcher = new RoomThreadDispatcher(Configuration.RealtimeThreadAllowance, TimeSpan.FromMilliseconds(Configuration.RealtimeFixedTime));
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
            public static MessagingServer Server { get; private set; }

            public static void Initialize()
            {
                Server = new MessagingServer();
            }

            public static void Start()
            {
                Server.Start(Constants.RelayMessagingPort);
            }
        }

        public static class Matchmaking
        {
            public static IPAddress LocalAddress { get; private set; }

            public static async Task Start()
            {
                Messaging.Server.Dispatcher.Register<CreateRoomRequest>(CreateRoomHandler);

                await Register();
            }

            async static Task Register()
            {
                using (var query = new MessagingQuery())
                {
                    var request = new RegisterRelayRequest(Configuration.Region);

                    var response = await query.Transport<RegisterRelayRequest, RegisterRelayResponse>(Configuration.CoordinatorAddress, Constants.CoordinatorMessagingPort, request);

                    if (response.IsError)
                        throw response.Error.ToException();

                    LocalAddress = response.Value.Address;

                    NetworkLog.Info($"Registered with Coordinator");
                }
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

            //Initialize
            {
                Messaging.Initialize();
                Realtime.Initialize();
            }

            //Start
            {
                Messaging.Start();
            }

            await Matchmaking.Start();

            while (true) Console.ReadKey();
        }

        static async Task LoadConfig()
        {
            NetworkLog.Info($"Loading Configuration Data");

            Configuration = ServerConfigurationLoader.Load<ConfigurationData>();
            await Configuration.Initialize();

            NetworkLog.Info($"Coordinator Address: {Configuration.CoordinatorAddress}");
            NetworkLog.Info($"Server Region: {Configuration.Region}");
        }

        static void ParseArguments(string[] args) { }
    }
}
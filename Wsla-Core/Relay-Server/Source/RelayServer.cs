using System.Net;
using System.Net.Sockets;
using System.Text.Json.Serialization;

namespace Wsla.Server
{
    public static class RelayServer
    {
        public static ConfigurationProperty Configuration { get; private set; }
        public class ConfigurationProperty : ServerConfigurationData
        {
            public IPAddress CoordinatorAddress { get; init; }

            public int RealtimeThreadAllowance { get; init; }
            public ushort RealtimeFixedTime { get; init; }

            public ServerRegion Region { get; init; }
            public int ID { get; init; }

            public IPAddress PublicAddress { get; init; }

            public static async Task<ConfigurationProperty> Create(Data data)
            {
                IPAddress CoordinatorAddress;

                //Resolve Coordinator Hostname
                {
                    CoordinatorAddress = await ResolveHostName(data.CoordinatorHostname);
                }

                IPAddress PublicAddress;

                //Resolve Public Address
                {
                    if (string.IsNullOrEmpty(data.PublicHostname))
                        PublicAddress = await FetchPublicAddress();
                    else
                        PublicAddress = await ResolveHostName(data.PublicHostname);
                }

                //Validate Realtime Thread Allowance
                {
                    if (data.RealtimeThreadAllowance is 0)
                        data.RealtimeThreadAllowance = Environment.ProcessorCount;
                }

                return new ConfigurationProperty()
                {
                    CoordinatorAddress = CoordinatorAddress,

                    ID = data.ID,
                    Region = data.Region,

                    PublicAddress = PublicAddress,

                    RealtimeFixedTime = data.RealtimeFixedTime,
                    RealtimeThreadAllowance = data.RealtimeThreadAllowance,
                };
            }

            static async Task<IPAddress> FetchPublicAddress()
            {
                var client = new HttpClient();

                var response = await client.GetStringAsync("https://ipinfo.io/ip");

                var address = IPAddress.Parse(response);

                return address;
            }
            static async ValueTask<IPAddress> ResolveHostName(string name)
            {
                if (IPAddress.TryParse(name, out var address))
                    return address;

                var collection = await Dns.GetHostAddressesAsync(name, AddressFamily.InterNetwork);
                return collection[0];
            }

            public class Data : ServerConfigurationData
            {
                [JsonInclude, JsonPropertyName("Coordinator Hostname")]
                public string CoordinatorHostname;

                [JsonInclude, JsonPropertyName("Realtime Thread Allowance")]
                public int RealtimeThreadAllowance;

                [JsonInclude, JsonPropertyName("Realtime Fixed Time")]
                public ushort RealtimeFixedTime;

                [JsonInclude, JsonPropertyName("Region")]
                public ServerRegion Region;

                [JsonInclude, JsonPropertyName("ID")]
                public int ID;

                [JsonInclude, JsonPropertyName("Public Hostname")]
                public string PublicHostname;
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

            public static Room CreateRoom(CreateRoomCommand request)
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
            public static async Task Start()
            {
                Messaging.Server.Dispatcher.RegisterAsync<CreateRoomCommand>(CreateRoomHandler);

                await Register();
            }

            async static Task Register()
            {
                using (var query = new MessagingQuery())
                {
                    var info = new RelayServerInfo(Configuration.Region, Configuration.ID, Configuration.PublicAddress);

                    var request = new RegisterRelayRequest(info);

                    var response = await query.Transport<RegisterRelayRequest, RegisterRelayResponse>(Configuration.CoordinatorAddress, Constants.CoordinatorMessagingPort, request);

                    if (response.IsError)
                        throw response.Error.ToException();

                    NetworkLog.Info($"Registered with Coordinator");
                }
            }

            static async Task CreateRoomHandler(MessagingPeer peer, CreateRoomCommand message)
            {
                var room = Realtime.CreateRoom(message);

                var response = new CreateRoomConfirmation(room.Transport.Port);

                await peer.Send(response);
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

            var data = ServerConfigurationLoader.Load<ConfigurationProperty.Data>();

            Configuration = await ConfigurationProperty.Create(data);

            NetworkLog.Info($"Coordinator Address: {Configuration.CoordinatorAddress}");
            NetworkLog.Info($"Server Region: {Configuration.Region}");
        }

        static void ParseArguments(string[] args) { }
    }
}
using GenHTTP.Api.Infrastructure;
using GenHTTP.Api.Protocol;
using GenHTTP.Engine.Internal;
using GenHTTP.Modules.Layouting;
using GenHTTP.Modules.Webservices;

using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

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

            public static void Init()
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

        public static class REST
        {
            public static class Server
            {
                static IServerHost Contract;

                public static void Init()
                {
                    var serializers = GenHTTP.Modules.Conversion.Serialization.Default(SharedAPI.JsonOptions);

                    var api = Layout.Create()
                        .AddService<Matchmaking.Endpoints>("/", serializers: serializers);

                    Contract = Host.Create()
                        .Handler(api)
                        .Bind(IPAddress.Any, Constants.RelayMessagingPort)
                        .Development()
                        .Console();
                }
                public static async void Start()
                {
                    await Contract.StartAsync();
                }
            }

            public static HttpRequester Client { get; private set; }

            public static void Init()
            {
                Server.Init();

                Client = new HttpRequester(SharedAPI.JsonOptions);
            }
            public static void Start()
            {
                Server.Start();
            }
        }

        public static class Matchmaking
        {
            public class Endpoints
            {
                [ResourceMethod(RequestMethod.Post, Constants.RestRoutes.CreateRoom)]
                public CreateRoomConfirmation CreateRoomHandler(CreateRoomCommand message)
                {
                    var room = Realtime.CreateRoom(message);

                    return new CreateRoomConfirmation(room.ID, room.Transport.Port);
                }
            }

            public static async Task Start()
            {
                await RegisterWithCoordinator();
            }

            public static async Task RegisterWithCoordinator()
            {
                var info = new RelayServerInfo(Configuration.Region, Configuration.ID, Configuration.PublicAddress);

                var request = new RegisterRelayRequest(info);

                while (true)
                {
                    var response = await REST.Client.PUT(Configuration.CoordinatorAddress, Constants.CoordinatorMessagingPort, Constants.RestRoutes.RegisterRelay, request);

                    if (response.IsError)
                    {
                        NetworkLog.Error($"Failed to Register With Coordinator, Error: {response.Error}");
                        await Task.Delay(TimeSpan.FromSeconds(1));
                        continue;
                    }

                    break;
                }
            }

            public static async void RemoveRoomFromCoordinator(Guid id)
            {
                //MOBO: deal with disconnects

                var request = new RemoveRoomRequest(Configuration.PublicAddress, id);

                var response = await REST.Client.PUT(Configuration.CoordinatorAddress, Constants.CoordinatorMessagingPort, Constants.RestRoutes.RemoveRoom, request);
            }
        }

        static async Task Main(string[] args)
        {
            Console.Title = "Relay Server";

            NetworkLog.UseConsole();

            await LoadConfig();

            ParseArguments(args);

            //Initialize
            {
                REST.Init();
                Realtime.Init();
            }

            //Start
            {
                await Matchmaking.Start();

                REST.Start();
            }

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
using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Wsla.Serialization;

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

            public static Room CreateRoom(CreateRoomCommand command)
            {
                var instance = new Room(command.ID, command.Parameters);

                instance.Start(ThreadDispatcher);

                return instance;
            }
        }

        public static class Messaging
        {
            static MessagingClient Client;

            public static async Task Start()
            {
                Client = new MessagingClient();

                var response = await Client.Connect(Configuration.CoordinatorAddress, Constants.CoordinatorMessagingPort);

                if (response.IsError)
                    throw response.Error.ToException();

                Matchmaking.RegisterMessages(Client);

                Matchmaking.RegisterWithCoordinator();
            }

            public static void Send<[NetworkSerializationMarker] T>(T message) => Client.SendMessage(message);
        }

        public static class Matchmaking
        {
            public static void RegisterMessages(MessagingClient client)
            {
                client.Dispatcher.Register<CreateRoomCommand>(CreateRoomHandler);
            }

            public static void RegisterWithCoordinator()
            {
                var info = new RelayServerInfo(Configuration.Region, Configuration.ID, Configuration.PublicAddress);
                var request = new RegisterRelayRequest(info);

                Messaging.Send(request);
            }

            static void CreateRoomHandler(ref CreateRoomCommand message)
            {
                var room = Realtime.CreateRoom(message);

                var confirmation = new CreateRoomConfirmation(room.ID, room.Transport.Port);

                Messaging.Send(confirmation);
            }

            public static void RemoveRoomFromCoordinator(Guid id)
            {
                var request = new RemoveRoomRequest(Configuration.PublicAddress, id);
                Messaging.Send(request);
            }
        }

        static async Task Main(string[] args)
        {
            Console.Title = "Relay Server";

            NetworkLog.UseConsole();

            await LoadConfig();

            ParseArguments(args);

            Realtime.Init();

            await Messaging.Start();

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
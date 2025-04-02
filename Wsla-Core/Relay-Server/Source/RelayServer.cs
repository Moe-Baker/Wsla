using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using Wsla.Serialization;

namespace Wsla.Server
{
    public static class RelayServer
    {
        static async Task Main(string[] args)
        {
            Console.Title = "Relay Server";

            NetworkLog.UseConsole();

            await LoadConfig();

            Realtime.Init();
            Matchmaking.Init();

            await Messaging.Start();

            while (true) Console.ReadKey();
        }

        public static ConfigurationProperty Configuration { get; private set; }
        public class ConfigurationProperty
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

                int RealtimeThreadAllowance;
                //Validate Realtime Thread Allowance
                {
                    if (data.RealtimeThreadAllowance is 0)
                        RealtimeThreadAllowance = Environment.ProcessorCount;
                    else
                        RealtimeThreadAllowance = data.RealtimeThreadAllowance;
                }

                return new ConfigurationProperty()
                {
                    CoordinatorAddress = CoordinatorAddress,

                    ID = data.ID,
                    Region = data.Region,

                    PublicAddress = PublicAddress,

                    RealtimeFixedTime = data.RealtimeFixedTime,
                    RealtimeThreadAllowance = RealtimeThreadAllowance,
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

            public class Data
            {
                [JsonPropertyName("Coordinator Hostname")]
                public string CoordinatorHostname;

                [JsonPropertyName("Realtime Thread Allowance")]
                public int RealtimeThreadAllowance;

                [JsonPropertyName("Realtime Fixed Time")]
                public ushort RealtimeFixedTime;

                [JsonPropertyName("Region")]
                public ServerRegion Region;

                [JsonPropertyName("ID")]
                public int ID;

                [JsonPropertyName("Public Hostname")]
                public string PublicHostname;
            }
        }
        static async Task LoadConfig()
        {
            NetworkLog.Info($"Loading Configuration Data");

            var data = ServerConfigurationLoader.Load<ConfigurationProperty.Data>();

            Configuration = await ConfigurationProperty.Create(data);

            NetworkLog.Info($"Coordinator Address: {Configuration.CoordinatorAddress}");
            NetworkLog.Info($"Server Region: {Configuration.Region}");
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
                var instance = new Room(command.ApplicationID, command.RoomID, command.Parameters);

                instance.Start(ThreadDispatcher);

                return instance;
            }
        }

        public static class Messaging
        {
            static volatile MessagingClient Client;
            public static bool IsConnected
            {
                get
                {
                    if (Client == null)
                        return false;

                    return Client.GetState() is MessagingSocketState.Connected;
                }
            }

            static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(1f);

            public static async Task Start()
            {
                await Wireup();
            }

            static async Task Wireup()
            {
                while (true)
                {
                    var response = await Connect().ToResponse();

                    switch (response.Type)
                    {
                        case WslaResponseResponseType.Success:
                        {
                            var client = response.Value;

                            NetworkLog.Info($"Messaging Client Connected");

                            client.RegisterStopCallback(ClientDisconnectCallback);

                            return;
                        }

                        case WslaResponseResponseType.Error:
                        {
                            var exception = response.Error;

                            NetworkLog.Error($"Messaging Client Connect Exception: {exception.Message}");

                            await Task.Delay(ReconnectDelay);

                            continue;
                        }
                    }
                }
            }

            static void ClientDisconnectCallback(MessagingConnection connection, MessagingSocketDisconnectReason reason)
            {
                if (reason is MessagingSocketDisconnectReason.LocalClose)
                {
                    NetworkLog.Info($"Messaging Socket Closed");
                    return;
                }

                NetworkLog.Error($"Messaging Socket Disconnected, Reconnecting");
                Reconnect().Forget();
            }

            static async Task Reconnect()
            {
                await Task.Delay(ReconnectDelay);

                await Wireup();
            }

            static async Task<MessagingClient> Connect()
            {
                Client = new MessagingClient();

                var response = await Client.Connect(Configuration.CoordinatorAddress, Constants.CoordinatorMessagingPort);

                if (response.IsError)
                    throw response.Error.ToException();

                Matchmaking.MessageHandlers.RegisterHandlers(Client);

                Matchmaking.RegisterRelay();

                return Client;
            }

            public static void Send<[NetworkSerializationMarker] T>(T message) => Client.SendMessage(message);
            public static Task SendAsync<[NetworkSerializationMarker] T>(T message) => Client.SendMessageAsync(message);
        }

        public static class Matchmaking
        {
            static List<Room> Rooms;

            public static class Updates
            {
                static ChangesCollector<Guid, UpdateRoomParameters> Changes;

                static readonly TimeSpan SendInterval = TimeSpan.FromSeconds(2);

                public static void Init()
                {
                    Changes = new ChangesCollector<Guid, UpdateRoomParameters>(UpdateRoomParameters.Merge);
                    Poll().Forget();
                }

                public static void Add(Guid id, UpdateRoomParameters parameters)
                {
                    lock (Changes)
                    {
                        Changes.Add(id, parameters);
                    }
                }
                public static void Remove(Guid id)
                {
                    lock (Changes)
                    {
                        Changes.Remove(id);
                    }
                }

                public static async Task Poll()
                {
                    var requests = new List<UpdateRoomRequest>(40);

                    while (true)
                    {
                        await Task.Delay(SendInterval);

                        //Collect Changes
                        lock (Changes)
                        {
                            if (Changes.TryRead(out var changes) is false)
                                continue;

                            requests.Clear();

                            foreach (var (id, parameters) in changes)
                            {
                                var request = new UpdateRoomRequest(id, parameters);
                                requests.Add(request);
                            }

                            Changes.Clear();
                        }

                        //Send Request
                        {
                            var request = new UpdateRoomsRequest(requests);

                            await Messaging.SendAsync(request);
                        }
                    }
                }
            }

            public static class MessageHandlers
            {
                public static void RegisterHandlers(MessagingClient client)
                {
                    client.Dispatcher.Register<CreateRoomCommand>(MessageHandlers.CreateRoomHandler);
                }

                public static void CreateRoomHandler(ref CreateRoomCommand message)
                {
                    var room = Realtime.CreateRoom(message);

                    RegisterRoom(room);

                    var confirmation = new CreateRoomConfirmation(room.RoomID, room.Transport.Port);
                    Messaging.Send(confirmation);
                }
            }

            public static void Init()
            {
                Rooms = new(100);
                Updates.Init();
            }

            public static void RegisterRelay()
            {
                var info = new RelayServerInfo(Configuration.Region, Configuration.ID, Configuration.PublicAddress);

                var rooms = new List<RoomMatchmakerEntryData>();
                ListRooms(rooms);

                var request = new RegisterRelayRequest(info, rooms);

                Messaging.Send(request);
            }

            public static void RegisterRoom(Room room)
            {
                lock (Rooms)
                {
                    Rooms.Add(room);
                }
            }
            public static bool UnregisterRoom(Room room)
            {
                lock (Rooms)
                {
                    if (Rooms.Remove(room) is false)
                        return false;
                }

                Updates.Remove(room.RoomID);

                var request = new RemoveRoomRequest(room.RoomID);
                Messaging.Send(request);

                return true;
            }

            public static void ListRooms(List<RoomStateInfo> list)
            {
                lock (Rooms)
                {
                    list.EnsureCapacity(Rooms.Count);

                    foreach (var room in Rooms)
                    {
                        var state = room.Properties.ReadState();
                        list.Add(state);
                    }
                }
            }
            public static void ListRooms(List<RoomMatchmakerEntryData> list)
            {
                lock (Rooms)
                {
                    list.EnsureCapacity(Rooms.Count);

                    foreach (var room in Rooms)
                    {
                        var state = room.Properties.ReadState();
                        var data = new RoomMatchmakerEntryData(room.ApplicationID, room.RoomID, room.Transport.Port, room.Properties.Privacy, state);
                        list.Add(data);
                    }
                }
            }
        }
    }
}
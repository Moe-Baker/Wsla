using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using Wsla.Serialization;
using System.ComponentModel;

namespace Wsla.Server
{
    public static class RelayServer
    {
        static async Task Main(string[] args)
        {
            Console.Title = "Relay Server";

            NetworkLog.UseConsole();

            NetworkLog.Info($"Working Directory: {System.IO.Directory.GetCurrentDirectory()}");

            await LoadConfig();

            //Load Plugins
            {
                var system = new RelayPluginSystem();
                system.LoadAll();
            }

            //Initialize
            {
                Time.Init();
                Realtime.Init();
                Matchmaking.Init();
            }

            //Start
            {
                await Messaging.Start();
            }

            while (true) Console.ReadKey();
        }

        public static ConfigurationProperty Configuration { get; private set; }
        public class ConfigurationProperty
        {
            public IPAddress CoordinatorAddress { get; init; }

            public int RealtimeThreadAllowance { get; init; }
            public ushort RealtimeFixedTime { get; init; }

            public ServerRegion Region { get; init; }

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
                    if (data.PublicHostname is null)
                        PublicAddress = await FetchPublicAddress();
                    else
                        PublicAddress = await ResolveHostName(data.PublicHostname);
                }

                data.RealtimeThreadAllowance ??= Environment.ProcessorCount;
                data.RealtimeFixedTime ??= Data.DefaultRealtimeFixedTime;

                return new ConfigurationProperty()
                {
                    CoordinatorAddress = CoordinatorAddress,

                    Region = data.Region,

                    PublicAddress = PublicAddress,

                    RealtimeFixedTime = data.RealtimeFixedTime.Value,
                    RealtimeThreadAllowance = data.RealtimeFixedTime.Value,
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
                [JsonRequired]
                [Description("Allows Both Host Names & IPs")]
                public string CoordinatorHostname;

                [Description("Thread Count to Use for Realtime Rooms, Defaults to Assign System's Thread Count")]
                public int? RealtimeThreadAllowance;

                [Description("The Timestep Duration for Realtime Rooms, Defaults to 10")]
                public ushort? RealtimeFixedTime;
                public const ushort DefaultRealtimeFixedTime = 10;

                [JsonRequired]
                [Description("The Region to List This Server in")]
                public ServerRegion Region;

                [Description($"The Public Host Name for This Machine, Defaults to Automatically Fetching The Machine's Public IPv4 Address")]
                public string PublicHostname;
            }
        }
        static async Task LoadConfig()
        {
            NetworkLog.Info($"Loading Configuration Data");

#if DEBUG
            ServerConfigurationLoader.Schema.Write<ConfigurationProperty.Data>("Schema/Configuration.json");
#endif

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

        public static class Time
        {
            static Stopwatch Timer;

            public static void Init()
            {
                Timer = Stopwatch.StartNew();
            }

            public static TimeSpan GetElapsed() => Timer.Elapsed;
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

                    var timer = new PeriodicTimer(SendInterval);

                    while (true)
                    {
                        await timer.WaitForNextTickAsync();

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
                var info = new RelayServerRegistrationInfo(Configuration.Region, Configuration.PublicAddress);

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
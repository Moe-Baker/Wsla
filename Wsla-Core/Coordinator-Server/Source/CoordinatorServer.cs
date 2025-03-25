using GenHTTP.Api.Content;
using GenHTTP.Api.Infrastructure;
using GenHTTP.Api.Protocol;
using GenHTTP.Engine.Internal;
using GenHTTP.Modules.Layouting;
using GenHTTP.Modules.Webservices;

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Wsla.Server
{
    public static class CoordinatorServer
    {
        static async Task Main(string[] args)
        {
            Console.Title = "Coordinator Server";

            NetworkLog.UseConsole();

            await LoadConfig();

            //Initialize
            {
                Messaging.Initialize();
                REST.Init();
                Matchmaking.Init();
            }

            //Start
            {
                Messaging.Start();
                REST.Start();
            }

            while (true) Console.ReadKey();
        }

        public static ConfigurationProperty Configuration { get; private set; }
        public class ConfigurationProperty : ServerConfigurationData
        {
            public static async Task<ConfigurationProperty> Create(Data data)
            {
                return new ConfigurationProperty()
                {

                };
            }

            public class Data : ServerConfigurationData
            {

            }
        }
        static async Task LoadConfig()
        {
            NetworkLog.Info($"Loading Configuration Data");

            var data = ServerConfigurationLoader.Load<ConfigurationProperty.Data>();

            Configuration = await ConfigurationProperty.Create(data);
        }

        public static class REST
        {
            static IServerHost Contract;

            public static void Init()
            {
                var serializers = GenHTTP.Modules.Conversion.Serialization.Default(SharedAPI.JsonOptions);

                var api = Layout.Create()
                    .AddService<Matchmaking.HttpEndpoints>("/", serializers: serializers);

                Contract = Host.Create()
                    .Handler(api)
                    .Bind(IPAddress.Any, Constants.CoordinatorHttpPort)
                    .Development()
                    .Console();
            }
            public static void Start()
            {
                Contract.StartAsync().Forget();
            }
        }

        public static class Messaging
        {
            public static MessagingServer Server { get; private set; }

            public static void Initialize()
            {
                Server = new MessagingServer();

                Matchmaking.MessageHandlers.RegisterHandlers(Server);
            }

            public static void Start()
            {
                Server.Start(Constants.CoordinatorMessagingPort);
            }
        }

        public static class Matchmaking
        {
            public static List<Server> Servers { get; private set; }
            public class Server : IDisposable
            {
                public RelayServerInfo Info { get; }

                public MessagingPeer MessagingPeer { get; }

                public ServerRegion Region => Info.Region;
                public IPAddress Address => Info.Address;

                Dictionary<Guid, Room> Rooms;

                public int Occupancy;
                void ModifyOccupancy(int modifier)
                {
                    var value = Interlocked.Add(ref Occupancy, modifier);
                    NetworkLog.Trace($"Relay {this} Occupancy Changed to {value}");

                    Matchmaking.ModifyOccupancy(modifier);
                }

                public bool RegisterRoom(Guid id, ushort port, CreateRoomParameters parameters)
                {
                    lock (Rooms)
                    {
                        if (Rooms.ContainsKey(id))
                        {
                            NetworkLog.Warning($"Room with ID {id} Already Registered");
                            return false;
                        }

                        var room = new Room(this, port, parameters.Name, parameters.Capacity);

                        Rooms.Add(id, room);

                        return true;
                    }
                }
                public bool UnregisterRoom(Guid id)
                {
                    lock (Rooms)
                    {
                        if (Rooms.Remove(id, out var room) is false)
                        {
                            NetworkLog.Warning($"No Room with ID {id} Registered");
                            return false;
                        }

                        ModifyOccupancy(-room.Occupancy);
                        return true;
                    }
                }

                public void UpdateRoom(Guid id, UpdateRoomParameters parameters)
                {
                    lock (Rooms)
                    {
                        if (Rooms.TryGetValue(id, out var room) is false)
                        {
                            NetworkLog.Error($"No Room With ID {id} Found");
                            return;
                        }

                        UpdateRoom(room, parameters);
                    }
                }
                public void UpdateRooms(IEnumerable<UpdateRoomRequest> requests)
                {
                    lock (Rooms)
                    {
                        foreach (var request in requests)
                        {
                            if (Rooms.TryGetValue(request.ID, out var room) is false)
                            {
                                NetworkLog.Error($"No Room With ID {request.ID} Found to Update");
                                continue;
                            }

                            UpdateRoom(room, request.Parameters);
                        }
                    }
                }
                void UpdateRoom(Room room, UpdateRoomParameters parameters)
                {
                    //Update Occupancy
                    if (parameters.Occupancy.HasValue)
                    {
                        var delta = (parameters.Occupancy.Value - room.Occupancy);

                        ModifyOccupancy(delta);
                    }

                    room.UpdateRoom(parameters);
                }

                public void ListRooms(List<RoomListEntryInfo> list)
                {
                    lock (Rooms)
                    {
                        list.EnsureCapacity(Rooms.Count);

                        foreach (var (id, room) in Rooms)
                        {
                            var connection = new RoomConnectionInfo(Address, room.Port);

                            var name = room.Name.ToString();
                            var capacity = room.Capacity;
                            var occupancy = room.Occupancy;

                            var entry = new RoomListEntryInfo(name, capacity, occupancy, connection);

                            list.Add(entry);
                        }
                    }
                }

                public bool TryFindFreeRoom(out Room target)
                {
                    lock (Rooms)
                    {
                        foreach (var (id, room) in Rooms)
                        {
                            if (room.IsFull)
                                continue;

                            target = room;
                            return true;
                        }
                    }

                    target = default;
                    return false;
                }

                public void Dispose()
                {
                    Matchmaking.ModifyOccupancy(-Occupancy);
                }

                public override string ToString() => Info.ToString();

                public Server(RelayServerInfo Info, MessagingPeer MessagingPeer)
                {
                    this.Info = Info;
                    this.MessagingPeer = MessagingPeer;

                    Rooms = new();
                }

                public static Server Create(RegisterRelayRequest request, MessagingPeer peer)
                {
                    var server = new Server(request.Info, peer);

                    if (request.Rooms is not null)
                    {
                        server.Rooms.EnsureCapacity(request.Rooms.Count);

                        foreach (var entry in request.Rooms)
                        {
                            var room = Room.Create(server, entry);
                            server.Rooms.Add(entry.ID, room);

                            server.Occupancy += room.Occupancy;
                        }
                    }

                    return server;
                }
            }
            public class Room
            {
                public Server Server { get; }

                public ushort Port { get; }

                public FixedString40 Name;

                public byte Capacity;

                public byte Occupancy;
                public bool IsFull => Occupancy >= Capacity;

                public RoomConnectionInfo GetConnectionInfo() => new RoomConnectionInfo(Server.Address, Port);

                public void UpdateRoom(UpdateRoomParameters parameters)
                {
                    if (parameters.Name.HasValue)
                        Name = parameters.Name.Value;

                    if (parameters.Occupancy.HasValue)
                        Occupancy = parameters.Occupancy.Value;
                }

                public Room(Server Server, ushort Port, FixedString40 Name, byte Capacity, byte Occupancy)
                {
                    this.Server = Server;
                    this.Port = Port;
                    this.Name = Name;
                    this.Capacity = Capacity;
                    this.Occupancy = Occupancy;
                }
                public Room(Server Server, ushort Port, FixedString40 Name, byte Capacity) : this(Server, Port, Name, Capacity, Occupancy: 0) { }

                public static Room Create(Server server, RoomMatchmakerEntryData data)
                {
                    return new Room(server, data.Port, data.State.Name, data.State.Capacity, data.State.Occupancy);
                }
            }

            public static int Occupancy;
            static void ModifyOccupancy(int modifier)
            {
                var value = Interlocked.Add(ref Occupancy, modifier);
                NetworkLog.Trace($"Matchmaking Occupancy Changed to {value}");
            }

            public static bool TryFindFreeServer(ServerRegion? region, out Server server)
            {
                lock (Servers)
                {
                    var marker = (Found: false, Server: default(Server), Occupancy: int.MaxValue);

                    for (int i = 0; i < Servers.Count; i++)
                    {
                        server = Servers[i];

                        if (region.HasValue && region.Value != server.Region)
                            continue;

                        if (server.Occupancy < marker.Occupancy)
                            marker = (true, server, server.Occupancy);
                    }

                    server = marker.Server;
                    return marker.Found;
                }
            }

            public static bool TryFindFreeRoom(ServerRegion? region, out Room room)
            {
                lock (Servers)
                {
                    for (int i = 0; i < Servers.Count; i++)
                    {
                        var server = Servers[i];

                        if (region.HasValue && region.Value != server.Region)
                            continue;

                        if (server.TryFindFreeRoom(out room))
                            return true;
                    }
                }

                room = default;
                return false;
            }

            static TaskCompletionQueue<Guid, CreateRoomConfirmation> RoomCreationQueue;

            public class HttpEndpoints
            {
                [ResourceMethod(RequestMethod.Get, Constants.RestRoutes.ListRegions)]
                public List<ServerRegion> ListRegions()
                {
                    List<ServerRegion> list;

                    lock (Servers)
                    {
                        list = new(Servers.Count);

                        foreach (var server in Servers)
                        {
                            if (list.Contains(server.Region))
                                continue;

                            list.Add(server.Region);
                        }
                    }

                    return list;
                }

                [ResourceMethod(RequestMethod.Post, Constants.RestRoutes.CreateRoom)]
                public async Task<RoomConnectionInfo> CreateRoom(CreateRoomRequest message)
                {
                    //Find Region
                    if (TryFindFreeServer(message.Region, out var server) is false)
                        throw new ProviderException(ResponseStatus.BadRequest, $"No Region {message.Region} Found");

                    var id = Guid.NewGuid();

                    var operation = RoomCreationQueue.Create(id, server.MessagingPeer.DisconnectCancellationToken);

                    //Forward Request to Relay
                    {
                        var request = new CreateRoomCommand(id, message.Parameters);
                        server.MessagingPeer.SendMessage(request);
                    }

                    CreateRoomConfirmation Confirmation;

                    //Wait for Response from Relay
                    try
                    {
                        Confirmation = await operation.Task;
                    }
                    catch (OperationCanceledException)
                    {
                        throw new ProviderException(ResponseStatus.InternalServerError, $"Room Creation on Relay Failed");
                    }

                    server.RegisterRoom(id, Confirmation.Port, message.Parameters);

                    return new RoomConnectionInfo(server.Address, Confirmation.Port);
                }

                [ResourceMethod(RequestMethod.Post, Constants.RestRoutes.ListRooms)]
                public List<RoomListEntryInfo> ListRooms(ListRoomsRequest request)
                {
                    var list = new List<RoomListEntryInfo>();

                    lock (Servers)
                    {
                        foreach (var server in Servers)
                        {
                            if (server.Region != request.Region)
                                continue;

                            server.ListRooms(list);
                        }
                    }

                    return list;
                }

                [ResourceMethod(RequestMethod.Post, Constants.RestRoutes.FindRoom)]
                public async Task<RoomConnectionInfo?> FindRoom(FindRoomRequest request)
                {
                    //Try Find Existing Room
                    {
                        if (TryFindFreeRoom(request.Region, out var room))
                            return room.GetConnectionInfo();
                    }

                    //Try Create Room
                    if (request.CreateRoom.HasValue)
                    {
                        var create = new CreateRoomRequest(request.Region, request.CreateRoom.Value);

                        return await CreateRoom(create);
                    }

                    return null;
                }
            }

            public class MessageHandlers
            {
                public static void RegisterHandlers(MessagingServer server)
                {
                    server.Dispatcher.RegisterSync<RegisterRelayRequest>(MessageHandlers.RegisterRelayHandler);
                    server.Dispatcher.RegisterSync<CreateRoomConfirmation>(MessageHandlers.CreateRoomConfirmationHandler);
                    server.Dispatcher.RegisterSync<RemoveRoomRequest>(MessageHandlers.RemoveRoomHandler);
                    server.Dispatcher.RegisterSync<UpdateRoomsRequest>(MessageHandlers.UpdateRoomsHandler);
                }

                public static void RegisterRelayHandler(MessagingPeer peer, ref RegisterRelayRequest message)
                {
                    NetworkLog.Info($"Registering ({message.Info.Region}) Relay Server on Address: {message.Info.Address}");

                    var server = Server.Create(message, peer);
                    peer.Tag = server;

                    lock (Servers)
                    {
                        Servers.Add(server);
                    }

                    peer.RegisterStopCallback(() => RelayStoppedCallback(server));
                }

                public static void CreateRoomConfirmationHandler(MessagingPeer peer, ref CreateRoomConfirmation message)
                {
                    RoomCreationQueue.Fulfill(message.ID, message);
                }

                public static void RemoveRoomHandler(MessagingPeer peer, ref RemoveRoomRequest message)
                {
                    if (TryReadTag(peer, out var server) is false)
                        return;

                    server.UnregisterRoom(message.RoomID);
                }

                public static void UpdateRoomsHandler(MessagingPeer peer, ref UpdateRoomsRequest message)
                {
                    if (TryReadTag(peer, out var server) is false)
                        return;

                    server.UpdateRooms(message.Requests);
                }
            }

            public static void Init()
            {
                Servers = new(10);

                RoomCreationQueue = new(100);
            }

            static void RelayStoppedCallback(Server server)
            {
                NetworkLog.Info($"Removing {server} Relay Server");

                lock (Servers)
                {
                    if (Servers.Remove(server) is false)
                    {
                        NetworkLog.Warning($"No Relay Server {server} Found to Remove");
                        return;
                    }

                    server.Dispose();
                }
            }

            static bool TryReadTag(MessagingPeer peer, out Server server)
            {
                if (peer.Tag is not Server)
                {
                    NetworkLog.Warning($"Peer {peer} not Tagged as Relay Server");

                    server = default;
                    return false;
                }

                server = peer.Tag as Server;
                return true;
            }
        }
    }
}
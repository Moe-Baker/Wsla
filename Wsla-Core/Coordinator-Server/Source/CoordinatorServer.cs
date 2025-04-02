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
        public class ConfigurationProperty
        {
            public ApplicationsData Applications;
            public class ApplicationsData
            {
                public string[] Names { get; }

                public bool TryGetID(ReadOnlySpan<char> name, out ApplicationID id)
                {
                    for (byte i = 0; i < Names.Length; i++)
                    {
                        if (MemoryExtensions.Equals(Names[i], name, StringComparison.OrdinalIgnoreCase))
                        {
                            id = new ApplicationID(i);
                            return true;
                        }
                    }

                    id = default;
                    return false;
                }

                public ApplicationsData(Data.ApplicationData[] data)
                {
                    Names = new string[data.Length];

                    for (int i = 0; i < Names.Length; i++)
                        Names[i] = data[i].Name;
                }
            }

            public static async Task<ConfigurationProperty> Create(Data data)
            {
                return new ConfigurationProperty()
                {
                    Applications = new(data.Applications)
                };
            }

            public class Data : ServerConfigurationData
            {
                public ApplicationData[] Applications;
                public struct ApplicationData
                {
                    public string Name;
                }
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
            public static class Browser
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
                    public void ModifyOccupancy(int modifier)
                    {
                        var value = Interlocked.Add(ref Occupancy, modifier);
                        NetworkLog.Trace($"Relay {this} Occupancy Changed to {value}");
                    }

                    public bool CreateRoom(ApplicationID application, Guid id, ushort port, CreateRoomParameters parameters, int reservations)
                    {
                        lock (Rooms)
                        {
                            if (Rooms.ContainsKey(id))
                            {
                                NetworkLog.Warning($"Room with ID {id} Already Registered");
                                return false;
                            }

                            var room = new Room(this, application, port, parameters.Name, parameters.Capacity, 0, parameters.Privacy);
                            Rooms.Add(id, room);

                            room.MakeJoinReservation(reservations);

                            return true;
                        }
                    }

                    public bool RemoveRoom(Guid id)
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

                                room.UpdateRoom(request.Parameters);
                            }
                        }
                    }

                    public void ListRooms(ApplicationID application, List<RoomListEntryInfo> list)
                    {
                        lock (Rooms)
                        {
                            foreach (var (id, room) in Rooms)
                            {
                                if (room.Privacy is RoomPrivacy.Private)
                                    continue;

                                if (room.Application != application)
                                    continue;

                                var connection = new RoomConnectionInfo(Address, room.Port);

                                var name = room.Name;
                                var capacity = room.Capacity;
                                var occupancy = room.Occupancy;

                                var entry = new RoomListEntryInfo(name, capacity, occupancy, connection);

                                list.Add(entry);
                            }
                        }
                    }

                    public bool TryReserveJoin(ApplicationID application, int capacity, out Room target)
                    {
                        lock (Rooms)
                        {
                            foreach (var (id, room) in Rooms)
                            {
                                if (room.Privacy is RoomPrivacy.Private)
                                    continue;

                                if (room.Application != application)
                                    continue;

                                var vacancy = room.CheckVacancy();

                                if (vacancy >= capacity)
                                {
                                    room.MakeJoinReservation(capacity);

                                    target = room;
                                    return true;
                                }
                            }
                        }

                        target = default;
                        return false;
                    }

                    public void Dispose() { }

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

                        if (request.Rooms?.Count > 0)
                        {
                            server.Rooms.EnsureCapacity(request.Rooms.Count);

                            var occupancy = 0;

                            foreach (var entry in request.Rooms)
                            {
                                var room = Room.Create(server, entry);
                                server.Rooms.Add(entry.ID, room);

                                occupancy += room.Occupancy;
                            }

                            server.ModifyOccupancy(occupancy);
                        }

                        return server;
                    }
                }
                public class Room
                {
                    public Server Server { get; }

                    public ApplicationID Application { get; }

                    public ushort Port { get; }

                    public FixedString<FS20> Name { get; }

                    public byte Capacity;

                    public byte Occupancy;
                    public bool IsFull => Occupancy >= Capacity;

                    public RoomPrivacy Privacy;

                    public bool IsLocked;
                    void Lock()
                    {
                        IsLocked = true;
                        Privacy = RoomPrivacy.Private;
                    }

                    TimedReservationCollection JoinReservations;
                    public void MakeJoinReservation(int capacity) => JoinReservations.ReserveCapacity(capacity);

                    public int CheckVacancy()
                    {
                        var total = Occupancy + JoinReservations.CalculateCapacity();

                        var vacancy = Capacity - total;
                        if (vacancy < 0) vacancy = 0;

                        return vacancy;
                    }

                    public RoomConnectionInfo GetConnectionInfo() => new RoomConnectionInfo(Server.Address, Port);

                    public void UpdateRoom(UpdateRoomParameters parameters)
                    {
                        //Lock
                        if (parameters.Lock)
                        {
                            Lock();
                        }

                        //Free Reservations
                        if (parameters.Joins > 0)
                        {
                            JoinReservations.FreeCapacity(parameters.Joins);
                        }

                        //Update Occupancy
                        if (parameters.Occupancy.HasValue)
                        {
                            var delta = (parameters.Occupancy.Value - Occupancy);
                            Server.ModifyOccupancy(delta);

                            Occupancy = parameters.Occupancy.Value;
                        }
                    }

                    public Room(Server Server, ApplicationID Application, ushort Port, FixedString<FS20> Name, byte Capacity, byte Occupancy, RoomPrivacy Privacy)
                    {
                        this.Application = Application;
                        this.Server = Server;
                        this.Port = Port;
                        this.Name = Name;
                        this.Capacity = Capacity;
                        this.Occupancy = Occupancy;
                        this.Privacy = Privacy;

                        IsLocked = false;

                        JoinReservations = new TimedReservationCollection(TimeSpan.FromSeconds(10));
                    }

                    public static Room Create(Server server, RoomMatchmakerEntryData data)
                    {
                        var state = data.State;

                        return new Room(server, data.Application, data.Port, state.Name, state.Capacity, state.Occupancy, data.Privacy);
                    }
                }

                public static void Init()
                {
                    Servers = new(10);
                }

                public static void RegisterServer(MessagingPeer peer, RegisterRelayRequest message)
                {
                    var server = Server.Create(message, peer);
                    peer.Tag = server;

                    lock (Servers)
                    {
                        Servers.Add(server);
                    }

                    peer.RegisterStopCallback(RelayStoppedCallback);
                }
                static void RelayStoppedCallback(MessagingConnection connection, MessagingSocketDisconnectReason reason)
                {
                    if (TryReadTag(connection, out var server) is false)
                        return;

                    NetworkLog.Info($"Removing {server} Relay Server, Disconnect: {reason}");

                    UnregisterServer(server);
                }

                static void UnregisterServer(Server server)
                {
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

                public static bool TryFindFreeServer(SparseArray<ServerRegion> regions, out Server server)
                {
                    lock (Servers)
                    {
                        var marker = (Found: false, Server: default(Server), Occupancy: int.MaxValue);

                        for (int i = 0; i < Servers.Count; i++)
                        {
                            server = Servers[i];

                            if (regions.Contains(server.Region) is false)
                                continue;

                            if (server.Occupancy < marker.Occupancy)
                                marker = (true, server, server.Occupancy);
                        }

                        server = marker.Server;
                        return marker.Found;
                    }
                }
                public static bool TryFindFreeRoom(ApplicationID application, SparseArray<ServerRegion> regions, int capacity, out Room room)
                {
                    lock (Servers)
                    {
                        for (int i = 0; i < Servers.Count; i++)
                        {
                            var server = Servers[i];

                            if (regions.Contains(server.Region) is false)
                                continue;

                            if (server.TryReserveJoin(application, capacity, out room))
                                return true;
                        }
                    }

                    room = default;
                    return false;
                }

                public static void ListRegions(List<ServerRegion> list)
                {
                    lock (Servers)
                    {
                        foreach (var server in Servers)
                        {
                            if (list.Contains(server.Region))
                                continue;

                            list.Add(server.Region);
                        }
                    }
                }
                public static void ListRooms(ApplicationID application, SparseArray<ServerRegion> regions, List<RoomListEntryInfo> list)
                {
                    lock (Servers)
                    {
                        foreach (var server in Servers)
                        {
                            if (regions.Contains(server.Region) is false)
                                continue;

                            server.ListRooms(application, list);
                        }
                    }
                }

                public static bool TryReadTag(MessagingConnection connection, out Server server)
                {
                    if (connection.Tag is not Server)
                    {
                        NetworkLog.Warning($"Peer {connection} not Tagged as Relay Server");

                        server = default;
                        return false;
                    }

                    server = connection.Tag as Server;
                    return true;
                }
            }

            public static class Queue
            {
                static List<Ticket> List;
                public class Ticket
                {
                    readonly MessagingPeer Peer;

                    public void Accept(RoomConnectionInfo info)
                    {
                        var response = new MatchmakingSuccessResponse(info);
                        Peer.SendMessage(response);
                    }

                    public void Fail() => Fail(WslaErrorCode.NoRoomFound);
                    public void Fail(WslaErrorCode code)
                    {
                        var response = new MatchmakingFailResponse(code);
                        Peer.SendMessage(response);
                    }

                    public Ticket(MessagingPeer Peer)
                    {
                        this.Peer = Peer;
                    }
                }

                public static void Init()
                {
                    List = new List<Ticket>(100);
                }

                public static void Register(MessagingPeer peer, StartMatchMakingRequest request)
                {
                    var ticket = new Ticket(peer);

                    if (Configuration.Applications.TryGetID(request.Application, out var applicationID) is false)
                    {
                        ticket.Fail(WslaErrorCode.ApplicationNotFound);
                        return;
                    }

                    peer.Tag = ticket;

                    lock (List)
                    {
                        List.Add(ticket);
                    }

                    NetworkLog.Info($"Match Making Ticket Created for {peer}");

                    peer.RegisterStopCallback(ClientDisconnectCallback);

                    Resolve(ticket).Forget();
                    static async Task Resolve(Ticket ticket)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2));
                        ticket.Fail();
                    }
                }
                static void ClientDisconnectCallback(MessagingConnection connection, MessagingSocketDisconnectReason reason)
                {
                    if (TryReadTag(connection, out var ticket) is false)
                        return;

                    Remove(ticket);
                }

                static bool Remove(Ticket ticket)
                {
                    lock (List)
                    {
                        return List.Remove(ticket);
                    }
                }

                static bool TryReadTag(MessagingConnection connection, out Ticket ticket)
                {
                    if (connection.Tag is not Ticket)
                    {
                        NetworkLog.Warning($"Peer {connection} not Tagged as Match Making Ticket");

                        ticket = default;
                        return false;
                    }

                    ticket = connection.Tag as Ticket;
                    return true;
                }
            }

            static TaskCompletionQueue<Guid, CreateRoomConfirmation> RoomCreationQueue;

            public class HttpEndpoints
            {
                [ResourceMethod(RequestMethod.Get, Constants.RestRoutes.ListRegions)]
                public List<ServerRegion> ListRegions()
                {
                    var list = new List<ServerRegion>();

                    Browser.ListRegions(list);

                    return list;
                }

                [ResourceMethod(RequestMethod.Post, Constants.RestRoutes.CreateRoom)]
                public async Task<RoomConnectionInfo> CreateRoom(CreateRoomRequest message)
                {
                    if (Configuration.Applications.TryGetID(message.Application, out var applicationID) is false)
                        throw new ProviderException(ResponseStatus.BadRequest, $"Application not Found");

                    //Find Region
                    if (Browser.TryFindFreeServer(message.Regions, out var server) is false)
                        throw new ProviderException(ResponseStatus.BadRequest, $"Regions not Available");

                    var roomID = Guid.NewGuid();

                    var operation = RoomCreationQueue.Create(roomID, server.MessagingPeer.DisconnectCancellationToken);

                    //Forward Request to Relay
                    {
                        var request = new CreateRoomCommand(applicationID, roomID, message.Parameters);
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

                    server.CreateRoom(applicationID, roomID, Confirmation.Port, message.Parameters, 1);

                    return new RoomConnectionInfo(server.Address, Confirmation.Port);
                }

                [ResourceMethod(RequestMethod.Post, Constants.RestRoutes.ListRooms)]
                public List<RoomListEntryInfo> ListRooms(ListRoomsRequest request)
                {
                    if (Configuration.Applications.TryGetID(request.Application, out var applicationID) is false)
                        throw new ProviderException(ResponseStatus.BadRequest, $"Application not Found");

                    var list = new List<RoomListEntryInfo>();

                    Browser.ListRooms(applicationID, request.Regions, list);

                    return list;
                }

                [ResourceMethod(RequestMethod.Post, Constants.RestRoutes.FindRoom)]
                public async Task<RoomConnectionInfo?> FindRoom(FindRoomRequest request)
                {
                    if (Configuration.Applications.TryGetID(request.Application, out var applicationID) is false)
                        throw new ProviderException(ResponseStatus.BadRequest, $"Application not Found");

                    //Try Find Existing Room
                    {
                        if (Browser.TryFindFreeRoom(applicationID, request.Regions, 1, out var room))
                            return room.GetConnectionInfo();
                    }

                    //Try Create Room
                    if (request.CreateRoom.HasValue)
                    {
                        var create = new CreateRoomRequest(request.Application, request.Regions, request.CreateRoom.Value);

                        return await CreateRoom(create);
                    }

                    return null;
                }
            }

            public class MessageHandlers
            {
                public static void RegisterHandlers(MessagingServer server)
                {
                    server.Dispatcher.RegisterSync<RegisterRelayRequest>(RegisterRelayHandler);

                    server.Dispatcher.RegisterSync<CreateRoomConfirmation>(CreateRoomConfirmationHandler);
                    server.Dispatcher.RegisterSync<RemoveRoomRequest>(RemoveRoomHandler);
                    server.Dispatcher.RegisterSync<UpdateRoomsRequest>(UpdateRoomsHandler);

                    server.Dispatcher.RegisterSync<StartMatchMakingRequest>(StartMatchMaking);
                }

                public static void RegisterRelayHandler(MessagingPeer peer, ref RegisterRelayRequest message)
                {
                    NetworkLog.Info($"Registering ({message.Info.Region}) Relay Server on Address: {message.Info.Address}");

                    Browser.RegisterServer(peer, message);
                }

                public static void CreateRoomConfirmationHandler(MessagingPeer peer, ref CreateRoomConfirmation message)
                {
                    RoomCreationQueue.Fulfill(message.ID, message);
                }
                public static void RemoveRoomHandler(MessagingPeer peer, ref RemoveRoomRequest message)
                {
                    if (Browser.TryReadTag(peer, out var server) is false)
                        return;

                    server.RemoveRoom(message.RoomID);
                }
                public static void UpdateRoomsHandler(MessagingPeer peer, ref UpdateRoomsRequest message)
                {
                    if (Browser.TryReadTag(peer, out var server) is false)
                        return;

                    server.UpdateRooms(message.Requests);
                }

                public static void StartMatchMaking(MessagingPeer peer, ref StartMatchMakingRequest request)
                {
                    Queue.Register(peer, request);
                }
            }

            public static void Init()
            {
                RoomCreationQueue = new(100);

                Browser.Init();
                Queue.Init();
            }
        }
    }
}
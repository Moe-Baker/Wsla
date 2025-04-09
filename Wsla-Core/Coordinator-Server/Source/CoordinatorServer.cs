using GenHTTP.Api.Content;
using GenHTTP.Api.Infrastructure;
using GenHTTP.Api.Protocol;
using GenHTTP.Engine.Internal;
using GenHTTP.Modules.Basics;
using GenHTTP.Modules.Conversion.Serializers;
using GenHTTP.Modules.Layouting;
using GenHTTP.Modules.Webservices;

using LiteNetLib.Utils;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Wsla.Serialization;

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
            public ApplicationData[] Applications;
            public class ApplicationData
            {
                public string Name;

                public MatchMakingPoolData[] Pools;
            }
            public struct MatchMakingPoolData
            {
                public string Name;

                public CapacityData Capacity;
                public struct CapacityData
                {
                    public byte Min;
                    public byte Max;
                }

                public bool Backfill;

                public float Duration;
            }

            public bool TryGetApplicationID(in FixedString<FS20> name, out ApplicationID id)
            {
                for (byte i = 0; i < Applications.Length; i++)
                {
                    if (name.Equals(Applications[i].Name, StringComparison.OrdinalIgnoreCase))
                    {
                        id = new ApplicationID(i);
                        return true;
                    }
                }

                id = default;
                return false;
            }

            public static async Task<ConfigurationProperty> Create(Data data)
            {
                return new ConfigurationProperty()
                {
                    Applications = data.Applications,
                };
            }

            public class Data : ServerConfigurationData
            {
                public ApplicationData[] Applications;
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
                var serializers = GenHTTP.Modules.Conversion.Serialization.Empty()
                    .Default(ContentType.ApplicationOctetStream)
                    .Add(ContentType.ApplicationOctetStream, new WslaSerializationFormat());

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
                static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(250);

                static Application[] Applications;
                public class Application
                {
                    readonly ConfigurationProperty.ApplicationData Configuration;

                    public ApplicationID ID { get; }

                    public MatchMakingPool[] Pools;
                    public bool TryFindPool(in FixedString<FS20> Name, out MatchMakingPool pool)
                    {
                        for (byte i = 0; i < Pools.Length; i++)
                        {
                            pool = Pools[i];

                            if (Name.Equals(pool.Name, StringComparison.OrdinalIgnoreCase))
                                return true;
                        }

                        pool = default;
                        return false;
                    }

                    public void Refresh()
                    {
                        foreach (var pool in Pools)
                            pool.Refresh();
                    }

                    public Application(ConfigurationProperty.ApplicationData Configuration, ApplicationID ID)
                    {
                        this.Configuration = Configuration;
                        this.ID = ID;

                        Pools = new MatchMakingPool[Configuration.Pools.Length];

                        for (int i = 0; i < Pools.Length; i++)
                            Pools[i] = new MatchMakingPool(this, Configuration.Pools[i]);
                    }
                }
                public class MatchMakingPool
                {
                    readonly Application Application;
                    readonly ConfigurationProperty.MatchMakingPoolData Configuration;

                    public string Name => Configuration.Name;
                    public TimeSpan Duration { get; }

                    List<Ticket> List;

                    public void Register(Ticket ticket)
                    {
                        lock (List)
                        {
                            List.Add(ticket);
                        }
                    }
                    public bool Unregister(Ticket ticket)
                    {
                        lock (List)
                        {
                            return List.Remove(ticket);
                        }
                    }

                    public void Refresh()
                    {
                        lock (List)
                        {
                            var index = 0;

                            if (List.Count is 0)
                                return;

                            //Skip Expired Tickets
                            for (/* Start at Index */; index < List.Count; index++)
                            {
                                if (List[index].IsExpired() is false)
                                    break;

                                List[index].Fail(WslaErrorCode.Timeout);
                                List[index] = null;
                            }

                            var allocations = (List.Count - index); //Count of Remaining Valid Tickets

                            //Dispatch Remaining Tickets
                            if (allocations >= Configuration.Capacity.Min)
                            {
                                var dispatcher = new Dispatcher(this);

                                for (/* Start at Index */; index < List.Count; index++)
                                {
                                    var entry = TicketEntry.For(List, index);
                                    dispatcher.Accept(entry);
                                }

                                foreach (var batch in dispatcher.Batches)
                                {
                                    if (IsValid(batch) is false)
                                        continue;

                                    foreach (var entry in batch.Entries)
                                        List[entry.Index] = null;

                                    Dispatch(batch).Forget();
                                }
                            }

                            List.RemoveAll(x => x is null);
                        }
                    }

                    bool IsValid(Batch batch)
                    {
                        if (batch.Count < Configuration.Capacity.Min)
                            return false;

                        //Validate Age
                        if (batch.IsFull is false)
                        {
                            var ticket = batch.GetOldestTicket();

                            var age = ticket.CalculateAge();
                            var factor = Duration * 0.75f;

                            if (age < factor)
                                return false;
                        }

                        return true;
                    }

                    class Dispatcher
                    {
                        readonly MatchMakingPool Pool;

                        public List<Batch> Batches { get; }

                        public Batch Accept(TicketEntry entry)
                        {
                            //Iterate Existing Batches
                            {
                                foreach (var batch in Batches)
                                    if (batch.TryAccept(entry))
                                        return batch;
                            }

                            //Create New Batch
                            {
                                var batch = new Batch(Pool, entry);
                                Batches.Add(batch);
                                return batch;
                            }
                        }

                        public Dispatcher(MatchMakingPool Pool)
                        {
                            this.Pool = Pool;

                            Batches = new List<Batch>();
                        }
                    }
                    class Batch
                    {
                        readonly MatchMakingPool Pool;

                        public List<TicketEntry> Entries { get; }

                        public byte Count => (byte)Entries.Count;

                        public bool IsFull => Count >= Pool.Configuration.Capacity.Max;

                        public List<ServerRegion> Regions { get; }

                        public bool TryAccept(TicketEntry entry)
                        {
                            if (IsFull)
                                return false;

                            var ticket = entry.Ticket;

                            if (CheckAllowRegion(ticket.Request.Regions) is false)
                                return false;

                            Entries.Add(entry);
                            CombineRegionList(ticket.Request.Regions);
                            return true;
                        }

                        bool CheckAllowRegion(SparseArray<ServerRegion> input)
                        {
                            foreach (var item in input)
                                if (Regions.Contains(item))
                                    return true;

                            return false;
                        }
                        void CombineRegionList(SparseArray<ServerRegion> input)
                        {
                            Regions.RemoveAll(x => input.Contains(x) is false);
                        }

                        public Ticket GetOldestTicket() => Entries[0].Ticket;

                        public NetworkSceneID GetScene() => GetOldestTicket().Request.Scene;

                        public Batch(MatchMakingPool Pool, TicketEntry entry)
                        {
                            this.Pool = Pool;

                            Entries = new() { entry };

                            Regions = entry.Ticket.Request.Regions.ToList();
                        }
                    }

                    public record struct TicketEntry(Ticket Ticket, int Index)
                    {
                        public static TicketEntry For(List<Ticket> list, int index) => new TicketEntry(list[index], index);
                    }

                    async Task Dispatch(Batch batch)
                    {
                        var Capacity = Configuration.Backfill ? Configuration.Capacity.Max : batch.Count;
                        var Scene = batch.GetScene();
                        var Privacy = Configuration.Backfill ? RoomPrivacy.Public : RoomPrivacy.Private;
                        var Lock = Configuration.Backfill ? RoomLockPolicy.None : RoomLockPolicy.AfterFill;
                        var Parameters = new CreateRoomParameters(Configuration.Name, Capacity, Scene, Password: default, Privacy, Lock);

                        var Regions = SparseArray.Clone(batch.Regions);

                        RoomConnectionInfo Info;

                        try
                        {
                            Info = await Matchmaking.CreateRoom(Application.ID, Regions, Parameters);
                        }
                        catch (Exception ex)
                        {
                            NetworkLog.Error($"Matchmaking Create Room Failed");
                            NetworkLog.Error(ex);

                            foreach (var entry in batch.Entries)
                                entry.Ticket.Fail(WslaErrorCode.InternalError);

                            return;
                        }

                        foreach (var entry in batch.Entries)
                            entry.Ticket.Accept(Info);
                    }

                    public MatchMakingPool(Application Application, ConfigurationProperty.MatchMakingPoolData Configuration)
                    {
                        this.Application = Application;
                        this.Configuration = Configuration;

                        Duration = TimeSpan.FromSeconds(Configuration.Duration);

                        List = new();
                    }
                }

                public class Ticket
                {
                    public readonly MessagingPeer Peer;
                    public readonly MatchMakingPool Pool;
                    public readonly StartMatchMakingRequest Request;

                    readonly DateTime Timestamp;
                    public TimeSpan CalculateAge() => (TimeNow - Timestamp).Duration();
                    public bool IsExpired() => (CalculateAge() > Pool.Duration);

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

                    public void Unregister() => Pool.Unregister(this);

                    public Ticket(MessagingPeer Peer, MatchMakingPool Pool, StartMatchMakingRequest Request)
                    {
                        this.Peer = Peer;
                        this.Pool = Pool;
                        this.Request = Request;

                        Timestamp = TimeNow;
                    }

                    static DateTime TimeNow => DateTime.UtcNow;
                }

                static bool TryGetPool(FixedString<FS20> ApplicationName, FixedString<FS20> PoolName, out MatchMakingPool pool)
                {
                    if (Configuration.TryGetApplicationID(ApplicationName, out var ApplicationID) is false)
                    {
                        pool = default;
                        return false;
                    }

                    return Applications[ApplicationID.Value].TryFindPool(PoolName, out pool);
                }

                public static void Init()
                {
                    //Applications
                    {
                        Applications = new Application[Configuration.Applications.Length];

                        for (byte i = 0; i < Applications.Length; i++)
                        {
                            var id = new ApplicationID(i);
                            Applications[i] = new Application(Configuration.Applications[i], id);
                        }
                    }

                    Refresh().Forget();
                }

                static async Task Refresh()
                {
                    var timer = new PeriodicTimer(RefreshInterval);

                    while (true)
                    {
                        await timer.WaitForNextTickAsync();

                        for (int i = 0; i < Applications.Length; i++)
                            Applications[i].Refresh();
                    }
                }

                public static void Register(MessagingPeer peer, StartMatchMakingRequest request)
                {
                    if (TryGetPool(request.Application, request.Pool, out var pool) is false)
                    {
                        var response = new MatchmakingFailResponse(WslaErrorCode.InvalidRequest);
                        peer.SendMessage(response);
                        return;
                    }

                    var ticket = new Ticket(peer, pool, request);
                    peer.Tag = ticket;

                    pool.Register(ticket);

                    NetworkLog.Info($"Match Making Ticket Created for {peer}");

                    peer.RegisterStopCallback(ClientDisconnectCallback);
                }
                static void ClientDisconnectCallback(MessagingConnection connection, MessagingSocketDisconnectReason reason)
                {
                    if (TryReadTag(connection, out var ticket) is false)
                        return;

                    ticket.Unregister();
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
                public void Usages()
                {
                    Register(ListRegions);
                    Register<CreateRoomRequest, RoomConnectionInfo>(CreateRoom);
                    Register<ListRoomsRequest, List<RoomListEntryInfo>>(ListRooms);
                    Register<FindRoomRequest, RoomConnectionInfo?>(FindRoom);
                }

                void Register<[NetworkSerializationMarker] T>(Func<T> function) { }
                void Register<[NetworkSerializationMarker] TRequest, [NetworkSerializationMarker] TResponse>(Func<TRequest, TResponse> function) { }
                void Register<[NetworkSerializationMarker] TRequest, [NetworkSerializationMarker] TResponse>(Func<TRequest, Task<TResponse>> function) { }

                [ResourceMethod(RequestMethod.Get, Constants.RestRoutes.ListRegions)]
                public List<ServerRegion> ListRegions()
                {
                    var list = new List<ServerRegion>();

                    Browser.ListRegions(list);

                    return list;
                }

                [ResourceMethod(RequestMethod.Post, Constants.RestRoutes.CreateRoom)]
                public Task<RoomConnectionInfo> CreateRoom(CreateRoomRequest message)
                {
                    if (Configuration.TryGetApplicationID(message.Application, out var applicationID) is false)
                        throw new ProviderException(ResponseStatus.BadRequest, $"Application not Found");

                    return Matchmaking.CreateRoom(applicationID, message.Regions, message.Parameters);
                }

                [ResourceMethod(RequestMethod.Post, Constants.RestRoutes.ListRooms)]
                public List<RoomListEntryInfo> ListRooms(ListRoomsRequest request)
                {
                    if (Configuration.TryGetApplicationID(request.Application, out var applicationID) is false)
                        throw new ProviderException(ResponseStatus.BadRequest, $"Application not Found");

                    var list = new List<RoomListEntryInfo>();

                    Browser.ListRooms(applicationID, request.Regions, list);

                    return list;
                }

                [ResourceMethod(RequestMethod.Post, Constants.RestRoutes.FindRoom)]
                public async Task<RoomConnectionInfo?> FindRoom(FindRoomRequest request)
                {
                    if (Configuration.TryGetApplicationID(request.Application, out var applicationID) is false)
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

            public static async Task<RoomConnectionInfo> CreateRoom(ApplicationID applicationID, SparseArray<ServerRegion> regions, CreateRoomParameters parameters)
            {
                //Find Region
                if (Browser.TryFindFreeServer(regions, out var server) is false)
                    throw new ProviderException(ResponseStatus.BadRequest, $"Regions not Available");

                var roomID = Guid.NewGuid();

                var operation = RoomCreationQueue.Create(roomID, server.MessagingPeer.DisconnectCancellationToken);

                //Forward Request to Relay
                {
                    var request = new CreateRoomCommand(applicationID, roomID, parameters);
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

                server.CreateRoom(applicationID, roomID, Confirmation.Port, parameters, 1);

                return new RoomConnectionInfo(server.Address, Confirmation.Port);
            }
        }
    }

    class WslaSerializationFormat : ISerializationFormat
    {
        public ValueTask<IResponseBuilder> SerializeAsync(IRequest request, object response)
        {
            var result = request.Respond()
                .Content(new WslaContent(response))
                .Type(ContentType.ApplicationOctetStream);

            return new ValueTask<IResponseBuilder>(result);
        }
        public async ValueTask<object> DeserializeAsync(Stream stream, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type)
        {
            var destination = NetworkStreamPool.Rent();

            try
            {
                while (true)
                {
                    destination.EnsureFit(100);
                    var memory = destination.PeekAvailableMemory();

                    var read = await stream.ReadAsync(memory);

                    if (read is 0)
                        break;

                    destination.Position += read;
                }

                destination.Position = 0;
                return NetworkSerializer.Implicit.ReadValue(type, destination);
            }
            finally
            {
                NetworkStreamPool.Return(destination);
            }
        }

        static class NetworkStreamPool
        {
            static Stack<INetworkStream> Stack;

            public static INetworkStream Rent()
            {
                lock (Stack)
                {
                    if (Stack.TryPop(out var stream) is false)
                        stream = new NetDataWriter(true, 128);

                    return stream;
                }
            }

            public static void Return(INetworkStream stream)
            {
                stream.Position = 0;

                lock (Stack)
                {
                    Stack.Push(stream);
                }
            }

            static NetworkStreamPool()
            {
                Stack = new(10);
            }
        }

        class WslaContent : IResponseContent
        {
            object Response;

            public ulong? Length => null;

            public ValueTask<ulong?> CalculateChecksumAsync() => new((ulong)Response.GetHashCode());

            public async ValueTask WriteAsync(Stream target, uint bufferSize)
            {
                var source = NetworkStreamPool.Rent();

                try
                {
                    var type = Response.GetType();
                    NetworkSerializer.Implicit.WriteValue(type, Response, source);

                    var memory = source.PeekAllocatedMemory();
                    await target.WriteAsync(memory);
                }
                finally
                {
                    NetworkStreamPool.Return(source);
                }
            }

            public WslaContent(object Response)
            {
                this.Response = Response;
            }
        }
    }
}
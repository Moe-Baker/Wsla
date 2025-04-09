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
                public static List<RelayServer> Servers { get; private set; }

                public static void Init()
                {
                    Servers = new(10);
                }

                public static void RegisterServer(MessagingPeer peer, RegisterRelayRequest message)
                {
                    var server = RelayServer.Create(message, peer);
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

                static void UnregisterServer(RelayServer server)
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

                public static bool TryFindFreeServer(SparseArray<ServerRegion> regions, out RelayServer server)
                {
                    lock (Servers)
                    {
                        var marker = (Found: false, Server: default(RelayServer), Occupancy: int.MaxValue);

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
                public static bool TryFindFreeRoom(ApplicationID application, SparseArray<ServerRegion> regions, MatchMakingPool pool, int capacity, out Room room)
                {
                    lock (Servers)
                    {
                        for (int i = 0; i < Servers.Count; i++)
                        {
                            var server = Servers[i];

                            if (regions.Contains(server.Region) is false)
                                continue;

                            if (server.TryReserveJoin(application, capacity, pool, out room))
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

                public static bool TryReadTag(MessagingConnection connection, out RelayServer server)
                {
                    if (connection.Tag is not RelayServer)
                    {
                        NetworkLog.Warning($"Peer {connection} not Tagged as Relay Server");

                        server = default;
                        return false;
                    }

                    server = connection.Tag as RelayServer;
                    return true;
                }
            }
            public static class Queue
            {
                static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(250);

                static MatchMakingApplication[] Applications;
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
                        Applications = new MatchMakingApplication[Configuration.Applications.Length];

                        for (byte i = 0; i < Applications.Length; i++)
                        {
                            var id = new ApplicationID(i);
                            Applications[i] = new MatchMakingApplication(Configuration.Applications[i], id);
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
                        Fail(peer, WslaErrorCode.InvalidRequest);
                        return;
                    }

                    if (pool.Backfill && Browser.TryFindFreeRoom(pool.Application.ID, request.Regions, 1, out var room))
                    {
                        var info = room.GetConnectionInfo();
                        Accept(peer, info);
                        return;
                    }

                    var ticket = new MatchMakingTicket(peer, pool, request);
                    peer.Tag = ticket;

                    pool.Register(ticket);

                    NetworkLog.Info($"Match Making Ticket Created for {peer}");

                    peer.RegisterStopCallback(ClientDisconnectCallback);
                }
                static void ClientDisconnectCallback(MessagingConnection connection, MessagingSocketDisconnectReason reason)
                {
                    if (connection.Tag is not MatchMakingTicket ticket)
                    {
                        NetworkLog.Warning($"Peer {connection} not Tagged as Match Making Ticket");
                        return;
                    }

                    ticket.Unregister();
                }

                #region Responses
                public static void Accept(MessagingPeer peer, RoomConnectionInfo info)
                {
                    var response = new MatchmakingSuccessResponse(info);
                    peer.SendMessage(response);
                }

                public static void Fail(MessagingPeer peer) => Fail(peer, WslaErrorCode.NoRoomFound);
                public static void Fail(MessagingPeer peer, WslaErrorCode code)
                {
                    var response = new MatchmakingFailResponse(code);
                    peer.SendMessage(response);
                }
                #endregion
            }

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
                public async Task<RoomConnectionInfo> CreateRoom(CreateRoomRequest message)
                {
                    if (Configuration.TryGetApplicationID(message.Application, out var applicationID) is false)
                        throw new ProviderException(ResponseStatus.BadRequest, $"Application not Found");

                    var room = await Matchmaking.CreateRoom(applicationID, message.Regions, message.Parameters);

                    return room.GetConnectionInfo();
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

            static TaskCompletionQueue<Guid, CreateRoomConfirmation> RoomCreationQueue;
            public static async Task<Room> CreateRoom(ApplicationID applicationID, SparseArray<ServerRegion> regions, CreateRoomParameters parameters)
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

                return server.CreateRoom(applicationID, roomID, Confirmation.Port, parameters, 1);
            }
        }
    }
}
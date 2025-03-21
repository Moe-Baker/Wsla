using GenHTTP.Api.Content;
using GenHTTP.Api.Infrastructure;
using GenHTTP.Api.Protocol;
using GenHTTP.Engine.Internal;
using GenHTTP.Modules.Layouting;
using GenHTTP.Modules.Webservices;

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace Wsla.Server
{
    public static class CoordinatorServer
    {
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
                        .Bind(IPAddress.Any, Constants.CoordinatorMessagingPort)
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
            public static List<Server> Servers { get; private set; }
            public class Server
            {
                public RelayServerInfo Info { get; }

                public ServerRegion Region => Info.Region;
                public IPAddress Address => Info.Address;

                Dictionary<Guid, Room> Rooms;
                record struct Room(ushort Port, string Name);

                public bool RegisterRoom(Guid id, ushort port, string name)
                {
                    lock (Rooms)
                    {
                        if (Rooms.ContainsKey(id))
                        {
                            NetworkLog.Warning($"Room with ID {id} Already Registered");
                            return false;
                        }

                        var room = new Room(port, name);
                        Rooms.Add(id, room);

                        return true;
                    }
                }
                public void UnregisterRoom(Guid id)
                {
                    lock (Rooms)
                    {
                        if (Rooms.Remove(id) is false)
                            NetworkLog.Warning($"No Room with ID {id} Registered");
                    }
                }
                public void ListRooms(IPAddress address, List<RoomListEntryInfo> list)
                {
                    lock (Rooms)
                    {
                        foreach (var (id, room) in Rooms)
                        {
                            var connection = new RoomConnectionInfo(address, room.Port);
                            var entry = new RoomListEntryInfo(room.Name, connection);

                            list.Add(entry);
                        }
                    }
                }

                public Server(RelayServerInfo Info)
                {
                    this.Info = Info;

                    Rooms = new(10);
                }
            }

            public class Endpoints
            {
                [ResourceMethod(RequestMethod.Put, Constants.RestRoutes.RegisterRelay)]
                public void RegisterRelayHandler(RegisterRelayRequest message)
                {
                    NetworkLog.Info($"Registering ({message.Info.Region}) Server on Address: {message.Info.Address}");

                    var entry = new Server(message.Info);

                    lock (Servers)
                    {
                        Servers.Add(entry);
                    }
                }

                [ResourceMethod(RequestMethod.Get, Constants.RestRoutes.ListRegions)]
                public ListRegionsResponse ListRegions()
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

                    return new ListRegionsResponse(list);
                }

                [ResourceMethod(RequestMethod.Post, Constants.RestRoutes.CreateRoom)]
                public async Task<CreateRoomResponse> CreateRoom(CreateRoomRequest message)
                {
                    Server Entry;

                    //Find Region
                    if (TryFindServer(message.Region, out Entry) is false)
                        throw new ProviderException(ResponseStatus.BadRequest, $"No Region {message.Region} Found");

                    CreateRoomConfirmation Confirmation;

                    //Forward Request to Relay
                    {
                        var response = await REST.Client.POST<CreateRoomCommand, CreateRoomConfirmation>
                            (Entry.Address, Constants.RelayMessagingPort, Constants.RestRoutes.CreateRoom, message.Command);

                        if (response.IsError)
                            throw response.Error.ToProviderException();

                        Confirmation = response.Value;
                    }

                    Entry.RegisterRoom(Confirmation.ID, Confirmation.Port, message.Command.Name);

                    return new CreateRoomResponse(Entry.Address, Confirmation.Port);
                }

                [ResourceMethod(RequestMethod.Put, Constants.RestRoutes.RemoveRoom)]
                public void RemoveRoom(RemoveRoomRequest request)
                {
                    TryRemoveRoom(request.RelayAddress, request.RoomID);
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

                            server.ListRooms(server.Address, list);
                        }
                    }

                    return list;
                }
            }

            public static void Init()
            {
                Servers = new(10);
            }

            public static bool TryFindServer(ServerRegion region, out Server server)
            {
                lock (Servers)
                {
                    for (int i = 0; i < Servers.Count; i++)
                    {
                        server = Servers[i];

                        if (server.Region == region)
                            return true;
                    }
                }

                server = default;
                return false;
            }
            public static bool TryFindServer(IPAddress address, out Server server)
            {
                lock (Servers)
                {
                    for (int i = 0; i < Servers.Count; i++)
                    {
                        server = Servers[i];

                        if (server.Address.Equals(address))
                            return true;
                    }
                }

                server = default;
                return false;
            }

            public static bool TryRemoveRoom(IPAddress relay, Guid id)
            {
                if (TryFindServer(relay, out var server) is false)
                    return false;

                server.UnregisterRoom(id);
                return true;
            }
        }

        static async Task Main(string[] args)
        {
            Console.Title = "Coordinator Server";

            NetworkLog.UseConsole();

            await LoadConfig();

            ParseArguments(args);

            //Initialize
            {
                REST.Init();
                Matchmaking.Init();
            }

            //Start
            {
                REST.Start();
            }

            while (true) Console.ReadKey();
        }

        static async Task LoadConfig()
        {
            NetworkLog.Info($"Loading Configuration Data");

            var data = ServerConfigurationLoader.Load<ConfigurationProperty.Data>();

            Configuration = await ConfigurationProperty.Create(data);
        }

        static void ParseArguments(string[] args) { }
    }
}
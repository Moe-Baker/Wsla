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

        public static class Messaging
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

                HashSet<Guid> Rooms;
                public void RegisterRoom(Guid id)
                {
                    lock (Rooms)
                    {
                        if (Rooms.Add(id) is false)
                            NetworkLog.Warning($"Room with ID {id} Already Registered");
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

                public Server(RelayServerInfo Info)
                {
                    this.Info = Info;

                    Rooms = new HashSet<Guid>(10);
                }
            }

            public class Mark
            {
                [ResourceMethod(RequestMethod.Get, "mark")]
                public string Method()
                {
                    return "Hello World";
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
                            if (list.Contains(server.Info.Region))
                                continue;

                            list.Add(server.Info.Region);
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
                        var response = await Messaging.Client.POST<CreateRoomCommand, CreateRoomConfirmation>
                            (Entry.Info.Address, Constants.RelayMessagingPort, Constants.RestRoutes.CreateRoom, message.Command);

                        if (response.IsError)
                            throw response.Error.ToProviderException();

                        Confirmation = response.Value;
                    }

                    Entry.RegisterRoom(Confirmation.ID);

                    return new CreateRoomResponse(Entry.Info.Address, Confirmation.Port);
                }
            }

            public static void Init()
            {
                Servers = new(10);
            }

            public static bool TryFindServer(ServerRegion region, out Server info)
            {
                for (int i = 0; i < Servers.Count; i++)
                {
                    info = Servers[i];

                    if (info.Info.Region == region)
                        return true;
                }

                info = default;
                return false;
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
                Messaging.Init();
                Matchmaking.Init();
            }

            //Start
            {
                Messaging.Start();
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
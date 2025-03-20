using GenHTTP.Api.Content;
using GenHTTP.Api.Infrastructure;
using GenHTTP.Api.Protocol;
using GenHTTP.Engine.Internal;

using GenHTTP.Modules.Functional;
using GenHTTP.Modules.Functional.Provider;
using GenHTTP.Modules.Layouting;

using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
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
                    var service = Inline.Create()
                        .Serializers(GenHTTP.Modules.Conversion.Serialization.Default(SharedAPI.JsonOptions));

                    Matchmaking.RegisterMessagingRoutes(service);

                    var api = Layout.Create()
                        .Add(service);

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
            public static List<Entry> Servers { get; private set; }
            public class Entry
            {
                public RelayServerInfo Info { get; }

                public Entry(RelayServerInfo Info)
                {
                    this.Info = Info;
                }
            }

            public static void Init()
            {
                Servers = new(10);
            }

            public static bool TryFindServer(ServerRegion region, out Entry info)
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

            public static void RegisterMessagingRoutes(InlineBuilder builder)
            {
                builder.Put(Constants.RestRoutes.RegisterRelay, (RegisterRelayRequest x) => RegisterRelayHandler(x));

                builder.Get(Constants.RestRoutes.ListRegions, () => ListRegions());

                builder.Post(Constants.RestRoutes.CreateRoom, (CreateRoomRequest x) => CreateRoom(x));
            }

            static void RegisterRelayHandler(RegisterRelayRequest message)
            {
                NetworkLog.Info($"Registering ({message.Info.Region}) Server on Address: {message.Info.Address}");

                var entry = new Entry(message.Info);

                lock (Servers)
                {
                    Servers.Add(entry);
                }
            }

            static List<ServerRegion> ListRegions()
            {
                List<ServerRegion> regions;

                lock (Servers)
                {
                    regions = new(Servers.Count);

                    foreach (var server in Servers)
                    {
                        if (regions.Contains(server.Info.Region))
                            continue;

                        regions.Add(server.Info.Region);
                    }
                }

                return regions;
            }

            static async Task<CreateRoomResponse> CreateRoom(CreateRoomRequest message)
            {
                Entry Entry;

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

                return new CreateRoomResponse(Entry.Info.Address, Confirmation.Port);
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
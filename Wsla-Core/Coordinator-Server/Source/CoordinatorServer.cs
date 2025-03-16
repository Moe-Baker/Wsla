namespace Wsla.Server
{
    public class CoordinatorServer
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
            public static MessagingServer Server { get; private set; }

            public static void Initialize()
            {
                Server = new MessagingServer();
            }

            public static void Start()
            {
                Server.Start(Constants.CoordinatorMessagingPort);
            }
        }

        public static class Matchmaking
        {
            public static List<RelayServerInfo> Servers { get; private set; }

            public static bool TryFindServer(ServerRegion region, out RelayServerInfo info)
            {
                for (int i = 0; i < Servers.Count; i++)
                {
                    info = Servers[i];

                    if (info.Region == region)
                        return true;
                }

                info = default;
                return false;
            }

            public static void Initialize()
            {
                Servers = new(10);

                Messaging.Server.Dispatcher.RegisterAsync<RegisterRelayRequest>(RegisterRelayHandler);

                Messaging.Server.Dispatcher.RegisterAsync<ListRegionsRequest>(ListRegions);

                Messaging.Server.Dispatcher.RegisterAsync<CreateRoomRequest>(CreateRoom);
            }

            static async Task RegisterRelayHandler(MessagingPeer peer, RegisterRelayRequest message)
            {
                NetworkLog.Info($"Registering ({message.Info.Region}) Server on Address: {message.Info.Address}");

                lock (Servers)
                {
                    Servers.Add(message.Info);
                }

                var response = new RegisterRelayResponse();
                await peer.Send(response);
            }

            static async Task ListRegions(MessagingPeer peer, ListRegionsRequest message)
            {
                List<ServerRegion> regions;

                lock (Servers)
                {
                    regions = new(Servers.Count);

                    foreach (var server in Servers)
                    {
                        if (regions.Contains(server.Region))
                            continue;

                        regions.Add(server.Region);
                    }
                }

                var response = new ListRegionsResponse(regions);
                await peer.Send(response);
            }

            static async Task CreateRoom(MessagingPeer peer, CreateRoomRequest message)
            {
                using (var query = new MessagingQuery())
                {
                    RelayServerInfo RelayInfo;

                    //Find Region
                    if (TryFindServer(message.Region, out RelayInfo) is false)
                    {
                        await peer.Send(WslaError.From(WslaErrorCode.NoRegion));
                        return;
                    }

                    CreateRoomConfirmation Confirmation;

                    //Forward Request to Relay
                    {
                        var response = await query.Transport<CreateRoomCommand, CreateRoomConfirmation>(RelayInfo.Address, Constants.RelayMessagingPort, message.Command);

                        if (response.IsError)
                        {
                            await peer.Send(response.Error);
                            return;
                        }

                        Confirmation = response.Value;
                    }

                    //Send Response
                    {
                        var response = new CreateRoomResponse(RelayInfo.Address, Confirmation.Port);
                        await peer.Send(response);
                    }
                }
            }
        }

        static async Task Main(string[] args)
        {
            NetworkLog.UseConsole();

            await LoadConfig();

            ParseArguments(args);

            //Initialize
            {
                Messaging.Initialize();
                Matchmaking.Initialize();
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
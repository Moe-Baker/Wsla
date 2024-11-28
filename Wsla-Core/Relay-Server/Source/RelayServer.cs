namespace Wsla.Server
{
    public static class RelayServer
    {
        public static class Realtime
        {
            public static RoomThreadDispatcher ThreadDispatcher { get; private set; }

            public static void Start()
            {
                NetworkLog.Info($"System Processor Count: {Environment.ProcessorCount}");

                ThreadDispatcher = new RoomThreadDispatcher(TimeSpan.FromMilliseconds(10));
            }

            public static Room CreateRoom(CreateRoomRequest request)
            {
                var instance = new Room(request);

                instance.Start(ThreadDispatcher);

                return instance;
            }
        }

        public static class Messaging
        {
            public static MessagingServer Server { get; private set; }

            public static void Start()
            {
                Server = new MessagingServer();

                Server.Dispatcher.Register<CreateRoomRequest>(CreateRoomHandler);

                Server.Start(Constants.RelayMessagingPort);
            }

            static void CreateRoomHandler(MessagingPeer peer, ref CreateRoomRequest message)
            {
                var room = Realtime.CreateRoom(message);

                var response = new CreateRoomResponse(room.Transport.Port);

                peer.Send(response);
            }
        }

        static void Main(string[] args)
        {
            NetworkLog.UseConsole();

            ParseArguments(args);

            Messaging.Start();
            Realtime.Start();

            while (true) Console.ReadKey();
        }

        static void ParseArguments(string[] args) { }
    }
}
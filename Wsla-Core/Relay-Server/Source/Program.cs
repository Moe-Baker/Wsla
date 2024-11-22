namespace Wsla.Server
{
    public class Program
    {
        public static RoomThreadDispatcher Dispatcher;

        static void Main(string[] args)
        {
            NetworkLog.UseConsole();

            NetworkLog.Info($"System Processor Count: {Environment.ProcessorCount}");

            Dispatcher = new RoomThreadDispatcher(TimeSpan.FromMilliseconds(10));

            var room = new Room("Sample Room");
            room.Start(Dispatcher);

            while (true)
                Console.ReadKey();
        }
    }
}
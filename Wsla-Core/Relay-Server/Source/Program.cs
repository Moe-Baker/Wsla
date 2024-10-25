namespace Wsla.Server.Relay
{
    public class Program
    {
        static void Main(string[] args)
        {
            NetworkLog.UseConsole();

            NetworkLog.Info($"System Processor Count: {Environment.ProcessorCount}");

            var dispatcher = new ThreadDispatcher(TimeSpan.FromMilliseconds(10));

            var room = new Room("Sample Room");
            room.Start(dispatcher);

            while (true)
                Console.ReadKey();
        }
    }
}
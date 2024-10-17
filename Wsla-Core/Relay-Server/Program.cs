using Wsla.Shared;

namespace Wsla.Server.Relay
{
    public class Program
    {
        static void Main(string[] args)
        {
            NetworkLog.UseConsole();

            var room = new Room("Sample Room");
            room.Start();

            while (true)
                Console.ReadKey();
        }
    }
}
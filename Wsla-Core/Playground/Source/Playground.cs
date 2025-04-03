using Wsla;
using Wsla.Serialization;

unsafe class Playground
{
    static void Main()
    {
        NetworkLog.UseConsole();

        Run();

        while (true)
            Console.ReadKey();
    }

    static void Run()
    {
        var source = BinarySource.From(stackalloc byte[200]);

        var request = new CreateRoomRequest()
        {
            Application = "My App",
            Regions = (ServerRegion.Asia, ServerRegion.EU, ServerRegion.USA),
            Parameters = new CreateRoomParameters()
            {
                Name = "My Room",
                Capacity = 10,
                Scene = NetworkSceneID.From(1),
                Password = "Hello Password",
                Privacy = RoomPrivacy.Private,
                Lock = RoomLockPolicy.AfterFill,
            }
        };

        NetworkSerializer.WriteValue(request, ref source);

        source.Position = 0;

        var clone = NetworkSerializer.ReadValue<CreateRoomRequest>(ref source);

        Console.WriteLine(clone.Application);

        Console.WriteLine(clone.Application);
    }
}
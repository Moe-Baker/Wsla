using Wsla;

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
        var array = SparseArray.Clone([1, 2, 3, 4]);

        Console.WriteLine($"Length: {array.Length} | Is Allocated: {array.IsAllocated}");

        foreach (var item in array)
        {
            Console.WriteLine(item);
        }
    }
}
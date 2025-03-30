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
        var text = new FixedString<FS20>("Hello World");
        Console.WriteLine(text.IndexOf("world", StringComparison.OrdinalIgnoreCase));

        var value = SparseArray.From(1, 2, 3);

        var clone = value.Clone();
        clone.Reverse<SparseArray<int>, int>();

        Console.WriteLine(value.IndexOf(1));
        Console.WriteLine(clone.IndexOf(1));
    }
}
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
        var attributes = new AttributeCollection();

        attributes.SetValue("hello", "world");

        Test("var1", 1234);
        Test("var3", DateTime.Now);
        Test("var4", 12.5f);
        Test("var5", 12.5);
        Test("var5", Guid.NewGuid());

        void Test<T>(FixedString<FS20> key, T original)
            where T : IEquatable<T>, ISpanFormattable
        {
            attributes.SetValue(key, original);

            if (attributes.TryParseValue(key, out T clone) is false)
                throw new NotImplementedException();

            Console.WriteLine($"{original} : {clone}");
        }
    }
}
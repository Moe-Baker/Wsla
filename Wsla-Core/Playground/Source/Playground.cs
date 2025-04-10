using System.ComponentModel;

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
        ServerConfigurationLoader.Schema.Write<Data>("Schema.json");
    }

    [Description("Data Type")]
    public struct Data
    {
        [Description("Value 1")]
        public MatchMakingValue PropertyValue { get; set; }

        [Description("Value 2")]
        public MatchMakingValue FieldValue;
    }
}
using System.Net;
using System.Text;

using Wsla;
using Wsla.Serialization;

class Playground
{
    static IPAddress ServerAddress = IPAddress.Parse("10.0.0.10");

    static async Task Main()
    {
        NetworkTypes.Register<SamplePayload>(100);

        NetworkLog.UseConsole();

        Console.WriteLine($"Start In Mode:");
        Console.WriteLine("1. Server");
        Console.WriteLine("2. Client");
        Console.WriteLine("3. Flood");

        Console.Write("Input: ");

        var input = int.Parse(Console.ReadLine());

        switch (input)
        {
            case 1:
                await Server();
                break;

            case 2:
                await Client();
                break;

            case 3:
                await Flood();
                break;
        }

        while (true)
            Console.ReadKey();
    }

    static async Task Server()
    {
        NetworkLog.Trace($"Started Server");

        var server = new MessagingServer();

        server.Dispatcher.RegisterSync<SamplePayload>(MessageHandler);
        void MessageHandler(MessagingPeer peer, ref SamplePayload message)
        {
            //NetworkLog.Trace($"Message: {message.Text}");

            peer.Send(message);
        }

        server.Start(4040);
    }

    static async Task Client()
    {
        NetworkLog.Trace($"Started Client");

        var client = new MessagingClient();

        client.Dispatcher.Register<SamplePayload>(MessageHandler);
        void MessageHandler(ref SamplePayload message)
        {
            Procedure(message);
            async void Procedure(SamplePayload message)
            {
                NetworkLog.Trace($"Message: {message.Text}");

                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }
        }

        await client.Connect(ServerAddress, 4040);

        await client.SendAsync(new SamplePayload("Hello World"));

        await Task.Delay(TimeSpan.FromSeconds(1));

        client.Disconnect();
    }

    static async Task Flood()
    {
        NetworkLog.Trace($"Flood Started");

        var client = new MessagingClient();

        client.Dispatcher.Register<SamplePayload>(MessageHandler);
        void MessageHandler(ref SamplePayload message)
        {
            Procedure(message);
            async void Procedure(SamplePayload message)
            {
                NetworkLog.Trace($"Message: {message.Text}");

                await Task.Delay(TimeSpan.FromMilliseconds(100));

                client.Send(message);
            }
        }

        await client.Connect(ServerAddress, 4040);

        var capacity = 5_000;

        for (int i = 0; i < capacity; i++)
            await client.SendAsync(new SamplePayload("Hello World"));

        NetworkLog.Info($"Flood Ended");
    }

    static string GetRandomString()
    {
        var builder = new StringBuilder();

        var length = Random.Shared.Next(4, 11);

        for (int i = 0; i < length; i++)
        {
            var character = (char)Random.Shared.Next(32, 123);
            builder.Append(character);
        }

        return builder.ToString();
    }

    public struct SamplePayload : IAutoNetworkSerialization
    {
        public string Text;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref Text);
        }

        public SamplePayload(string Text)
        {
            this.Text = Text;
        }
    }
}
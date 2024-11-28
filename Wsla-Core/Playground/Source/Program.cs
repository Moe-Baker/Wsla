using System.Net;
using System.Text;

using Wsla;
using Wsla.Serialization;

class Program
{
    static async Task Main()
    {
        NetworkTypes.Register<SamplePayload>(100);

        NetworkLog.UseConsole();

        Console.WriteLine($"Start In Mode:");
        Console.WriteLine("1. Server");
        Console.WriteLine("2. Client");
        Console.WriteLine("3. Query");

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
                await Query();
                break;
        }

        while (true)
            Console.ReadKey();
    }

    static async Task Server()
    {
        var server = new MessagingServer();

        server.Dispatcher.Register<SamplePayload>(MessageHandler);
        void MessageHandler(MessagingPeer peer, ref SamplePayload message)
        {
            Console.WriteLine($"Message: {message.Text}");

            peer.Send(message);
        }

        server.Start(4040);
    }

    static async Task Client()
    {
        var client = new MessagingClient();

        client.Dispatcher.Register<SamplePayload>(MessageHandler);
        void MessageHandler(ref SamplePayload message)
        {
            Procedure(message);
            async void Procedure(SamplePayload message)
            {
                Console.WriteLine($"Message: {message.Text}");

                await Task.Delay(TimeSpan.FromMilliseconds(100));

                client.Send(new SamplePayload(GetRandomString()));
            }
        }

        await client.Connect(IPAddress.Loopback, 4040);

        client.Send(new SamplePayload("Hello World"));
    }

    static async Task Query()
    {
        using var query = new MessagingQuery();

        await query.Connect(IPAddress.Loopback, 4040);

        for (int i = 0; i < 10; i++)
        {
            //Send
            {
                var payload = new SamplePayload(GetRandomString());
                query.Send(payload);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));

            //Receive
            {
                var payload = await query.Receive<SamplePayload>();
                Console.WriteLine($"Message: {payload.Text}");
            }
        }

        query.Disconnect();
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
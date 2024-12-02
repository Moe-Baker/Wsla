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

        NetworkLog.UseIgnore();

        Console.WriteLine($"Start In Mode:");
        Console.WriteLine("1. Server");
        Console.WriteLine("2. Client");
        Console.WriteLine("3. Query");
        Console.WriteLine("4. Flood");

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

            case 4:
                await Flood();
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
            NetworkLog.Trace($"Message: {message.Text}");

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
                NetworkLog.Trace($"Message: {message.Text}");

                await Task.Delay(TimeSpan.FromMilliseconds(100));

                client.Send(new SamplePayload(GetRandomString()));
            }
        }

        await client.Connect(ServerAddress, 4040);

        client.Send(new SamplePayload("Hello World"));
    }

    static async Task Query()
    {
        using var query = new MessagingQuery();

        for (int c = 0; c < 2; c++)
        {
            //Connect
            {
                var response = await query.Connect(ServerAddress, 4040);

                if (response.IsError)
                    throw response.Error.ToException();
            }

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
                    var response = await query.Receive<SamplePayload>();

                    if (response.IsError)
                        throw response.Error.ToException();

                    var payload = response.Value;

                    NetworkLog.Trace($"Message: {payload.Text}");
                }
            }

            await query.Disconnect();
        }
    }

    static async Task Flood()
    {
        var list = new List<Task>();

        for (int i = 0; i < 5_000; i++)
            list.Add(Query());

        await Task.WhenAll(list);

        Console.WriteLine("Flood Finished");
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
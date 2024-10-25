using Wsla;

NetworkLog.UseConsole();

var dispatcher = new ThreadDispatcher(TimeSpan.FromMilliseconds(500));

for (int i = 0; i < 1; i++)
{
    var job = new Job(dispatcher);
}

while (true)
    Console.ReadKey();

class Job : ThreadDispatcher.IJob
{
    ThreadDispatcher.IJob? ThreadDispatcher.IJob.Next { get; set; }
    ThreadDispatcher.IJob? ThreadDispatcher.IJob.Previous { get; set; }

    readonly ThreadDispatcher.Processor Processor;

    public void Send(TimeSpan elapsed)
    {
        Console.WriteLine("Debug");
    }

    public void Receive()
    {
        Console.WriteLine("Debug");
    }

    public Job(ThreadDispatcher dispatcher)
    {
        Processor = dispatcher.Retrieve();

        Processor.Register(this);
    }
}
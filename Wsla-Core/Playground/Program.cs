using Wsla.Shared.Global;

var queue = new Queue<int>();

queue.Enqueue(1);
queue.Enqueue(2);
queue.Enqueue(3);

foreach (var item in queue)
{
    Console.WriteLine(item);
}

while (true)
    Console.ReadKey();
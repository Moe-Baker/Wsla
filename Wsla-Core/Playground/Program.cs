using System.Net;

using Wsla.Shared.Global;

var array = new ExpandArray<IPAddress>(10, 256, 10);

array.Add(0, IPAddress.Loopback);
array.Add(5, IPAddress.Any);
array.Add(20, IPAddress.Broadcast);

array.Remove(0);

foreach (var item in array)
{
    Console.WriteLine(item);
}

Console.WriteLine("Done");

while (true)
    Console.ReadKey();
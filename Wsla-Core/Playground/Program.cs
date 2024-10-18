using Wsla.Shared.Global;

var array = new AutoExpandArray<int>(0, 100, 10);

array[4] = 0;
array[14] = 0;
array[41] = 0;
array[56] = 0;

Console.WriteLine(array.Length);

while (true)
    Console.ReadKey();
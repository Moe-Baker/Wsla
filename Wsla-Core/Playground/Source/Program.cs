using Wsla;

NetworkLog.UseConsole();

var list = new ExpandList<Data>();

list.Add(1);
list.Add(2);
list.Add(3);

list.RemoveAt(0);
list.RemoveAt(1);
list.RemoveAt(2);

list.Add(4);
list.Add(5);

foreach (var item in list)
{
    Console.WriteLine(item);
}

while (true)
    Console.ReadKey();

class Data
{
    int Number;

    public override string ToString() => Number.ToString();

    public Data(int Number)
    {
        this.Number = Number;
    }

    public static implicit operator Data(int value) => new(value);
}
using System.Collections;

using Wsla;

NetworkLog.UseConsole();

unsafe
{
    var x = new NetworkVersion(255, 255, 255);

    PrintBits((int)x.Major << 16, littleEndian: true);
    PrintBits((int)x.Minor << 8, littleEndian: true);
    PrintBits((int)x.Patch << 0, littleEndian: true);

    PrintBits(0);
    PrintBits(1);
    return;

    var a = new NetworkVersion(0, 1, 0);
    var b = new NetworkVersion(1, 0, 1);

    Console.WriteLine(Convert.ToString((uint)a.Major, toBase: 2));
    Console.WriteLine(Convert.ToString((uint)a.Minor << 8, toBase: 2));
    Console.WriteLine(Convert.ToString((uint)a.Patch << 16, toBase: 2));
    PrintBits(a);
    PrintBits(a.Numerical);
    Console.WriteLine(a.Numerical);

    PrintBits(a);
    PrintBits(a.Numerical);
    Console.WriteLine(b.Numerical);

    Console.WriteLine("--------------------------------");

    PrintBits(b);
    PrintBits(b.Numerical);

    Console.WriteLine(a > b);
}

while (true)
    Console.ReadKey();

unsafe void PrintBits<T>(T instance, bool littleEndian = false)
    where T : unmanaged
{
    void* ptr = &instance;

    var bytes = new byte[sizeof(T)];

    fixed (void* destination = bytes)
    {
        Buffer.MemoryCopy(ptr, destination, bytes.Length, bytes.Length);
    }

    var bits = new BitArray(bytes);

    if (littleEndian)
    {
        for (int i = 0; i < bits.Count; i++)
            Console.Write(bits[i] ? 1 : 0);
    }
    else
    {
        for (int i = bits.Count - 1; i >= 0; i--)
            Console.Write(bits[i] ? 1 : 0);
    }

    Console.WriteLine();
}

struct Data
{
    public bool a;
    public ushort c;
    public uint d;
}
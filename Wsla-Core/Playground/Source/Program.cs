using System.Collections;

using Wsla;

NetworkLog.UseConsole();

var a = Choice.A;

unsafe
{
    Console.WriteLine(sizeof(Choice));
}

PrintBits(a);

Console.WriteLine("DONE");

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

public enum Choice : byte
{
    A, B, C, D, E
}
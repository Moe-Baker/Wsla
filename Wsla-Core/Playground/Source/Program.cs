using System.Collections;

using Wsla;

class Program
{
    static void Main()
    {
        NetworkLog.UseConsole();

        var stream = new BitStream(stackalloc byte[2]);

        for (int i = 0; i < 16; i++)
        {
            Write(ref stream, true);
        }

        stream.Reset();

        for (int i = 0; i < 8; i++)
        {
            Write(ref stream, true);
            Write(ref stream, false);
        }

        stream.Reset();

        for (int i = 0; i < 16; i++)
        {
            Console.WriteLine(Read(ref stream));
        }

        while (true)
            Console.ReadKey();
    }

    static void Write(ref BitStream input, bool value)
    {
        ref var stream = ref input;

        stream.Write(value);
    }

    static bool Read(ref BitStream input)
    {
        ref var stream = ref input;

        return stream.Read();
    }

    static unsafe void PrintBits<T>(T instance)
            where T : unmanaged
    {
        void* ptr = &instance;
        var span = new Span<byte>(ptr, sizeof(T));
        var array = span.ToArray();

        var bits = new BitArray(array);

        for (int i = 0; i < bits.Length; i++)
            Console.Write(bits[i] ? "1" : "0");

        Console.WriteLine();
    }

    public ref struct BitStream
    {
        Span<byte> Buffer { get; }
        int Position;

        public void Write(bool value)
        {
            var notch = Position / 8;
            var index = Position % 8;

            if (value)
            {
                Buffer[notch] |= (byte)(1 << index);
            }
            else
            {
                Buffer[notch] &= (byte)~(1 << index);
            }

            Position += 1;
        }
        public bool Read()
        {
            var notch = Position / 8;
            var index = Position % 8;

            Position += 1;

            return (Buffer[notch] & (1 << index)) != 0;
        }

        public void Reset()
        {
            Position = 0;
        }

        public BitStream(Span<byte> Buffer)
        {
            this.Buffer = Buffer;
            Position = 0;
        }

        public static int BitsToBytes(int bits) => ((bits - 1) / 8) + 1;
    }
}
using System;

public ref struct BitStream
{
    public Span<byte> Buffer { get; }
    public int Position { get; set; }

    public void Write(bool value)
    {
        var octet = Position / 8;
        var shift = Position % 8;

        if (value)
            Buffer[octet] |= (byte)(1 << shift);
        else
            Buffer[octet] &= (byte)~(1 << shift);

        Position += 1;
    }
    public bool Read()
    {
        var octet = Position / 8;
        var shift = Position % 8;

        Position += 1;

        return (Buffer[octet] & (1 << shift)) != 0;
    }

    public void Reset()
    {
        Position = 0;
    }

    public BitStream(Span<byte> Buffer) : this(Buffer, 0) { }
    public BitStream(Span<byte> Buffer, int Position)
    {
        this.Buffer = Buffer;
        this.Position = Position;
    }

    public static int BitsToBytes(int bits) => ((bits - 1) / 8) + 1;
}
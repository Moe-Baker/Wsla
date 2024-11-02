using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

using Wsla.Serialization;

namespace Wsla
{
    public static class FixedString
    {
        internal unsafe static int Populate<TString>(ref TString instance, ReadOnlySpan<char> characters, int capacity)
            where TString : IFixedString
        {
            if (characters.Length > capacity)
                throw new ArgumentOutOfRangeException($"{typeof(TString)} Can Only Accept {capacity} Characters");

            fixed (char* source = characters)
            fixed (char* destination = instance)
            {
                Buffer.MemoryCopy(source, destination, capacity * sizeof(char), characters.Length * sizeof(char));
            }

            return characters.Length;
        }

        public unsafe static char Index<TString>(ref TString instance, int index, int length)
            where TString : IFixedString
        {
            if (index > length)
                throw new IndexOutOfRangeException($"Can't Access Character {index} on {typeof(TString)} as it Only has {length} Elements");

            fixed (char* destination = instance)
                return *(destination + index);
        }

        public unsafe static Span<char> CopyTo<TString>(this TString instance, Span<char> buffer)
            where TString : IFixedString
        {
            fixed (char* source = instance)
            fixed (char* destination = buffer)
            {
                Buffer.MemoryCopy(source, destination, buffer.Length * sizeof(char), instance.Length * sizeof(char));

                return buffer.Slice(0, instance.Length);
            }
        }

        public unsafe static string ToString<TString>(ref TString instance)
            where TString : IFixedString
        {
            fixed (char* destination = instance)
                return new string(destination, 0, instance.Length);
        }
    }

    public interface IFixedString
    {
        int Length { get; }

        public char this[int index] { get; }

        [EditorBrowsable(EditorBrowsableState.Never)]
        ref char GetPinnableReference();
    }

    public unsafe struct FixedString20 : IFixedString
    {
        fixed char Characters[Capacity];

        public int Length { get; }

        public char this[int index] => FixedString.Index(ref this, index, Length);

        public const int Capacity = 20;

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ref char GetPinnableReference() => ref Characters[0];

        public override string ToString() => FixedString.ToString(ref this);

        public FixedString20(ReadOnlySpan<char> characters)
        {
            Unsafe.SkipInit(out this);

            Length = FixedString.Populate(ref this, characters, Capacity);
        }

        public static implicit operator FixedString20(string text) => new(text);
        public static implicit operator FixedString20(ReadOnlySpan<char> characters) => new(characters);
    }
    public unsafe struct FixedString40 : IFixedString
    {
        fixed char Characters[Capacity];

        public int Length { get; }

        public char this[int index] => FixedString.Index(ref this, index, Length);

        public const int Capacity = 40;

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ref char GetPinnableReference() => ref Characters[0];

        public override string ToString() => FixedString.ToString(ref this);

        public FixedString40(ReadOnlySpan<char> characters)
        {
            Unsafe.SkipInit(out this);

            Length = FixedString.Populate(ref this, characters, Capacity);
        }

        public static implicit operator FixedString40(string text) => new(text);
        public static implicit operator FixedString40(ReadOnlySpan<char> characters) => new(characters);
    }
    public unsafe struct FixedString80 : IFixedString
    {
        fixed char Characters[Capacity];

        public int Length { get; }

        public char this[int index] => FixedString.Index(ref this, index, Length);

        public const int Capacity = 80;

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ref char GetPinnableReference() => ref Characters[0];

        public override string ToString() => FixedString.ToString(ref this);

        public FixedString80(ReadOnlySpan<char> characters)
        {
            Unsafe.SkipInit(out this);

            Length = FixedString.Populate(ref this, characters, Capacity);
        }

        public static implicit operator FixedString80(string text) => new(text);
        public static implicit operator FixedString80(ReadOnlySpan<char> characters) => new(characters);
    }

    public unsafe class FixedStringNetworkSerializationResolver<TString> : NetworkSerializationResolver<TString>
        where TString : IFixedString
    {
        CreatorDelegate Creator;
        public delegate TString CreatorDelegate(ReadOnlySpan<char> characters);

        Encoding Encoder => Encoding.UTF8;

        public override void Write(in TString value, INetworkStream stream)
        {
            fixed (char* ptr = value)
            {
                var source = new Span<char>(ptr, value.Length);

                Span<byte> buffer = stackalloc byte[Encoder.GetMaxByteCount(value.Length)];

                //Max capacity of 80 specifically chosen to ensure the max byte count is under byte.MaxValue
                var length = (byte)Encoder.GetBytes(source, buffer);
                buffer = buffer.Slice(0, length);

                //Pop (length header + characters buffer) size span
                var destination = stream.PopSpan(1 + length);

                //Write Length
                destination[0] = length;

                //Write characters
                destination = destination.Slice(1, length);
                buffer.CopyTo(destination);
            }
        }
        public override void Read(ref TString value, INetworkStream stream)
        {
            var length = stream.PopByte();

            if (length is 0)
            {
                value = default;
                return;
            }

            var binary = stream.PopSpan(length);

            Span<char> characters = stackalloc char[Encoder.GetMaxCharCount(length)];

            var count = Encoder.GetChars(binary, characters);
            characters = characters.Slice(0, count);

            value = Creator(characters);
        }

        public FixedStringNetworkSerializationResolver(CreatorDelegate Creator)
        {
            this.Creator = Creator;
        }
    }
}
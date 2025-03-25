using System;
using System.Buffers;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Wsla.Serialization;

namespace Wsla
{
    public static class FixedString
    {
        public const int Min = FixedString20.Capacity;
        public const int Max = FixedString80.Capacity;

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

        public unsafe static bool Equals<TString>(ref TString left, ref TString right, StringComparison comparison)
            where TString : IFixedString
        {
            fixed (void* leftPtr = left)
            fixed (void* rightPtr = right)
            {
                var leftSpan = new ReadOnlySpan<char>(leftPtr, left.Length);
                var rightSpan = new ReadOnlySpan<char>(rightPtr, right.Length);

                return MemoryExtensions.Equals(leftSpan, rightSpan, comparison);
            }
        }

        public unsafe static int Compare<TString>(ref TString left, ref TString right, StringComparison comparison)
            where TString : IFixedString
        {
            fixed (void* leftPtr = left)
            fixed (void* rightPtr = right)
            {
                var leftSpan = new ReadOnlySpan<char>(leftPtr, left.Length);
                var rightSpan = new ReadOnlySpan<char>(rightPtr, right.Length);

                return MemoryExtensions.CompareTo(leftSpan, rightSpan, comparison);
            }
        }

        public unsafe static int GetHashcode<TString>(ref TString instance)
            where TString : IFixedString
        {
            fixed (char* ptr = instance)
            {
                var span = new ReadOnlySpan<char>(ptr, instance.Length);
                return FNVHash.Compute(span);
            }
        }

        public static class FNVHash
        {
            // http://isthe.com/chongo/tech/comp/fnv/
            public const uint FNV_PRIME = 16777619;
            public const uint FNV_OFFSET_BASIS = 2166136261;

            public static int Compute(ReadOnlySpan<char> characters, bool ignoreCase = false)
            {
                var hash = FNV_OFFSET_BASIS;

                for (var i = 0; i < characters.Length; i++)
                {
                    byte octet;

                    if (ignoreCase)
                        octet = (byte)char.ToLower(characters[i]);
                    else
                        octet = (byte)characters[i];

                    hash = hash * FNV_PRIME;
                    hash = hash ^ octet;
                }

                return Unsafe.As<uint, int>(ref hash);
            }
        }
    }

    public interface IFixedString
    {
        int Length { get; }
        void SetLength(int value);

        int Max { get; }

        char this[int index] { get; }

        [EditorBrowsable(EditorBrowsableState.Never)]
        ref char GetPinnableReference();
    }

    public unsafe struct FixedString20 : IFixedString, IComparable<FixedString20>, IEquatable<FixedString20>
    {
        fixed char Characters[Capacity];

        public int Length { get; private set; }
        void IFixedString.SetLength(int value) => Length = value;

        public char this[int index] => FixedString.Index(ref this, index, Length);

        public const int Capacity = 20;
        public int Max => Capacity;

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ref char GetPinnableReference() => ref Characters[0];

        public override string ToString() => FixedString.ToString(ref this);

        public override bool Equals(object obj)
        {
            if (obj is FixedString20 other)
                return Equals(other);

            return false;
        }
        public bool Equals(FixedString20 other) => FixedString.Equals(ref this, ref other, StringComparison.Ordinal);

        public override int GetHashCode() => FixedString.GetHashcode(ref this);

        public int CompareTo(FixedString20 other) => FixedString.GetHashcode(ref this);

        public FixedString20(ReadOnlySpan<char> characters)
        {
            Unsafe.SkipInit(out this);

            Length = FixedString.Populate(ref this, characters, Capacity);
        }

        public static implicit operator FixedString20(string text) => new(text);
        public static implicit operator FixedString20(ReadOnlySpan<char> characters) => new(characters);

        public static bool operator ==(FixedString20 left, FixedString20 right) => left.Equals(right);
        public static bool operator !=(FixedString20 left, FixedString20 right) => !left.Equals(right);
    }
    public unsafe struct FixedString40 : IFixedString, IComparable<FixedString40>, IEquatable<FixedString40>
    {
        fixed char Characters[Capacity];

        public int Length { get; private set; }
        void IFixedString.SetLength(int value) => Length = value;

        public char this[int index] => FixedString.Index(ref this, index, Length);

        public const int Capacity = 20;
        public int Max => Capacity;

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ref char GetPinnableReference() => ref Characters[0];

        public override string ToString() => FixedString.ToString(ref this);

        public override bool Equals(object obj)
        {
            if (obj is FixedString40 other)
                return Equals(other);

            return false;
        }
        public bool Equals(FixedString40 other) => FixedString.Equals(ref this, ref other, StringComparison.Ordinal);

        public override int GetHashCode() => FixedString.GetHashcode(ref this);

        public int CompareTo(FixedString40 other) => FixedString.GetHashcode(ref this);

        public FixedString40(ReadOnlySpan<char> characters)
        {
            Unsafe.SkipInit(out this);

            Length = FixedString.Populate(ref this, characters, Capacity);
        }

        public static implicit operator FixedString40(string text) => new(text);
        public static implicit operator FixedString40(ReadOnlySpan<char> characters) => new(characters);

        public static bool operator ==(FixedString40 left, FixedString40 right) => left.Equals(right);
        public static bool operator !=(FixedString40 left, FixedString40 right) => !left.Equals(right);
    }
    public unsafe struct FixedString60 : IFixedString, IComparable<FixedString60>, IEquatable<FixedString60>
    {
        fixed char Characters[Capacity];

        public int Length { get; private set; }
        void IFixedString.SetLength(int value) => Length = value;

        public char this[int index] => FixedString.Index(ref this, index, Length);

        public const int Capacity = 20;
        public int Max => Capacity;

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ref char GetPinnableReference() => ref Characters[0];

        public override string ToString() => FixedString.ToString(ref this);

        public override bool Equals(object obj)
        {
            if (obj is FixedString60 other)
                return Equals(other);

            return false;
        }
        public bool Equals(FixedString60 other) => FixedString.Equals(ref this, ref other, StringComparison.Ordinal);

        public override int GetHashCode() => FixedString.GetHashcode(ref this);

        public int CompareTo(FixedString60 other) => FixedString.GetHashcode(ref this);

        public FixedString60(ReadOnlySpan<char> characters)
        {
            Unsafe.SkipInit(out this);

            Length = FixedString.Populate(ref this, characters, Capacity);
        }

        public static implicit operator FixedString60(string text) => new(text);
        public static implicit operator FixedString60(ReadOnlySpan<char> characters) => new(characters);

        public static bool operator ==(FixedString60 left, FixedString60 right) => left.Equals(right);
        public static bool operator !=(FixedString60 left, FixedString60 right) => !left.Equals(right);
    }
    public unsafe struct FixedString80 : IFixedString, IComparable<FixedString80>, IEquatable<FixedString80>
    {
        fixed char Characters[Capacity];

        public int Length { get; private set; }
        void IFixedString.SetLength(int value) => Length = value;

        public char this[int index] => FixedString.Index(ref this, index, Length);

        public const int Capacity = 20;
        public int Max => Capacity;

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ref char GetPinnableReference() => ref Characters[0];

        public override string ToString() => FixedString.ToString(ref this);

        public override bool Equals(object obj)
        {
            if (obj is FixedString80 other)
                return Equals(other);

            return false;
        }
        public bool Equals(FixedString80 other) => FixedString.Equals(ref this, ref other, StringComparison.Ordinal);

        public override int GetHashCode() => FixedString.GetHashcode(ref this);

        public int CompareTo(FixedString80 other) => FixedString.GetHashcode(ref this);

        public FixedString80(ReadOnlySpan<char> characters)
        {
            Unsafe.SkipInit(out this);

            Length = FixedString.Populate(ref this, characters, Capacity);
        }

        public static implicit operator FixedString80(string text) => new(text);
        public static implicit operator FixedString80(ReadOnlySpan<char> characters) => new(characters);

        public static bool operator ==(FixedString80 left, FixedString80 right) => left.Equals(right);
        public static bool operator !=(FixedString80 left, FixedString80 right) => !left.Equals(right);
    }

    public unsafe class FixedStringNetworkSerializationResolver<TString> : NetworkSerializationResolver<TString>
        where TString : IFixedString, new()
    {
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

            fixed (char* ptr = value)
            {
                var characters = new Span<char>(ptr, value.Max);

                var count = Encoder.GetChars(binary, characters);
                characters = characters.Slice(0, count);

                value.SetLength(count);
            }
        }
    }

    public unsafe class FixedStringJsonConverter<TString> : JsonConverter<TString>
        where TString : IFixedString, new()
    {
        public override TString Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType is JsonTokenType.Null)
                return default;

            if (reader.TokenType is not JsonTokenType.String)
                throw new JsonException($"Cannot Convert {reader.TokenType} to Fixed String");

            if (reader.ValueIsEscaped)
                throw new JsonException($"Cannot Convert Escaped String to Fixed String");

            if (reader.HasValueSequence)
            {
                CheckBinarySize((int)reader.ValueSequence.Length);

                Span<byte> binary = stackalloc byte[(int)reader.ValueSequence.Length];
                reader.ValueSequence.CopyTo(binary);

                return ReadBinary(binary);
            }
            else
            {
                CheckBinarySize(reader.ValueSpan.Length);

                var binary = reader.ValueSpan;

                return ReadBinary(binary);
            }

            static void CheckBinarySize(int binary)
            {
                var max = Encoding.UTF8.GetMaxByteCount(FixedString.Max);

                if (binary > max)
                    throw new JsonException($"Json Fixed String Bytes Longer than Possible Max of {max}");
            }
            static TString ReadBinary(ReadOnlySpan<byte> binary)
            {
                var value = new TString();

                fixed (char* ptr = value)
                {
                    var characters = new Span<char>(ptr, value.Max);

                    var length = Encoding.UTF8.GetChars(binary, characters);
                    characters = characters.Slice(0, length);

                    value.SetLength(length);
                }

                return value;
            }
        }

        public override void Write(Utf8JsonWriter writer, TString value, JsonSerializerOptions options)
        {
            fixed (char* ptr = value)
            {
                var source = new Span<char>(ptr, value.Length);
                writer.WriteStringValue(source);
            }
        }
    }
}
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Wsla.Serialization;

namespace Wsla
{
    public static unsafe class FixedString
    {
        public const int MinCharacters = FS20.Capacity;
        public const int MaxCharacters = FS80.Capacity;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Equals<TLeft, TRight>(in TLeft left, in TRight right)
            where TLeft : IFixedString
            where TRight : IFixedString
        {
            return Equals(in left, in right, StringComparison.Ordinal);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Equals<TLeft, TRight>(in TLeft left, in TRight right, StringComparison comparison)
            where TLeft : IFixedString
            where TRight : IFixedString
        {
            return MemoryExtensions.Equals(left.GetUsedSpan(), right.GetUsedSpan(), comparison);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Compare<TLeft, TRight>(in TLeft left, in TRight right)
            where TLeft : IFixedString
            where TRight : IFixedString
        {
            return Compare(in left, in right);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Compare<TLeft, TRight>(in TLeft left, in TRight right, StringComparison comparison)
            where TLeft : IFixedString
            where TRight : IFixedString
        {
            return MemoryExtensions.CompareTo(left.GetUsedSpan(), right.GetUsedSpan(), comparison);
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

        public class Comparer : IEqualityComparer<FixedString<FS20>>,
            IEqualityComparer<FixedString<FS40>>,
            IEqualityComparer<FixedString<FS60>>,
            IEqualityComparer<FixedString<FS80>>
        {
            public bool IgnoreCase { get; }

            public bool Equals(FixedString<FS20> x, FixedString<FS20> y)
            {
                var comparison = IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                return FixedString.Equals(in x, in y, comparison);
            }
            public int GetHashCode(FixedString<FS20> target)
            {
                var span = target.AsSpan();
                return FixedString.FNVHash.Compute(span, ignoreCase: IgnoreCase);
            }

            public bool Equals(FixedString<FS40> x, FixedString<FS40> y)
            {
                var comparison = IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                return FixedString.Equals(in x, in y, comparison);
            }
            public int GetHashCode(FixedString<FS40> target)
            {
                var span = target.AsSpan();
                return FixedString.FNVHash.Compute(span, ignoreCase: IgnoreCase);
            }

            public bool Equals(FixedString<FS60> x, FixedString<FS60> y)
            {
                var comparison = IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                return FixedString.Equals(in x, in y, comparison);
            }
            public int GetHashCode(FixedString<FS60> target)
            {
                var span = target.AsSpan();
                return FixedString.FNVHash.Compute(span, ignoreCase: IgnoreCase);
            }

            public bool Equals(FixedString<FS80> x, FixedString<FS80> y)
            {
                var comparison = IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                return FixedString.Equals(in x, in y, comparison);
            }
            public int GetHashCode(FixedString<FS80> target)
            {
                var span = target.AsSpan();
                return FixedString.FNVHash.Compute(span, ignoreCase: IgnoreCase);
            }

            public Comparer(bool IgnoreCase)
            {
                this.IgnoreCase = IgnoreCase;
            }

            public static Comparer Ordinal { get; } = new Comparer(false);
            public static Comparer OrdinalIgnoreCase { get; } = new Comparer(true);
        }
    }

    public interface IFixedString
    {
        int Length { get; }
        void SetLength(int value);

        char this[int index] { get; }
        int Max { get; }

        /// <summary>
        /// Get the total span that this fixed string has access to (the entire fixed storage)
        /// </summary>
        /// <returns></returns>
        Span<char> GetTotalSpan();

        /// <summary>
        /// Gets a span of the used characters in this fixed string
        /// </summary>
        /// <returns></returns>
        Span<char> GetUsedSpan();

        ReadOnlySpan<char> AsSpan();
    }
    public unsafe struct FixedString<TStorage> : IFixedString,
        ISpannable<char>, IAssignableSpannable<char>,
        IEquatable<FixedString<FS20>>, IEquatable<FixedString<FS40>>, IEquatable<FixedString<FS60>>, IEquatable<FixedString<FS80>>, IEquatable<string>,
        IComparable<FixedString<FS20>>, IComparable<FixedString<FS40>>, IComparable<FixedString<FS60>>, IComparable<FixedString<FS80>>, IComparable<string>
        where TStorage : struct, IFixedStringStorage
    {
        TStorage Storage;

        public int Length { get; private set; }
        /// <summary>
        /// Sets the length of this fixed string while respecting its Storage's max value
        /// </summary>
        /// <param name="value"></param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public void SetLength(int value)
        {
            if (value < 0 || value > Storage.Max)
                throw new ArgumentOutOfRangeException($"Fixed String {typeof(TStorage).Name} Only  Supports Length of [{0}-{Storage.Max}]");

            Length = value;
        }

        public int Max => Storage.Max;

        public string AsString => ToString();

        public char this[int index]
        {
            get
            {
                var span = GetUsedSpan();
                return span[index];
            }
            set
            {
                var span = GetUsedSpan();
                span[index] = value;
            }
        }

        #region Equality
        public override bool Equals(object obj)
        {
            if (obj is IFixedString other)
                return FixedString.Equals(in this, in other);

            return false;
        }

        //Fixed String 20
        public bool Equals(FixedString<FS20> other) => FixedString.Equals(in this, in other);
        public bool Equals(FixedString<FS20> other, StringComparison comparison) => FixedString.Equals(in this, in other, comparison);
        public static bool operator ==(FixedString<TStorage> left, FixedString<FS20> right) => FixedString.Equals(in left, in right);
        public static bool operator !=(FixedString<TStorage> left, FixedString<FS20> right) => !FixedString.Equals(in left, in right);

        //Fixed String 40
        public bool Equals(FixedString<FS40> other) => FixedString.Equals(in this, in other);
        public bool Equals(FixedString<FS40> other, StringComparison comparison) => FixedString.Equals(in this, in other, comparison);
        public static bool operator ==(FixedString<TStorage> left, FixedString<FS40> right) => FixedString.Equals(in left, in right);
        public static bool operator !=(FixedString<TStorage> left, FixedString<FS40> right) => !FixedString.Equals(in left, in right);

        //Fixed String 60
        public bool Equals(FixedString<FS60> other) => FixedString.Equals(in this, in other);
        public bool Equals(FixedString<FS60> other, StringComparison comparison) => FixedString.Equals(in this, in other, comparison);
        public static bool operator ==(FixedString<TStorage> left, FixedString<FS60> right) => FixedString.Equals(in left, in right);
        public static bool operator !=(FixedString<TStorage> left, FixedString<FS60> right) => !FixedString.Equals(in left, in right);

        //Fixed String 80
        public bool Equals(FixedString<FS80> other) => FixedString.Equals(in this, in other);
        public bool Equals(FixedString<FS80> other, StringComparison comparison) => FixedString.Equals(in this, in other, comparison);
        public static bool operator ==(FixedString<TStorage> left, FixedString<FS80> right) => FixedString.Equals(in left, in right);
        public static bool operator !=(FixedString<TStorage> left, FixedString<FS80> right) => !FixedString.Equals(in left, in right);

        //System.String
        public bool Equals(string other) => Equals(other, StringComparison.Ordinal);
        public bool Equals(string other, StringComparison comparison)
        {
            return MemoryExtensions.Equals(AsSpan(), other.AsSpan(), comparison);
        }
        public static bool operator ==(FixedString<TStorage> left, string right) => left.Equals(right);
        public static bool operator !=(FixedString<TStorage> left, string right) => !left.Equals(right);
        public static bool operator ==(string left, FixedString<TStorage> right) => right.Equals(left);
        public static bool operator !=(string left, FixedString<TStorage> right) => !right.Equals(left);
        #endregion

        #region Comparison
        public int CompareTo(FixedString<FS20> other) => FixedString.Compare(in this, in other);
        public int CompareTo(FixedString<FS20> other, StringComparison comparison) => FixedString.Compare(in this, in other, comparison);

        public int CompareTo(FixedString<FS40> other) => FixedString.Compare(in this, in other);
        public int CompareTo(FixedString<FS40> other, StringComparison comparison) => FixedString.Compare(in this, in other, comparison);

        public int CompareTo(FixedString<FS60> other) => FixedString.Compare(in this, in other);
        public int CompareTo(FixedString<FS60> other, StringComparison comparison) => FixedString.Compare(in this, in other, comparison);

        public int CompareTo(FixedString<FS80> other) => FixedString.Compare(in this, in other);
        public int CompareTo(FixedString<FS80> other, StringComparison comparison) => FixedString.Compare(in this, in other, comparison);

        public int CompareTo(string other) => CompareTo(other, StringComparison.Ordinal);
        public int CompareTo(string other, StringComparison comparison)
        {
            return MemoryExtensions.CompareTo(AsSpan(), other.AsSpan(), comparison);
        }
        #endregion

        #region Span
        public Span<char> GetTotalSpan() => Storage.GetSpan();
        public Span<char> GetUsedSpan() => Storage.GetSpan().Slice(0, Length);

        public ReadOnlySpan<char> AsSpan() => GetUsedSpan();

        public void Assign(ReadOnlySpan<char> input)
        {
            SetLength(input.Length);

            var destination = GetTotalSpan();
            input.CopyTo(destination);
        }
        #endregion

        #region Implicit Converters
        public static implicit operator FixedString<TStorage>(string input) => new FixedString<TStorage>(input);
        public static implicit operator FixedString<TStorage>(ReadOnlySpan<char> input) => new FixedString<TStorage>(input);

        public static implicit operator FixedString<TStorage>(FixedString<FS20> other) => new FixedString<TStorage>(other.AsSpan());
        public static implicit operator FixedString<TStorage>(FixedString<FS40> other) => new FixedString<TStorage>(other.AsSpan());
        public static implicit operator FixedString<TStorage>(FixedString<FS60> other) => new FixedString<TStorage>(other.AsSpan());
        public static implicit operator FixedString<TStorage>(FixedString<FS80> other) => new FixedString<TStorage>(other.AsSpan());

        public static implicit operator ReadOnlySpan<char>(FixedString<TStorage> text) => text.AsSpan();
        #endregion

        public FixedString<TStorage> Clone()
        {
            var characters = AsSpan();
            return new FixedString<TStorage>(characters);
        }

        public override int GetHashCode() => FixedString.FNVHash.Compute(AsSpan());

        public override string ToString() => AsSpan().ToString();

        public FixedString(ReadOnlySpan<char> input)
        {
            Unsafe.SkipInit(out this);

            Storage = new TStorage();

            Assign(input);
        }
    }

    public interface IFixedStringStorage
    {
        int Max { get; }

        Span<char> GetSpan();
    }
    public unsafe struct FS20 : IFixedStringStorage
    {
        public const int Capacity = 20;

        fixed char Characters[Capacity];
        public int Max => Capacity;

        Span<char> IFixedStringStorage.GetSpan() => MemoryMarshal.CreateSpan(ref Characters[0], Capacity);
    }
    public unsafe struct FS40 : IFixedStringStorage
    {
        public const int Capacity = 40;

        fixed char Characters[Capacity];
        public int Max => Capacity;

        Span<char> IFixedStringStorage.GetSpan() => MemoryMarshal.CreateSpan(ref Characters[0], Capacity);
    }
    public unsafe struct FS60 : IFixedStringStorage
    {
        public const int Capacity = 60;

        fixed char Characters[Capacity];
        public int Max => Capacity;

        Span<char> IFixedStringStorage.GetSpan() => MemoryMarshal.CreateSpan(ref Characters[0], Capacity);
    }
    public unsafe struct FS80 : IFixedStringStorage
    {
        public const int Capacity = 80;

        fixed char Characters[Capacity];
        public int Max => Capacity;

        Span<char> IFixedStringStorage.GetSpan() => MemoryMarshal.CreateSpan(ref Characters[0], Capacity);
    }

    public unsafe class FixedStringNetworkSerializationResolver<TString> : NetworkSerializationResolver<TString>
        where TString : IFixedString, new()
    {
        Encoding Encoder => Encoding.UTF8;

        public override void Write(in TString value, ref BinarySource stream)
        {
            var source = value.GetUsedSpan();

            Span<byte> buffer = stackalloc byte[Encoder.GetMaxByteCount(value.Length)];

            //Max capacity of 80 specifically chosen to ensure the max byte count is under byte.MaxValue
            var length = (byte)Encoder.GetBytes(source, buffer);
            buffer = buffer.Slice(0, length);

            //Pop (length header + characters buffer) size span
            var destination = stream.AllocateSpan(1 + length);

            //Write Length
            destination[0] = length;

            //Write characters
            destination = destination.Slice(1, length);
            buffer.CopyTo(destination);
        }
        public override void Read(ref TString value, ref BinarySource stream)
        {
            var length = stream.ReadByte();

            if (length is 0)
            {
                value = default;
                return;
            }

            var binary = stream.ReadSpan(length);

            var characters = value.GetTotalSpan();
            var count = Encoder.GetChars(binary, characters);
            value.SetLength(count);
        }
    }

    public unsafe class FixedStringJsonConverter<TString> : JsonConverter<TString>
        where TString : IFixedString, new()
    {
        public override TString ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return ReadValue(ref reader);
        }
        public override void WriteAsPropertyName(Utf8JsonWriter writer, TString value, JsonSerializerOptions options)
        {
            var characters = value.AsSpan();
            writer.WritePropertyName(characters);
        }

        TString ReadValue(ref Utf8JsonReader reader)
        {
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
                var max = Encoding.UTF8.GetMaxByteCount(FixedString.MaxCharacters);

                if (binary > max)
                    throw new JsonException($"Json Fixed String Bytes Longer than Possible Max of {max}");
            }
            static TString ReadBinary(ReadOnlySpan<byte> binary)
            {
                var value = new TString();

                var characters = value.GetTotalSpan();
                var length = Encoding.UTF8.GetChars(binary, characters);
                value.SetLength(length);

                return value;
            }
        }

        public override TString Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType is JsonTokenType.Null)
                return default;

            if (reader.TokenType is not JsonTokenType.String)
                throw new JsonException($"Cannot Convert {reader.TokenType} to Fixed String");

            return ReadValue(ref reader);
        }
        public override void Write(Utf8JsonWriter writer, TString value, JsonSerializerOptions options)
        {
            var characters = value.AsSpan();
            writer.WriteStringValue(characters);
        }
    }
}
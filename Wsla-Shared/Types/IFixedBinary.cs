using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Wsla.Serialization;

namespace Wsla
{
    public interface IFixedBinary
    {
        byte Length { get; }
        void SetLength(int value);

        byte Max { get; }

        /// <summary>
        /// Get the total span that this fixed binary has access to (the entire fixed storage)
        /// </summary>
        /// <returns></returns>
        Span<byte> GetTotalSpan();

        /// <summary>
        /// Gets a span of the used characters in this fixed binary
        /// </summary>
        /// <returns></returns>
        Span<byte> GetUsedSpan();

        ReadOnlySpan<byte> AsSpan();
    }
    public struct FixedBinary<TStorage> : IFixedBinary
        where TStorage : struct, IFixedBinaryStorage
    {
        TStorage Storage;

        public byte Length { get; private set; }
        /// <summary>
        /// Sets the length of this fixed string while respecting its Storage's max value
        /// </summary>
        /// <param name="value"></param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public void SetLength(int value)
        {
            if (value < 0 || value > Storage.Max)
                throw new ArgumentOutOfRangeException($"Fixed String {typeof(TStorage).Name} Only  Supports Length of [{0}-{Storage.Max}]");

            Length = (byte)value;
        }

        public byte Max => Storage.Max;

        #region Span
        public Span<byte> GetTotalSpan() => Storage.GetSpan();
        public Span<byte> GetUsedSpan() => Storage.GetSpan().Slice(0, Length);

        public ReadOnlySpan<byte> AsSpan() => GetUsedSpan();

        public void Assign(ReadOnlySpan<byte> input)
        {
            SetLength(input.Length);

            var destination = GetTotalSpan();
            input.CopyTo(destination);
        }
        #endregion

        public FixedBinary<TStorage> Clone()
        {
            var characters = AsSpan();
            return new FixedBinary<TStorage>(characters);
        }

        public FixedBinary(ReadOnlySpan<byte> input)
        {
            Unsafe.SkipInit(out this);

            Storage = new TStorage();

            Assign(input);
        }
    }

    public interface IFixedBinaryStorage
    {
        byte Max { get; }

        Span<byte> GetSpan();
    }
    public unsafe struct FB20 : IFixedBinaryStorage
    {
        public const byte Capacity = 20;

        fixed byte Buffer[Capacity];
        public byte Max => Capacity;

        Span<byte> IFixedBinaryStorage.GetSpan() => MemoryMarshal.CreateSpan(ref Buffer[0], Capacity);
    }
    public unsafe struct FB40 : IFixedBinaryStorage
    {
        public const byte Capacity = 40;

        fixed byte Buffer[Capacity];
        public byte Max => Capacity;

        Span<byte> IFixedBinaryStorage.GetSpan() => MemoryMarshal.CreateSpan(ref Buffer[0], Capacity);
    }
    public unsafe struct FB80 : IFixedBinaryStorage
    {
        public const byte Capacity = 80;

        fixed byte Buffer[Capacity];
        public byte Max => Capacity;

        Span<byte> IFixedBinaryStorage.GetSpan() => MemoryMarshal.CreateSpan(ref Buffer[0], Capacity);
    }
    public unsafe struct FB160 : IFixedBinaryStorage
    {
        public const byte Capacity = 160;

        fixed byte Buffer[Capacity];
        public byte Max => Capacity;

        Span<byte> IFixedBinaryStorage.GetSpan() => MemoryMarshal.CreateSpan(ref Buffer[0], Capacity);
    }

    [SourceGenerator]
    [SourceGenerator.Condition.ImplementsInterface(typeof(IFixedBinary))]
    [SourceGenerator.Builder.FromSourceType]
    public unsafe class FixedBinaryNetworkSerializationResolver<TBinary> : NetworkSerializationResolver<TBinary>
        where TBinary : IFixedBinary, new()
    {
        public override void Write(in TBinary value, ref BinarySource stream)
        {
            var length = value.Length;
            stream.WriteByte(length);

            var source = value.GetUsedSpan();
            var destination = stream.AllocateSpan(length);
            source.CopyTo(destination);
        }
        public override void Read(ref TBinary value, ref BinarySource stream)
        {
            var length = stream.ReadByte();
            value.SetLength(length);

            if (length is 0)
                return;

            var source = stream.ReadSpan(length);
            var destination = value.GetTotalSpan();

            source.CopyTo(destination);
        }
    }
}
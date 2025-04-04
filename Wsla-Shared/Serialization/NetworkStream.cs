using System;

namespace Wsla.Serialization
{
    public interface INetworkStream
    {
        int Position { get; set; }
        int Capacity { get; }
        int Available => (Capacity - Position);

        void EnsureFit(int extra);

        Memory<byte> GetMemory(int start, int length);
    }

    public static class NetworkStreamExtensions
    {
        public static Memory<byte> PeekAvailableMemory<TStream>(this TStream stream)
            where TStream : INetworkStream
        {
            return stream.GetMemory(stream.Position, stream.Available);
        }
        public static Memory<byte> PopAvailableMemory<TStream>(this TStream stream)
            where TStream : INetworkStream
        {
            var span = PeekAvailableMemory(stream);
            stream.Position += span.Length;
            return span;
        }

        public static Memory<byte> PeekAllocatedMemory<TStream>(this TStream stream)
            where TStream : INetworkStream
        {
            return stream.GetMemory(0, stream.Position);
        }

        public static Memory<byte> PopMemory<TStream>(this TStream stream, int length)
            where TStream : INetworkStream
        {
            stream.EnsureFit(length);

            var buffer = stream.GetMemory(stream.Position, length);
            stream.Position += length;
            return buffer;
        }

        public static ref byte PopByte<TStream>(this TStream stream)
            where TStream : INetworkStream
        {
            stream.EnsureFit(1);

            var buffer = stream.GetMemory(stream.Position, 1);
            stream.Position += 1;
            return ref buffer.Span[0];
        }
    }

    public ref struct BinarySource
    {
        StorageType Type;
        public enum StorageType
        {
            Stream, Span
        }

        INetworkStream Stream;

        SpanStream Span;
        public ref struct SpanStream
        {
            Span<byte> Buffer;
            public int Count => Buffer.Length;

            public int Position;

            public Span<byte> GetSpan(int start, int length) => Buffer.Slice(start, length);

            public SpanStream(Span<byte> Buffer)
            {
                this.Buffer = Buffer;
                Position = 0;
            }
        }

        public int Position
        {
            get
            {
                switch (Type)
                {
                    case StorageType.Stream:
                        return Stream.Position;

                    case StorageType.Span:
                        return Span.Position;

                    default:
                        throw new NotImplementedException();
                }
            }
            set
            {
                switch (Type)
                {
                    case StorageType.Stream:
                        Stream.Position = value;
                        break;

                    case StorageType.Span:
                        Span.Position = value;
                        break;

                    default:
                        throw new NotImplementedException();
                }
            }
        }
        public int Capacity
        {
            get
            {
                switch (Type)
                {
                    case StorageType.Stream:
                        return Stream.Capacity;

                    case StorageType.Span:
                        return Span.Count;

                    default:
                        throw new NotImplementedException();
                }
            }
        }

        public int Available => (Capacity - Position);

        public void EnsureFit(int size)
        {
            switch (Type)
            {
                case StorageType.Stream:
                    Stream.EnsureFit(size);
                    break;

                case StorageType.Span:
                {
                    if (size > Available)
                        throw new InvalidOperationException($"Can't Expand Span Source");
                    else
                        break;
                }

                default:
                    throw new NotImplementedException();
            }
        }

        public Span<byte> GetSpan(int start, int length)
        {
            switch (Type)
            {
                case StorageType.Stream:
                    return Stream.GetMemory(start, length).Span;

                case StorageType.Span:
                    return Span.GetSpan(start, length);

                default:
                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Returns the complete unread span, no read position adjustment
        /// </summary>
        /// <returns></returns>
        public Span<byte> PeekAllocatedSpan() => GetSpan(Position, Available);

        /// <summary>
        /// Returns the total span (read + unread), no read position adjusted
        /// </summary>
        /// <returns></returns>
        public Span<byte> PeekTotalSpan() => GetSpan(0, Capacity);

        /// <summary>
        /// Reads the specified length span and advances position
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        public Span<byte> ReadSpan(int length)
        {
            var span = GetSpan(Position, length);

            Position += length;

            return span;
        }

        /// <summary>
        /// Returns a span of length and advances the position, ensures length fit
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        public Span<byte> AllocateSpan(int length)
        {
            EnsureFit(length);

            return ReadSpan(length);
        }

        public void WriteByte(byte value)
        {
            EnsureFit(1);

            ReadSpan(1)[0] = value;
        }
        public byte ReadByte()
        {
            return ReadSpan(1)[0];
        }

        public BinarySource(INetworkStream Stream)
        {
            Type = StorageType.Stream;

            this.Stream = Stream;
            Span = default;
        }
        public BinarySource(Span<byte> Span)
        {
            Type = StorageType.Span;

            this.Span = new SpanStream(Span);
            Stream = default;
        }

        public static BinarySource From(INetworkStream stream) => new(stream);
        public static BinarySource From(Span<byte> span) => new(span);
        public static BinarySource From<TBinary>(ref TBinary binary)
            where TBinary : IFixedBinary
        {
            var span = binary.GetTotalSpan();
            return new(span);
        }
    }
}
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
}
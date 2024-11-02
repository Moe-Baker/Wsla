using System;

namespace Wsla.Serialization
{
    public interface INetworkStream
    {
        int Position { get; set; }
        int Available { get; }

        void EnsureFit(int extra);

        Span<byte> GetSpan(int start, int length);
    }

    public static class NetworkStreamExtensions
    {
        public static Span<byte> PeekAvailableSpan<TStream>(this TStream stream)
            where TStream : INetworkStream
        {
            return stream.GetSpan(stream.Position, stream.Available);
        }
        public static Span<byte> PopAvailableSpan<TStream>(this TStream stream)
            where TStream : INetworkStream
        {
            var span = PeekAvailableSpan(stream);
            stream.Position += span.Length;
            return span;
        }

        public static Span<byte> PeekAllocatedSpan<TStream>(this TStream stream)
            where TStream : INetworkStream
        {
            return stream.GetSpan(0, stream.Position);
        }

        public static Span<byte> PopSpan<TStream>(this TStream stream, int length)
            where TStream : INetworkStream
        {
            stream.EnsureFit(length);

            var buffer = stream.GetSpan(stream.Position, length);
            stream.Position += length;
            return buffer;
        }

        public static byte PopByte<TStream>(this TStream stream)
            where TStream : INetworkStream
        {
            if (stream.Available < 1)
                throw new IndexOutOfRangeException($"No Available Byte Left to Read");

            var buffer = stream.GetSpan(stream.Position, 1);
            stream.Position += 1;
            return buffer[0];
        }
    }
}
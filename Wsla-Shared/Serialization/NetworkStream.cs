using System;

namespace Wsla.Serialization
{
    public interface INetworkStream
    {
        void Advance(int count);

        /// <summary>
        /// Returns the remaining capacity available without advancing the stream
        /// </summary>
        /// <returns></returns>
        Span<byte> GetRemaining();

        /// <summary>
        /// Returns the a span with the specified size & andvances the stream
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        Span<byte> Take(int size);
    }

    public class NetworkStream : INetworkStream
    {
        public byte[] Data { get; }

        public int Position { get; private set; }

        public int Remaining => Data.Length - Position;

        public void Advance(int count)
        {
            if (count > Remaining)
                throw new ArgumentOutOfRangeException($"Advancement count of {count} Bigger than the Remaing capacity of {Remaining}");

            Position += count;
        }

        public Span<byte> GetRemaining() => new Span<byte>(Data, Position, Remaining);

        public Span<byte> Take(int size)
        {
            var span = new Span<byte>(Data, Position, size);
            Position += size;
            return span;
        }

        public void Reset() => Position = 0;

        public NetworkStream(int count) : this(new byte[count]) { }
        public NetworkStream(byte[] Data)
        {
            this.Data = Data;
        }
    }
}
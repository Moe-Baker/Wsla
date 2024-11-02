using System;

using Wsla.Serialization;

namespace LiteNetLib.Utils
{
    partial class NetDataReader : INetworkStream
    {
        int INetworkStream.Position
        {
            get => _position;
            set => _position = value;
        }

        public int Available => _dataSize - _position;

        public Span<byte> GetSpan(int start, int length) => _data.AsSpan(start, length);

        public void EnsureFit(int extra)
        {
#if DEBUG
            if (extra > Available)
                throw new ArgumentOutOfRangeException($"Can't Read More Data than Availabile in Net Data Reader, Available: {Available}, Required: {extra}");
#endif
        }
    }
}
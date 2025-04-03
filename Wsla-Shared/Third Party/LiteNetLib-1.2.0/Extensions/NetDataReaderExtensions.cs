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

        int INetworkStream.Capacity => _dataSize;

        Memory<byte> INetworkStream.GetMemory(int start, int length) => _data.AsMemory(start, length);

        void INetworkStream.EnsureFit(int extra)
        {
#if DEBUG
            if (extra > AvailableBytes)
                throw new ArgumentOutOfRangeException($"Can't Read More Data than Available in Net Data Reader, Available: {AvailableBytes}, Required: {extra}");
#endif
        }
    }
}
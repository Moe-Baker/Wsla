using System;

using Wsla.Serialization;

namespace LiteNetLib.Utils
{
    partial class NetDataWriter : INetworkStream
    {
        int INetworkStream.Position
        {
            get => _position;
            set => _position = value;
        }

        int INetworkStream.Capacity => _data.Length;

        Memory<byte> INetworkStream.GetMemory(int start, int length) => Data.AsMemory(start, length);

        void INetworkStream.EnsureFit(int extra) => EnsureFit(extra);
    }
}
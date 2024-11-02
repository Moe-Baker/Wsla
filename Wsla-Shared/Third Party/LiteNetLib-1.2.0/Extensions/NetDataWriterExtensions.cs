using System;

using Wsla.Serialization;

namespace LiteNetLib.Utils
{
    partial class NetDataWriter : INetworkStream
    {
        public int Position
        {
            get => _position;
            set => _position = value;
        }

        public int Available => _data.Length - _position;

        public Span<byte> GetSpan(int start, int length) => Data.AsSpan(start, length);
    }
}
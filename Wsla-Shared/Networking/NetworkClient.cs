using System;

using MemoryPack;

namespace Wsla.Shared.Global
{
    [MemoryPackable]
    public partial struct NetworkClientData
    {
        public NetworkClientID ID { get; }
        public string Username { get; }

        public NetworkClientData(NetworkClientID id, string username)
        {
            this.ID = id;
            this.Username = username;
        }
    }

    [MemoryPackable]
    public partial struct NetworkClientID : IEquatable<NetworkClientID>
    {
        public byte Value { get; }

        public const byte MaxValue = byte.MaxValue;

        public override bool Equals(object obj)
        {
            if (obj is NetworkClientID other)
                return Equals(other);

            return false;
        }
        public bool Equals(NetworkClientID other)
        {
            return Value == other.Value;
        }

        public override int GetHashCode() => Value;

        public override string ToString() => Value.ToString();

        public NetworkClientID(byte value)
        {
            this.Value = value;
        }

        public static bool operator ==(NetworkClientID left, NetworkClientID right) => left.Equals(right);
        public static bool operator !=(NetworkClientID left, NetworkClientID right) => !left.Equals(right);

        public static bool Increment(ref NetworkClientID index, out NetworkClientID key)
        {
            if (index.Value >= MaxValue)
            {
                key = default;
                return false;
            }

            key = index;
            index = new NetworkClientID((byte)(index.Value + 1));

            return true;
        }
    }
}
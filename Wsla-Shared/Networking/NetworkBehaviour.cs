using System;

using MemoryPack;

namespace Wsla.Shared.Global
{
    [MemoryPackable]
    public partial struct NetworkBehaviourID : IEquatable<NetworkBehaviourID>
    {
        public byte Value { get; }

        public const byte MaxValue = byte.MaxValue;

        public override bool Equals(object obj)
        {
            if (obj is NetworkBehaviourID other)
                return Equals(other);

            return false;
        }
        public bool Equals(NetworkBehaviourID other)
        {
            return Value == other.Value;
        }

        public override int GetHashCode() => Value;

        public override string ToString() => Value.ToString();

        public NetworkBehaviourID(byte value)
        {
            this.Value = value;
        }

        public static bool operator ==(NetworkBehaviourID left, NetworkBehaviourID right) => left.Equals(right);
        public static bool operator !=(NetworkBehaviourID left, NetworkBehaviourID right) => !left.Equals(right);

        public static bool Increment(ref NetworkBehaviourID index, out NetworkBehaviourID key)
        {
            if (index.Value >= MaxValue)
            {
                key = default;
                return false;
            }

            key = index;
            index = new NetworkBehaviourID((byte)(index.Value + 1));

            return true;
        }
    }
}
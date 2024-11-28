using System;

using Wsla.Serialization;

namespace Wsla
{
    [NetworkBlittable]
    public partial struct NetworkBehaviourID : IEquatable<NetworkBehaviourID>
    {
        public byte Value { get; }

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

        public static NetworkBehaviourID Min { get; } = new(byte.MinValue);
        public static NetworkBehaviourID Max { get; } = new(byte.MaxValue - 1);
        public static NetworkBehaviourID None { get; } = new(byte.MaxValue);

        public static bool operator ==(NetworkBehaviourID left, NetworkBehaviourID right) => left.Equals(right);
        public static bool operator !=(NetworkBehaviourID left, NetworkBehaviourID right) => !left.Equals(right);

        public static bool Increment(ref NetworkBehaviourID index, out NetworkBehaviourID key)
        {
            if (index.Value >= Max.Value)
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
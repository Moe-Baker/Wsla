using System;

using Wsla.Serialization;

namespace Wsla
{
    [Serializable]
    [NetworkBlittable]
    public partial struct NetworkEntityID : IEquatable<NetworkEntityID>
    {
        public ushort Value { get; private set; }

        public const ushort MaxValue = ushort.MaxValue;

        public override bool Equals(object obj)
        {
            if (obj is NetworkEntityID other)
                return Equals(other);

            return false;
        }
        public bool Equals(NetworkEntityID other)
        {
            return Value == other.Value;
        }

        public override int GetHashCode() => Value;

        public override string ToString() => Value.ToString();

        public NetworkEntityID(ushort value)
        {
            this.Value = value;
        }

        public static bool operator ==(NetworkEntityID left, NetworkEntityID right) => left.Equals(right);
        public static bool operator !=(NetworkEntityID left, NetworkEntityID right) => !left.Equals(right);

        public static bool Increment(ref NetworkEntityID index, out NetworkEntityID key)
        {
            if (index.Value >= MaxValue)
            {
                key = default;
                return false;
            }

            key = index;
            index = new NetworkEntityID((ushort)(index.Value + 1));

            return true;
        }
    }

    [Serializable]
    [NetworkBlittable]
    public partial struct NetworkEntityResource : IEquatable<NetworkEntityResource>
    {
        public ushort Value { get; }

        public const ushort MaxValue = ushort.MaxValue;

        public override bool Equals(object obj)
        {
            if (obj is NetworkEntityResource other)
                return Equals(other);

            return false;
        }
        public bool Equals(NetworkEntityResource other)
        {
            return Value == other.Value;
        }

        public override int GetHashCode() => Value;

        public override string ToString() => Value.ToString();

        public NetworkEntityResource(ushort value)
        {
            this.Value = value;
        }

        public static bool operator ==(NetworkEntityResource left, NetworkEntityResource right) => left.Equals(right);
        public static bool operator !=(NetworkEntityResource left, NetworkEntityResource right) => !left.Equals(right);
    }

    public enum NetworkEntitySource : byte
    {
        Prefab = 1,
        Scene = 2,
    }
}
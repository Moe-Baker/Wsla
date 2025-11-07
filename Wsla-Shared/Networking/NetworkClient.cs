using System;

using Wsla.Serialization;

namespace Wsla
{
    [Serializable]
    [NetworkBlittable]
    public partial struct NetworkClientID : IEquatable<NetworkClientID>
    {
        public byte Value { get; private set; }

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

        public static NetworkClientID Min { get; } = new(byte.MinValue);
        public static NetworkClientID Max { get; } = new(byte.MaxValue - 1);

        public static NetworkClientID None { get; } = new(byte.MaxValue);

        public static bool operator ==(NetworkClientID left, NetworkClientID right) => left.Equals(right);
        public static bool operator !=(NetworkClientID left, NetworkClientID right) => !left.Equals(right);

        public static NetworkClientID Increment(NetworkClientID index) => new NetworkClientID((byte)(index.Value + 1));
    }

    [Serializable]
    public partial struct NetworkClientDefinition : IAutoNetworkSerialization
    {
        public NetworkClientID ID;
        public FixedString<FS20> Username;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref ID);
            context.Select(ref Username);
        }

        public NetworkClientDefinition(NetworkClientID ID, FixedString<FS20> Username)
        {
            this.ID = ID;
            this.Username = Username;
        }
    }
}
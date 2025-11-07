using System;

using Wsla.Serialization;

namespace Wsla
{
    [NetworkBlittable]
    public struct NetworkSceneID : IEquatable<NetworkSceneID>
    {
        public byte Value { get; }

        public const byte MaxValue = byte.MaxValue;

        public override bool Equals(object obj)
        {
            if (obj is NetworkSceneID other)
                return Equals(other);

            return false;
        }
        public bool Equals(NetworkSceneID other)
        {
            return Value == other.Value;
        }

        public override int GetHashCode() => Value;

        public override string ToString() => Value.ToString();

        public NetworkSceneID(byte value)
        {
            this.Value = value;
        }

        public static bool operator ==(NetworkSceneID left, NetworkSceneID right) => left.Equals(right);
        public static bool operator !=(NetworkSceneID left, NetworkSceneID right) => !left.Equals(right);

        public static NetworkSceneID From(int index) => new NetworkSceneID((byte)index);
    }

    [NetworkBlittable]
    public struct NetworkSceneVersion : IEquatable<NetworkSceneVersion>
    {
        public byte Value { get; }

        public const byte MaxValue = byte.MaxValue;

        public override bool Equals(object obj)
        {
            if (obj is NetworkSceneVersion other)
                return Equals(other);

            return false;
        }
        public bool Equals(NetworkSceneVersion other)
        {
            return Value == other.Value;
        }

        public override int GetHashCode() => Value;

        public override string ToString() => Value.ToString();

        public NetworkSceneVersion(byte value)
        {
            this.Value = value;
        }

        public static NetworkSceneVersion Increment(NetworkSceneVersion key)
        {
            var index = key.Value;

            if (index >= MaxValue)
                index = 0;
            else
                index += 1;

            return new NetworkSceneVersion(index);
        }

        public static bool operator ==(NetworkSceneVersion left, NetworkSceneVersion right) => left.Equals(right);
        public static bool operator !=(NetworkSceneVersion left, NetworkSceneVersion right) => !left.Equals(right);
    }

    public struct NetworkSceneDefinition : IAutoNetworkSerialization
    {
        public NetworkSceneID ID;
        public NetworkSceneVersion Version;
        public bool IsSpawned;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref ID);
            context.Select(ref Version);
            context.Select(ref IsSpawned);
        }

        public NetworkSceneDefinition(NetworkSceneID ID, NetworkSceneVersion Version, bool IsSpawned)
        {
            this.ID = ID;
            this.Version = Version;
            this.IsSpawned = IsSpawned;
        }
    }
}
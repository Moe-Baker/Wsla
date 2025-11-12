using System;

using Wsla.Serialization;

namespace Wsla
{
    [Serializable]
    [NetworkBlittable]
    public struct NetworkSceneID : IEquatable<NetworkSceneID>
    {
        public byte Index { get; private set; }
        public NetworkSceneSource Source { get; private set; }

        public override bool Equals(object obj)
        {
            if (obj is NetworkSceneID other)
                return Equals(other);

            return false;
        }
        public bool Equals(NetworkSceneID other)
        {
            return (Index == other.Index) && (other.Source == Source);
        }

        public override int GetHashCode() => HashCode.Combine(Index, Source);

        public override string ToString() => $"[{Index} | {Source}]";

        public NetworkSceneID(byte Index, NetworkSceneSource Source)
        {
            this.Index = Index;
            this.Source = Source;
        }

        public static bool operator ==(NetworkSceneID left, NetworkSceneID right) => left.Equals(right);
        public static bool operator !=(NetworkSceneID left, NetworkSceneID right) => !left.Equals(right);

        public static NetworkSceneID FromBuild(int index) => new NetworkSceneID((byte)index, NetworkSceneSource.Build);
        public static NetworkSceneID FromAddressable(int index) => new NetworkSceneID((byte)index, NetworkSceneSource.Addressable);
    }

    public enum NetworkSceneSource : byte
    {
        Build, Addressable
    }

    [Serializable]
    [NetworkBlittable]
    public struct NetworkSceneVersion : IEquatable<NetworkSceneVersion>
    {
        public byte Value { get; private set; }

        public const byte MinValue = byte.MinValue;
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

        public static NetworkSceneVersion Min { get; } = new(MinValue);
        public static NetworkSceneVersion Max { get; } = new(MaxValue);

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

    public struct NetworkSceneState : IAutoNetworkSerialization
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

        public NetworkSceneState(NetworkSceneID ID, NetworkSceneVersion Version, bool IsSpawned)
        {
            this.ID = ID;
            this.Version = Version;
            this.IsSpawned = IsSpawned;
        }
    }

    [NetworkBlittable]
    public struct NetworkSceneDefinition
    {
        public NetworkSceneID ID;
        public NetworkSceneVersion Version;

        public NetworkSceneDefinition(NetworkSceneID ID, NetworkSceneVersion Version)
        {
            this.ID = ID;
            this.Version = Version;
        }
    }
}
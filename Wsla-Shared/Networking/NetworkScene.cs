using System;

using MemoryPack;

namespace Wsla.Shared.Global
{
    [MemoryPackable]
    public partial struct NetworkSceneID : IEquatable<NetworkSceneID>
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
    }

    public enum NetworkSceneLoadMode
    {
        Single = 0,
        Additive = 1,
    }
}
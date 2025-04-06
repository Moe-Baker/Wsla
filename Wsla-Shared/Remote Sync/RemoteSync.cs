using System;

using Wsla.Serialization;

namespace Wsla
{
    [NetworkBlittable]
    public partial struct NetworkSyncMemberID : IEquatable<NetworkSyncMemberID>
    {
        public byte Value { get; }

        public const byte MaxValue = byte.MaxValue;

        public override bool Equals(object obj)
        {
            if (obj is NetworkSyncMemberID other)
                return Equals(other);

            return false;
        }
        public bool Equals(NetworkSyncMemberID other)
        {
            return Value == other.Value;
        }

        public override int GetHashCode() => Value;

        public override string ToString() => Value.ToString();

        public NetworkSyncMemberID(byte value)
        {
            this.Value = value;
        }

        public static bool operator ==(NetworkSyncMemberID left, NetworkSyncMemberID right) => left.Equals(right);
        public static bool operator !=(NetworkSyncMemberID left, NetworkSyncMemberID right) => !left.Equals(right);

        public static bool Increment(ref NetworkSyncMemberID index, out NetworkSyncMemberID key)
        {
            if (index.Value >= MaxValue)
            {
                key = default;
                return false;
            }

            key = index;
            index = new NetworkSyncMemberID((byte)(index.Value + 1));

            return true;
        }
    }

    public enum SyncMemberType : byte
    {
        RPC = 0,
        Variable = 1,
    }

    public enum RemoteBufferMode : byte
    {
        None, Buffer
    }

    [NetworkBlittable]
    public struct NetworkSyncMemberParameters
    {
        public NetworkEntityID Entity;
        public NetworkBehaviourID Behaviour;
        public NetworkSyncMemberID Member;

        public override string ToString() => $"(Entity: {Entity}, Behaviour:{Behaviour}, Member: {Member})";

        public NetworkSyncMemberParameters(NetworkEntityID Entity, NetworkBehaviourID Behaviour, NetworkSyncMemberID Member)
        {
            this.Entity = Entity;
            this.Behaviour = Behaviour;
            this.Member = Member;
        }
    }
}
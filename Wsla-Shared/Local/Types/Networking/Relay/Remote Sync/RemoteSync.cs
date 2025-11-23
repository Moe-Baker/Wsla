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

    public static class NetworkSyncMemberBufferPayload
    {
        public ref struct EntityWriter
        {
            public MemberWriter Write(NetworkEntityID id, ushort count)
            {
                NetworkSerializer.WriteValue(id, Stream);
                NetworkSerializer.WriteValue(count, Stream);

                return new MemberWriter(Stream);
            }

            public void Dispose()
            {
                //End of Stream
                NetworkSerializer.WriteValue(NetworkEntityID.None, Stream);
            }

            readonly INetworkStream Stream;
            public EntityWriter(INetworkStream Stream)
            {
                this.Stream = Stream;
            }
        }
        public ref struct MemberWriter
        {
            public void Write(NetworkBehaviourID behaviour, NetworkSyncMemberID member, NetworkClientID sender, Span<byte> data)
            {
                NetworkSerializer.WriteValue(behaviour, Stream);
                NetworkSerializer.WriteValue(member, Stream);
                NetworkSerializer.WriteValue(sender, Stream);

                if (data.Length > 0)
                {
                    var destination = Stream.AllocateMemory(data.Length);
                    data.CopyTo(destination.Span);
                }
            }

            public void Dispose()
            {

            }

            readonly INetworkStream Stream;
            public MemberWriter(INetworkStream Stream)
            {
                this.Stream = Stream;
            }
        }

        public ref struct EntityReader
        {
            public bool TryReadID(out NetworkEntityID entity)
            {
                entity = NetworkSerializer.ReadValue<NetworkEntityID>(Stream);
                return entity != NetworkEntityID.None;
            }
            public MemberReader ReadMember(NetworkEntityID entity)
            {
                var count = NetworkSerializer.ReadValue<ushort>(Stream);
                return new MemberReader(Stream, entity, count);
            }

            public void Dispose()
            {

            }

            readonly INetworkStream Stream;
            public EntityReader(INetworkStream Stream)
            {
                this.Stream = Stream;
            }
        }
        public ref struct MemberReader
        {
            public NetworkEntityID Entity { get; }
            public int Count { get; }

            int Index;

            public void Read(out NetworkBehaviourID behaviour, out NetworkSyncMemberID member, out NetworkClientID sender, out INetworkStream data)
            {
                Index += 1;

                behaviour = NetworkSerializer.ReadValue<NetworkBehaviourID>(Stream);
                member = NetworkSerializer.ReadValue<NetworkSyncMemberID>(Stream);
                sender = NetworkSerializer.ReadValue<NetworkClientID>(Stream);
                data = Stream;
            }

            public void Dispose()
            {
                if (Count != Index)
                    throw new InvalidOperationException($"({typeof(MemberReader).FullName}) Mismatched Read, Read {Index}, Expected {Count}");
            }

            readonly INetworkStream Stream;
            public MemberReader(INetworkStream Stream, NetworkEntityID Entity, int Count)
            {
                this.Stream = Stream;
                this.Entity = Entity;
                this.Count = Count;

                Index = default;
            }
        }
    }
}
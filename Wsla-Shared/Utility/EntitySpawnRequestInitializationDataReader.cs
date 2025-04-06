using System;

using Wsla.Serialization;

namespace Wsla
{
    public ref struct EntitySpawnRequestInitializationDataReader
    {
        INetworkStream Stream;

        public Entry Current { get; private set; }

        public EntitySpawnRequestInitializationDataReader GetEnumerator() => this;

        public bool MoveNext()
        {
            if (Stream.Available is 0)
                return false;

            var behaviour = NetworkSerializer.ReadValue<NetworkBehaviourID>(Stream);

            var type = NetworkSerializer.ReadValue<SyncMemberType>(Stream);

            var member = NetworkSerializer.ReadValue<NetworkSyncMemberID>(Stream);

            var length = NetworkSerializer.ReadValue<ushort>(Stream);

            var binary = Stream.ReadMemory(length);

            Current = new Entry(behaviour, type, member, binary);
            return true;
        }

        public EntitySpawnRequestInitializationDataReader(INetworkStream Stream)
        {
            this.Stream = Stream;
            Current = default;
        }

        public struct Entry
        {
            public NetworkBehaviourID Behaviour { get; }
            public SyncMemberType Type { get; }
            public NetworkSyncMemberID Member { get; }
            public Memory<byte> Binary { get; }

            public Entry(NetworkBehaviourID Behaviour, SyncMemberType Type, NetworkSyncMemberID Member, Memory<byte> Binary)
            {
                this.Behaviour = Behaviour;
                this.Type = Type;
                this.Member = Member;
                this.Binary = Binary;
            }
        }
    }
}
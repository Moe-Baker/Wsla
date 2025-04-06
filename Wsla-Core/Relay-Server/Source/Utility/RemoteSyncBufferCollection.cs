using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using LiteNetLib.Utils;

using Wsla.Serialization;

namespace Wsla.Server
{
    public struct RemoteSyncBufferCollection : IDisposable
    {
        Dictionary<Key, Payload> Collection;
        public readonly struct Key : IEquatable<Key>
        {
            public NetworkBehaviourID Behaviour { get; }
            public NetworkSyncMemberID Member { get; }

            public override bool Equals(object obj)
            {
                if (obj is Key other)
                    return Equals(other);

                return false;
            }
            public bool Equals(Key other)
            {
                return Behaviour == other.Behaviour && Member.Equals(other.Member);
            }

            readonly int HashCode;
            public override int GetHashCode() => HashCode;

            public Key(NetworkBehaviourID Behaviour, NetworkSyncMemberID Member)
            {
                this.Behaviour = Behaviour;
                this.Member = Member;

                HashCode = (Behaviour.Value << 4) | (Member.Value);
            }
        }
        public struct Payload
        {
            public NetworkClientID SenderID;
            public NetworkClientVersion SenderVersion;

            public readonly NetDataWriter Stream;

            public void SetSender(NetworkClient sender)
            {
                SenderID = sender.ID;
                SenderVersion = sender.Version;
            }

            public Payload(NetDataWriter Stream)
            {
                Unsafe.SkipInit(out this);

                this.Stream = Stream;
            }
        }

        public ushort Count => Collection is null ? (ushort)0 : (ushort)Collection.Count;

        public void Register(NetworkClient sender, NetworkBehaviourID behaviour, NetworkSyncMemberID member, NetDataReader Input)
        {
            var binary = Input.PeekAvailableMemory();
            Register(sender, behaviour, member, binary.Span);
        }
        public void Register(NetworkClient sender, NetworkBehaviourID behaviour, NetworkSyncMemberID member, ReadOnlySpan<byte> binary)
        {
            if (Collection is null)
                Collection = new(1);

            var key = new Key(behaviour, member);

            ref var payload = ref CollectionsMarshal.GetValueRefOrAddDefault(Collection, key, out var exists);

            if (exists is false)
            {
                if (binary.Length is 0)
                {
                    payload = new Payload(default);
                }
                else
                {
                    var writer = Room.Pools.MultiPackerWriter.Retrieve();
                    payload = new Payload(writer);
                }
            }

            payload.SetSender(sender);

            //Copy Buffer
            if (binary.Length > 0)
            {
                if (payload.Stream is null)
                {
                    //MOBO: Disconnect Client?
                    //Can only happen if an RPC was buffered without any binary data, then buffered with
                    NetworkLog.Error($"No Payload Stream Defined for Buffer Member [Behaviour: {behaviour} | Member: {member}]");
                    return;
                }

                payload.Stream.SetPosition(0);
                var destination = payload.Stream.AllocateMemory(binary.Length);
                binary.CopyTo(destination.Span);
            }
        }

        public void WriteState(NetworkEntityID entity, NetDataWriter output)
        {
            if (Collection is null)
                return;

            NetworkSerializer.WriteValue(in entity, output);

            NetworkSerializer.WriteValue(Count, output);

            foreach (var (key, payload) in Collection)
            {
                //Write Key
                {
                    NetworkSerializer.WriteValue(key.Behaviour, output);
                    NetworkSerializer.WriteValue(key.Member, output);
                }

                //Write Sender
                {
                    if (TryGetClient(payload.SenderID, payload.SenderVersion, out var client))
                        NetworkSerializer.WriteValue(client.ID, output);
                    else
                        NetworkSerializer.WriteValue(NetworkClientID.None, output);
                }

                //Write Payload
                if (payload.Stream is not null)
                {
                    var source = payload.Stream.PeekAllocatedMemory();
                    var destination = output.AllocateMemory(source.Length);
                    source.CopyTo(destination);
                }
            }
        }

        bool TryGetClient(NetworkClientID id, NetworkClientVersion version, out NetworkClient client)
        {
            if (Room.Clients.TryGet(id, out client) is false)
                return false;

            if (client.Version != version)
                return false;

            return true;
        }

        public void Dispose()
        {
            if (Collection is null)
                return;

            foreach (var (key, payload) in Collection)
            {
                if (payload.Stream is null)
                    continue;

                Room.Pools.MultiPackerWriter.Return(payload.Stream);
            }
        }

        readonly Room Room;
        public RemoteSyncBufferCollection(Room Room)
        {
            this.Room = Room;

            Collection = null;
        }
    }
}
using System;

using Wsla.Serialization;

namespace Wsla
{
    [NetworkBlittable]
    public partial struct NetworkRpcID : IEquatable<NetworkRpcID>
    {
        public byte Value { get; }

        public const byte MaxValue = byte.MaxValue;

        public override bool Equals(object obj)
        {
            if (obj is NetworkRpcID other)
                return Equals(other);

            return false;
        }
        public bool Equals(NetworkRpcID other)
        {
            return Value == other.Value;
        }

        public override int GetHashCode() => Value;

        public override string ToString() => Value.ToString();

        public NetworkRpcID(byte value)
        {
            this.Value = value;
        }

        public static bool operator ==(NetworkRpcID left, NetworkRpcID right) => left.Equals(right);
        public static bool operator !=(NetworkRpcID left, NetworkRpcID right) => !left.Equals(right);

        public static bool Increment(ref NetworkRpcID index, out NetworkRpcID key)
        {
            if (index.Value >= MaxValue)
            {
                key = default;
                return false;
            }

            key = index;
            index = new NetworkRpcID((byte)(index.Value + 1));

            return true;
        }
    }

    [NetworkBlittable]
    public struct BroadcastNetworkRpcRequest
    {
        public RemoteBufferMode Buffer;
        public NetworkRpcParameters Parameters;

        public BroadcastNetworkRpcRequest(RemoteBufferMode Buffer, NetworkRpcParameters Parameters)
        {
            this.Buffer = Buffer;
            this.Parameters = Parameters;
        }
    }

    [NetworkBlittable]
    public struct BufferNetworkRpcRequest
    {
        public RemoteBufferMode Buffer;
        public NetworkRpcParameters Parameters;

        public BufferNetworkRpcRequest(RemoteBufferMode Buffer, NetworkRpcParameters Parameters)
        {
            this.Buffer = Buffer;
            this.Parameters = Parameters;
        }
    }

    [NetworkBlittable]
    public struct TargetNetworkRpcRequest
    {
        public NetworkClientID Target;
        public NetworkRpcParameters Parameters;

        public TargetNetworkRpcRequest(NetworkClientID Target, NetworkRpcParameters Parameters)
        {
            this.Target = Target;
            this.Parameters = Parameters;
        }
    }

    [NetworkBlittable]
    public struct NetworkRpcCommand
    {
        public NetworkClientID Sender;
        public NetworkRpcParameters Parameters;

        public NetworkRpcCommand(NetworkClientID Sender, NetworkRpcParameters Parameters)
        {
            this.Sender = Sender;
            this.Parameters = Parameters;
        }
    }

    [NetworkBlittable]
    public struct NetworkRpcParameters
    {
        public NetworkEntityID Entity;
        public NetworkBehaviourID Behaviour;
        public NetworkRpcID RPC;

        public override string ToString() => $"(Entity: {Entity}, Behaviour:{Behaviour}, RPC: {RPC})";

        public NetworkRpcParameters(NetworkEntityID Entity, NetworkBehaviourID Behaviour, NetworkRpcID RPC)
        {
            this.Entity = Entity;
            this.Behaviour = Behaviour;
            this.RPC = RPC;
        }
    }
}
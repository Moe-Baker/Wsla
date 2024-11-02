using System;

using Wsla.Serialization;

namespace Wsla
{
    [NetworkBlittable]
    public partial struct NetworkVariableID : IEquatable<NetworkVariableID>, IRemoteSyncMemberID
    {
        public byte Value { get; }

        public const byte MaxValue = byte.MaxValue;

        public override bool Equals(object obj)
        {
            if (obj is NetworkVariableID other)
                return Equals(other);

            return false;
        }
        public bool Equals(NetworkVariableID other)
        {
            return Value == other.Value;
        }

        public override int GetHashCode() => Value;

        public override string ToString() => Value.ToString();

        public NetworkVariableID(byte value)
        {
            this.Value = value;
        }

        public static bool operator ==(NetworkVariableID left, NetworkVariableID right) => left.Equals(right);
        public static bool operator !=(NetworkVariableID left, NetworkVariableID right) => !left.Equals(right);

        public static bool Increment(ref NetworkVariableID index, out NetworkVariableID key)
        {
            if (index.Value >= MaxValue)
            {
                key = default;
                return false;
            }

            key = index;
            index = new NetworkVariableID((byte)(index.Value + 1));

            return true;
        }
    }

    [NetworkBlittable]
    public struct BroadcastNetworkVariableRequest
    {
        public NetworkVariableParameters Parameters;

        public BroadcastNetworkVariableRequest(NetworkVariableParameters Parameters)
        {
            this.Parameters = Parameters;
        }
    }

    [NetworkBlittable]
    public struct BufferNetworkVariableRequest
    {
        public NetworkVariableParameters Parameters;

        public BufferNetworkVariableRequest(NetworkVariableParameters Parameters)
        {
            this.Parameters = Parameters;
        }
    }

    [NetworkBlittable]
    public struct NetworkVariableCommand
    {
        public NetworkClientID Sender;
        public NetworkVariableParameters Parameters;

        public NetworkVariableCommand(NetworkClientID Sender, NetworkVariableParameters Parameters)
        {
            this.Sender = Sender;
            this.Parameters = Parameters;
        }
    }

    [NetworkBlittable]
    public struct NetworkVariableParameters
    {
        public NetworkEntityID Entity;
        public NetworkBehaviourID Behaviour;
        public NetworkVariableID Variable;

        public override string ToString() => $"(Entity: {Entity}, Behaviour:{Behaviour}, Variable: {Variable})";

        public NetworkVariableParameters(NetworkEntityID Entity, NetworkBehaviourID Behaviour, NetworkVariableID Variable)
        {
            this.Entity = Entity;
            this.Behaviour = Behaviour;
            this.Variable = Variable;
        }
    }
}
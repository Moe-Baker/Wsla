using System;

using Wsla.Serialization;

namespace Wsla
{
    [Serializable]
    [NetworkBlittable]
    public struct NetworkEntityID : IEquatable<NetworkEntityID>
    {
        public ushort Value { get; private set; }

        public const ushort MaxValue = ushort.MaxValue;

        public override bool Equals(object obj)
        {
            if (obj is NetworkEntityID other)
                return Equals(other);

            return false;
        }
        public bool Equals(NetworkEntityID other)
        {
            return Value == other.Value;
        }

        public override int GetHashCode() => Value;

        public override string ToString() => Value.ToString();

        public NetworkEntityID(ushort value)
        {
            this.Value = value;
        }

        public static NetworkEntityID Min { get; } = new(ushort.MinValue);
        public static NetworkEntityID Max { get; } = new(ushort.MaxValue - 1);

        public static NetworkEntityID None { get; } = new(ushort.MaxValue);

        public static bool operator ==(NetworkEntityID left, NetworkEntityID right) => left.Equals(right);
        public static bool operator !=(NetworkEntityID left, NetworkEntityID right) => !left.Equals(right);

        public static NetworkEntityID Increment(NetworkEntityID index) => new NetworkEntityID((ushort)(index.Value + 1));
    }

    [Serializable]
    [NetworkBlittable]
    public struct NetworkEntityResource : IEquatable<NetworkEntityResource>
    {
        public ushort Value { get; }

        public const ushort MaxValue = ushort.MaxValue;

        public override bool Equals(object obj)
        {
            if (obj is NetworkEntityResource other)
                return Equals(other);

            return false;
        }
        public bool Equals(NetworkEntityResource other)
        {
            return Value == other.Value;
        }

        public override int GetHashCode() => Value;

        public override string ToString() => Value.ToString();

        public NetworkEntityResource(ushort value)
        {
            this.Value = value;
        }

        public static NetworkEntityResource None { get; } = new NetworkEntityResource(MaxValue);

        public static bool operator ==(NetworkEntityResource left, NetworkEntityResource right) => left.Equals(right);
        public static bool operator !=(NetworkEntityResource left, NetworkEntityResource right) => !left.Equals(right);
    }

    public struct NetworkEntityDefinition : IAutoNetworkSerialization
    {
        public NetworkEntityID ID;
        public NetworkEntityOrigin Origin;
        public NetworkEntityResource Resource;

        public NetworkEntityAuthorityMode Authority;

        public NetworkClientID Owner;

        public NetworkEntityTransferToken TransferToken;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref ID);
            context.Select(ref Origin);
            context.Select(ref Resource);

            context.Select(ref Authority);
            if (Authority is not NetworkEntityAuthorityMode.Authoritative)
                context.Select(ref Owner);

            if (Authority is NetworkEntityAuthorityMode.Transferable)
                context.Select(ref TransferToken);
        }

        public NetworkEntityDefinition(NetworkEntityID ID, NetworkEntityOrigin Origin, NetworkEntityResource Resource, NetworkEntityAuthorityMode Authority, NetworkClientID Owner, NetworkEntityTransferToken TransferToken)
        {
            this.ID = ID;
            this.Origin = Origin;
            this.Resource = Resource;

            this.Authority = Authority;

            this.Owner = Owner;

            this.TransferToken = TransferToken;
        }
    }

    public enum NetworkEntityOrigin : byte
    {
        /// <summary>
        /// Entity originates from a prefab
        /// </summary>
        Prefab = 1,

        /// <summary>
        /// Entity originates from a scene
        /// </summary>
        Scene = 2,
    }

    public enum NetworkEntityAuthorityMode : byte
    {
        /// <summary>
        /// Authority is handled by the master client
        /// </summary>
        Authoritative = 0,

        /// <summary>
        /// Authority is always handled by the spawning client, entity lifetime is tied to said client
        /// </summary>
        Explicit = 1,

        /// <summary>
        /// Authority is handled by the spawning client with the ability to be transfered,
        /// prefab entity lifetime is tied to the owning client,
        /// scene entity lifetime is tied to scene lifetime; and the entity will be transfered to the master client on owner disconnect
        /// </summary>
        Transferable = 2,
    }

    [Serializable]
    [NetworkBlittable]
    public struct NetworkEntityTransferToken : IEquatable<NetworkEntityTransferToken>
    {
        public byte Value { get; private set; }

        public override bool Equals(object obj)
        {
            if (obj is NetworkEntityTransferToken other)
                return Equals(other);

            return false;
        }
        public bool Equals(NetworkEntityTransferToken other)
        {
            return Value == other.Value;
        }

        public override int GetHashCode() => Value;

        public override string ToString() => Value.ToString();

        public NetworkEntityTransferToken(byte value)
        {
            this.Value = value;
        }

        public static NetworkEntityTransferToken Zero { get; } = new(0);

        public static bool operator ==(NetworkEntityTransferToken left, NetworkEntityTransferToken right) => left.Equals(right);
        public static bool operator !=(NetworkEntityTransferToken left, NetworkEntityTransferToken right) => !left.Equals(right);

        public static NetworkEntityTransferToken Increment(NetworkEntityTransferToken index)
        {
            unchecked
            {
                return new NetworkEntityTransferToken((byte)(index.Value + 1));
            }
        }
    }
}
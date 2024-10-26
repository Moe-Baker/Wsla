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

        public static bool operator ==(NetworkEntityID left, NetworkEntityID right) => left.Equals(right);
        public static bool operator !=(NetworkEntityID left, NetworkEntityID right) => !left.Equals(right);

        public static bool Increment(ref NetworkEntityID index, out NetworkEntityID key)
        {
            if (index.Value >= MaxValue)
            {
                key = default;
                return false;
            }

            key = index;
            index = new NetworkEntityID((ushort)(index.Value + 1));

            return true;
        }
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

        public static bool operator ==(NetworkEntityResource left, NetworkEntityResource right) => left.Equals(right);
        public static bool operator !=(NetworkEntityResource left, NetworkEntityResource right) => !left.Equals(right);
    }

    [NetworkBlittable]
    public struct NetworkEntityDefinition : IAutoNetworkSerialization
    {
        public NetworkEntityID ID;
        public NetworkEntityOrigin Origin;
        public NetworkEntityResource Resource;

        public NetworkEntityAuthorityMode Authority;
        public NetworkEntityLifetimeMode Lifetime;

        public NetworkClientID Owner;
        public NetworkSceneID Scene;

        public bool HasScene => Lifetime is NetworkEntityLifetimeMode.Scene;
        public bool DefinesOwner => Authority is not NetworkEntityAuthorityMode.Authoritative;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref ID);
            context.Select(ref Origin);
            context.Select(ref Resource);

            context.Select(ref Authority);
            if (Authority is not NetworkEntityAuthorityMode.Authoritative)
                context.Select(ref Owner);

            context.Select(ref Lifetime);
            if (Lifetime is not NetworkEntityLifetimeMode.Scene)
                context.Select(ref Scene);
        }

        public NetworkEntityDefinition(NetworkEntityID ID, NetworkEntityOrigin Origin, NetworkEntityResource Resource, NetworkEntityAuthorityMode Authority, NetworkEntityLifetimeMode Lifetime, NetworkClientID Owner, NetworkSceneID Scene)
        {
            this.ID = ID;
            this.Origin = Origin;
            this.Resource = Resource;

            this.Authority = Authority;
            this.Lifetime = Lifetime;

            this.Owner = Owner;
            this.Scene = Scene;
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
        /// Authority is distributed to connected clients equally
        /// </summary>
        Distributable,

        /// <summary>
        /// Authority is handled explicitly by the master client
        /// </summary>
        Authoritative,

        /// <summary>
        /// Authority is always handled by the spawning client
        /// </summary>
        Explicit,

        /// <summary>
        /// Authority is handled by the spawning client with the ability to transfer instantly
        /// </summary>
        Transferable,

        /// <summary>
        /// Authority is handled by the spawning client, with the ability to request ownership from the current owner
        /// </summary>
        Requestable,
    }

    public enum NetworkEntityLifetimeMode : byte
    {
        /// <summary>
        /// Entitiy will live with it's owning client, and despawn when the owner disconnects
        /// </summary>
        Owner,

        /// <summary>
        /// Entity will live with the scene it was spawned in and despawn when that scene is unloaded
        /// </summary>
        Scene,

        /// <summary>
        /// Entity will live with no restrictions, will only despawn if manually requested
        /// </summary>
        Persistent,
    }
}
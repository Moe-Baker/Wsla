using System;
using System.Net;

using Wsla.Serialization;

namespace Wsla
{
    public enum ServerRegion : byte
    {
        Asia = 1,

        EU = 2,

        USA = 3,
    }

    public struct RelayServerRegistrationInfo : IEquatable<RelayServerRegistrationInfo>, IAutoNetworkSerialization
    {
        public ServerRegion Region;
        public IPAddress Address;

        public override string ToString() => $"[{Address}-{Region}]";

        public override bool Equals(object obj)
        {
            if (obj is RelayServerRegistrationInfo other)
                return Equals(other);

            return false;
        }
        public bool Equals(RelayServerRegistrationInfo other) => (Region == other.Region) && (Address.Equals(other.Address));

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref Region);
            context.Select(ref Address);
        }

        public override int GetHashCode() => (Region, Address).GetHashCode();

        public static bool operator ==(RelayServerRegistrationInfo left, RelayServerRegistrationInfo right) => left.Equals(right);
        public static bool operator !=(RelayServerRegistrationInfo left, RelayServerRegistrationInfo right) => !left.Equals(right);

        public RelayServerRegistrationInfo(ServerRegion Region, IPAddress Address)
        {
            this.Region = Region;
            this.Address = Address;
        }
    }
}
using System;
using System.Net;

using Wsla.Serialization;

namespace Wsla
{
    public struct RelayServerInfo : IEquatable<RelayServerInfo>, IAutoNetworkSerialization
    {
        public ServerRegion Region;
        public IPAddress Address;

        public override string ToString() => $"[{Address}-{Region}]";

        public override bool Equals(object obj)
        {
            if (obj is RelayServerInfo other)
                return Equals(other);

            return false;
        }
        public bool Equals(RelayServerInfo other) => (Region == other.Region) && (Address.Equals(other.Address));

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref Region);
            context.Select(ref Address);
        }

        public override int GetHashCode() => (Region, Address).GetHashCode();

        public static bool operator ==(RelayServerInfo left, RelayServerInfo right) => left.Equals(right);
        public static bool operator !=(RelayServerInfo left, RelayServerInfo right) => !left.Equals(right);

        public RelayServerInfo(ServerRegion Region, IPAddress Address)
        {
            this.Region = Region;
            this.Address = Address;
        }
    }
}
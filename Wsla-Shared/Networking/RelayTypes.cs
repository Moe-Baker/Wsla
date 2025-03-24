using System;
using System.Net;

using Wsla.Serialization;

namespace Wsla
{
    public struct RelayServerInfo : IEquatable<RelayServerInfo>, IAutoNetworkSerialization
    {
        public ServerRegion Region;
        public int ID;
        public IPAddress Address;

        public override string ToString() => $"[{Region}-{ID}]";

        public override bool Equals(object obj)
        {
            if (obj is RelayServerInfo other)
                return Equals(other);

            return false;
        }
        public bool Equals(RelayServerInfo other) => (Region == other.Region) && (ID == other.ID);

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref Region);
            context.Select(ref ID);
            context.Select(ref Address);
        }

        public override int GetHashCode() => (Region, ID).GetHashCode();

        public static bool operator ==(RelayServerInfo left, RelayServerInfo right) => left.Equals(right);
        public static bool operator !=(RelayServerInfo left, RelayServerInfo right) => !left.Equals(right);

        public RelayServerInfo(ServerRegion Region, int ID, IPAddress Address)
        {
            this.Region = Region;
            this.ID = ID;
            this.Address = Address;
        }
    }
}
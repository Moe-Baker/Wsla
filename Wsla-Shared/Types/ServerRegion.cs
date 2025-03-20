using System;
using System.Net;
using System.Text.Json.Serialization;

namespace Wsla
{
    public enum ServerRegion : byte
    {
        Asia = 1,

        EU = 2,

        USA = 3,
    }

    public struct RelayServerInfo : IEquatable<RelayServerInfo>
    {
        [JsonInclude]
        public ServerRegion Region;

        [JsonInclude]
        public int ID;

        [JsonInclude]
        public IPAddress Address;

        public override string ToString() => $"[{Region}-{ID}]";

        public override bool Equals(object obj)
        {
            if (obj is RelayServerInfo other)
                return Equals(other);

            return false;
        }
        public bool Equals(RelayServerInfo other) => (Region == other.Region) && (ID == other.ID);

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
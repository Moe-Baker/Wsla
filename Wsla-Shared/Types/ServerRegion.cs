using System;
using System.Net;
using System.Text.Json.Serialization;

using Wsla.Serialization;

namespace Wsla
{
    public enum ServerRegion : byte
    {
        Asia = 1,

        EU = 2,

        USA = 3,
    }

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

    public struct RoomConnectionInfo : IEquatable<RoomConnectionInfo>
    {
        [JsonInclude]
        public IPAddress Address;
        [JsonInclude]
        public ushort Port;

        public override string ToString() => $"[{Address}:{Port}]";

        public override bool Equals(object obj)
        {
            if (obj is RoomConnectionInfo other)
                return Equals(other);

            return false;
        }
        public bool Equals(RoomConnectionInfo other) => (Port.Equals(other.Port)) && (Address.Equals(other.Address));

        public override int GetHashCode() => HashCode.Combine(Address, Port);

        public static bool operator ==(RoomConnectionInfo left, RoomConnectionInfo right) => left.Equals(right);
        public static bool operator !=(RoomConnectionInfo left, RoomConnectionInfo right) => !left.Equals(right);

        public RoomConnectionInfo(IPAddress Address, ushort Port)
        {
            this.Address = Address;
            this.Port = Port;
        }
    }

    public struct RoomListEntryInfo
    {
        [JsonInclude]
        public string Name;

        [JsonInclude]
        public RoomConnectionInfo ConnectionInfo;

        public override string ToString() => $"[Room: {Name}, Connection: {ConnectionInfo}]";

        public RoomListEntryInfo(string Name, RoomConnectionInfo ConnectionInfo)
        {
            this.Name = Name;
            this.ConnectionInfo = ConnectionInfo;
        }
    }
}
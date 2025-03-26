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

    public struct RoomConnectionInfo : IEquatable<RoomConnectionInfo>
    {
        public IPAddress Address;
        public ushort Port;

        public string GetCode() => IPEndpointTextEncoder.Encode(Address, Port);

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

        public static bool TryParseCode(ReadOnlySpan<char> characters, out RoomConnectionInfo info)
        {
            if (IPEndpointTextEncoder.TryDecode(characters, out var address, out var port) is false)
            {
                info = default;
                return false;
            }

            info = new RoomConnectionInfo(address, port);
            return true;
        }
    }

    public struct RoomListEntryInfo
    {
        public FixedString<FS20> Name;

        public byte Capacity;
        public byte Occupancy;

        public RoomConnectionInfo ConnectionInfo;

        public override string ToString() => $"[Room: {Name} ({Occupancy}/{Capacity}) | Connection: {ConnectionInfo}]";

        public RoomListEntryInfo(FixedString<FS20> Name, byte Capacity, byte Occupancy, RoomConnectionInfo ConnectionInfo)
        {
            this.Name = Name;
            this.Capacity = Capacity;
            this.Occupancy = Occupancy;
            this.ConnectionInfo = ConnectionInfo;
        }
    }

    public struct RoomStateInfo : IAutoNetworkSerialization
    {
        public FixedString<FS20> Name;

        public byte Capacity;
        public byte Occupancy;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref Name);

            context.Select(ref Capacity);
            context.Select(ref Occupancy);
        }

        public RoomStateInfo(FixedString<FS20> Name, byte Capacity, byte Occupancy)
        {
            this.Name = Name;

            this.Capacity = Capacity;
            this.Occupancy = Occupancy;
        }
    }

    public struct RoomMatchmakerEntryData : IAutoNetworkSerialization
    {
        public Guid ID;
        public ushort Port;

        public RoomStateInfo State;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref ID);
            context.Select(ref Port);

            context.Select(ref State);
        }

        public RoomMatchmakerEntryData(Guid ID, ushort Port, RoomStateInfo State)
        {
            this.ID = ID;
            this.Port = Port;

            this.State = State;
        }
    }

    public struct CreateRoomParameters : IAutoNetworkSerialization
    {
        public FixedString<FS20> Name;
        public byte Capacity;
        public NetworkSceneID Scene;
        public FixedString<FS20> Password;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref Name);
            context.Select(ref Capacity);
            context.Select(ref Scene);
            context.Select(ref Password);
        }

        public CreateRoomParameters(FixedString<FS20> Name, byte Capacity, NetworkSceneID Scene, FixedString<FS20> Password)
        {
            this.Name = Name;
            this.Capacity = Capacity;
            this.Scene = Scene;
            this.Password = Password;
        }
    }

    public struct UpdateRoomParameters : IAutoNetworkSerialization
    {
        public byte? Occupancy;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref Occupancy);
        }

        public UpdateRoomParameters(byte? Occupancy)
        {
            this.Occupancy = Occupancy;
        }

        public static UpdateRoomParameters Merge(UpdateRoomParameters previous, UpdateRoomParameters current)
        {
            var occupancy = Merge(previous.Occupancy, current.Occupancy);

            return new UpdateRoomParameters(occupancy);
        }

        static T? Merge<T>(T? previous, T? current)
            where T : struct
        {
            if (current.HasValue)
                return current;

            if (previous.HasValue)
                return previous;

            return null;
        }
    }
}
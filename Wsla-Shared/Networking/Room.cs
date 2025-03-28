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
        public RoomPrivacy Privacy;

        public RoomStateInfo State;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref ID);
            context.Select(ref Port);
            context.Select(ref Privacy);

            context.Select(ref State);
        }

        public RoomMatchmakerEntryData(Guid ID, ushort Port, RoomPrivacy Privacy, RoomStateInfo State)
        {
            this.ID = ID;
            this.Port = Port;
            this.Privacy = Privacy;

            this.State = State;
        }
    }

    public struct CreateRoomParameters : IAutoNetworkSerialization
    {
        public FixedString<FS20> Name;
        public byte Capacity;
        public NetworkSceneID Scene;
        public FixedString<FS20> Password;
        public RoomPrivacy Privacy;
        public RoomLockPolicy Lock;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref Name);
            context.Select(ref Capacity);
            context.Select(ref Scene);
            context.Select(ref Password);
            context.Select(ref Privacy);
            context.Select(ref Lock);
        }

        public CreateRoomParameters(FixedString<FS20> Name, byte Capacity, NetworkSceneID Scene, FixedString<FS20> Password, RoomPrivacy Privacy, RoomLockPolicy Lock)
        {
            this.Name = Name;
            this.Capacity = Capacity;
            this.Scene = Scene;
            this.Password = Password;
            this.Privacy = Privacy;
            this.Lock = Lock;
        }
    }

    public struct UpdateRoomParameters : IAutoNetworkSerialization
    {
        public byte? Occupancy;
        public byte Joins;

        public bool Lock;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref Occupancy);
            context.Select(ref Joins);

            context.Select(ref Lock);
        }

        public static UpdateRoomParameters Merge(UpdateRoomParameters previous, UpdateRoomParameters current)
        {
            return new UpdateRoomParameters()
            {
                Lock = previous.Lock || current.Lock,
                Occupancy = MergeNullable(previous.Occupancy, current.Occupancy),
                Joins = (byte)(previous.Joins + current.Joins),
            };

            static T? MergeNullable<T>(T? previous, T? current)
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

    public enum RoomPrivacy : byte
    {
        /// <summary>
        /// Room will be available for anyone to join at all times
        /// </summary>
        Public = 0,

        /// <summary>
        /// Room will only be join-able by code (IP + Port)
        /// </summary>
        Private = 2,
    }

    /// <summary>
    /// When a room is locked, no more people can join it at all
    /// </summary>
    public enum RoomLockPolicy
    {
        /// <summary>
        /// Nothing special will happen
        /// </summary>
        None,

        /// <summary>
        /// Room will lock after it fills
        /// </summary>
        AfterFill,
    }
}
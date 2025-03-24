using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json.Serialization;

using Wsla.Serialization;

namespace Wsla
{
    public struct CreateRoomParameters : IAutoNetworkSerialization
    {
        [JsonInclude]
        public string Name;

        [JsonInclude]
        public byte Capacity;

        [JsonInclude]
        public string Password;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref Name);
            context.Select(ref Capacity);
            context.Select(ref Password);
        }

        public CreateRoomParameters(string Name, byte Capacity, string Password)
        {
            this.Name = Name;
            this.Capacity = Capacity;
            this.Password = Password;
        }
    }

    public struct CreateRoomRequest
    {
        [JsonInclude]
        public ServerRegion Region;

        [JsonInclude]
        public CreateRoomParameters Parameters;

        public CreateRoomRequest(ServerRegion Region, CreateRoomParameters Parameters)
        {
            this.Parameters = Parameters;
            this.Region = Region;
        }
    }
    public struct CreateRoomResponse
    {
        [JsonInclude]
        public IPAddress Address;

        [JsonInclude]
        public ushort Port;

        public CreateRoomResponse(IPAddress Address, ushort Port)
        {
            this.Address = Address;
            this.Port = Port;
        }
    }

    public struct CreateRoomCommand : IAutoNetworkSerialization
    {
        public Guid ID;
        public CreateRoomParameters Parameters;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref ID);
            context.Select(ref Parameters);
        }

        public CreateRoomCommand(Guid ID, CreateRoomParameters Parameters)
        {
            this.ID = ID;
            this.Parameters = Parameters;
        }
    }
    public struct CreateRoomConfirmation : IAutoNetworkSerialization
    {
        public Guid ID;
        public ushort Port;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref ID);
            context.Select(ref Port);
        }

        public CreateRoomConfirmation(Guid ID, ushort Port)
        {
            this.ID = ID;
            this.Port = Port;
        }
    }

    public struct RegisterRelayRequest : IAutoNetworkSerialization
    {
        public RelayServerInfo Info;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref Info);
        }

        public RegisterRelayRequest(RelayServerInfo Info)
        {
            this.Info = Info;
        }
    }

    public struct ListRegionsResponse
    {
        [JsonInclude]
        public List<ServerRegion> Regions;

        public ListRegionsResponse(List<ServerRegion> regions)
        {
            this.Regions = regions;
        }
    }

    public struct RemoveRoomRequest : IAutoNetworkSerialization
    {
        public Guid RoomID;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref RoomID);
        }

        public RemoveRoomRequest(Guid RoomID)
        {
            this.RoomID = RoomID;
        }
    }

    public struct ListRoomsRequest
    {
        public ServerRegion Region;

        public ListRoomsRequest(ServerRegion Region)
        {
            this.Region = Region;
        }
    }

    public struct UpdateRoomParameters : IAutoNetworkSerialization
    {
        public FixedString40? Name;

        public byte? Occupancy;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref Name);
            context.Select(ref Occupancy);
        }

        public UpdateRoomParameters(FixedString40? Name, byte? Occupancy)
        {
            this.Name = Name;
            this.Occupancy = Occupancy;
        }

        public static UpdateRoomParameters Merge(UpdateRoomParameters previous, UpdateRoomParameters current)
        {
            var name = Merge(previous.Name, current.Name);
            var occupancy = Merge(previous.Occupancy, current.Occupancy);

            return new UpdateRoomParameters(name, occupancy);
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

    public struct UpdateRoomsRequest : IAutoNetworkSerialization
    {
        public List<UpdateRoomRequest> Requests;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref Requests);
        }

        public UpdateRoomsRequest(List<UpdateRoomRequest> Requests)
        {
            this.Requests = Requests;
        }
    }
    public struct UpdateRoomRequest : IAutoNetworkSerialization
    {
        public Guid ID;

        public UpdateRoomParameters Parameters;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref ID);
            context.Select(ref Parameters);
        }

        public UpdateRoomRequest(Guid ID, UpdateRoomParameters Parameters)
        {
            this.ID = ID;
            this.Parameters = Parameters;
        }
    }
}
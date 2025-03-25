using System;
using System.Collections.Generic;

using Wsla.Serialization;

namespace Wsla
{
    public struct CreateRoomRequest
    {
        public ServerRegion? Region;

        public CreateRoomParameters Parameters;

        public CreateRoomRequest(ServerRegion? Region, CreateRoomParameters Parameters)
        {
            this.Parameters = Parameters;
            this.Region = Region;
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

        public List<RoomMatchmakerEntryData> Rooms;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref Info);
            context.Select(ref Rooms);
        }

        public RegisterRelayRequest(RelayServerInfo Info, List<RoomMatchmakerEntryData> Rooms)
        {
            this.Info = Info;
            this.Rooms = Rooms;
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

    public struct FindRoomRequest
    {
        public ServerRegion? Region;

        public CreateRoomParameters? CreateRoom;

        public FindRoomRequest(ServerRegion? Region, CreateRoomParameters? CreateRoom)
        {
            this.Region = Region;
            this.CreateRoom = CreateRoom;
        }
    }
}
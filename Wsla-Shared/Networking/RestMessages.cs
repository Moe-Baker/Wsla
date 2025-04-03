using System;
using System.Collections.Generic;

using Wsla.Serialization;

namespace Wsla
{
    public struct CreateRoomRequest : IAutoNetworkSerialization
    {
        public FixedString<FS20> Application;

        public SparseArray<ServerRegion> Regions;

        public CreateRoomParameters Parameters;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref Application);
            context.Select(ref Regions);
            context.Select(ref Parameters);
        }

        public CreateRoomRequest(FixedString<FS20> ApplicationName, SparseArray<ServerRegion> Regions, CreateRoomParameters Parameters)
        {
            this.Application = ApplicationName;
            this.Parameters = Parameters;
            this.Regions = Regions;
        }
    }

    public struct CreateRoomCommand : IAutoNetworkSerialization
    {
        public ApplicationID ApplicationID;
        public Guid RoomID;
        public CreateRoomParameters Parameters;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref ApplicationID);
            context.Select(ref RoomID);
            context.Select(ref Parameters);
        }

        public CreateRoomCommand(ApplicationID ApplicationID, Guid RoomID, CreateRoomParameters Parameters)
        {
            this.ApplicationID = ApplicationID;
            this.RoomID = RoomID;
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
        public FixedString<FS20> Application;

        public SparseArray<ServerRegion> Regions;

        public ListRoomsRequest(FixedString<FS20> ApplicationName, SparseArray<ServerRegion> Regions)
        {
            this.Application = ApplicationName;
            this.Regions = Regions;
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

    public struct FindRoomRequest : IAutoNetworkSerialization
    {
        public FixedString<FS20> Application;

        public SparseArray<ServerRegion> Regions;

        public CreateRoomParameters? CreateRoom;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref Application);
            context.Select(ref Regions);
            context.Select(ref CreateRoom);
        }

        public FindRoomRequest(FixedString<FS20> Application, SparseArray<ServerRegion> Regions, CreateRoomParameters? CreateRoom)
        {
            this.Application = Application;
            this.Regions = Regions;
            this.CreateRoom = CreateRoom;
        }
    }
}
using System;
using System.Collections.Generic;

using Wsla.Serialization;

namespace Wsla
{
    public struct CreateRoomRequest : IAutoNetworkSerialization
    {
        public NetworkVersion ApiVersion;
        public NetworkVersion GameVersion;

        public FixedString<FS20> Application;

        public SparseArray<ServerRegion> Regions;

        public CreateRoomParameters Parameters;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref ApiVersion);
            context.Select(ref GameVersion);

            context.Select(ref Application);
            context.Select(ref Regions);
            context.Select(ref Parameters);
        }

        public CreateRoomRequest(NetworkVersion GameVersion, FixedString<FS20> ApplicationName, SparseArray<ServerRegion> Regions, CreateRoomParameters Parameters)
        {
            this.ApiVersion = Constants.ApiVersion;
            this.GameVersion = GameVersion;

            this.Application = ApplicationName;
            this.Parameters = Parameters;
            this.Regions = Regions;
        }
    }

    public struct CreateRoomCommand : IAutoNetworkSerialization
    {
        public NetworkVersion GameVersion;

        public ApplicationID ApplicationID;
        public Guid RoomID;
        public CreateRoomParameters Parameters;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref GameVersion);

            context.Select(ref ApplicationID);
            context.Select(ref RoomID);
            context.Select(ref Parameters);
        }

        public CreateRoomCommand(NetworkVersion GameVersion, ApplicationID ApplicationID, Guid RoomID, CreateRoomParameters Parameters)
        {
            this.GameVersion = GameVersion;

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
        public RelayServerRegistrationInfo Info;

        public List<RoomMatchmakerEntryData> Rooms;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref Info);
            context.Select(ref Rooms);
        }

        public RegisterRelayRequest(RelayServerRegistrationInfo Info, List<RoomMatchmakerEntryData> Rooms)
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

    public struct QueryRoomsRequest : IAutoNetworkSerialization
    {
        public NetworkVersion GameVersion;
        public FixedString<FS20> Application;
        public SparseArray<ServerRegion> Regions;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref GameVersion);
            context.Select(ref Application);
            context.Select(ref Regions);
        }

        public QueryRoomsRequest(NetworkVersion GameVersion, FixedString<FS20> ApplicationName, SparseArray<ServerRegion> Regions)
        {
            this.GameVersion = GameVersion;
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
        public NetworkVersion ApiVersion;
        public NetworkVersion GameVersion;

        public FixedString<FS20> Application;

        public SparseArray<ServerRegion> Regions;

        public CreateRoomParameters? CreateRoom;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref ApiVersion);
            context.Select(ref GameVersion);

            context.Select(ref Application);
            context.Select(ref Regions);
            context.Select(ref CreateRoom);
        }

        public FindRoomRequest(NetworkVersion GameVersion, FixedString<FS20> Application, SparseArray<ServerRegion> Regions, CreateRoomParameters? CreateRoom)
        {
            this.ApiVersion = Constants.ApiVersion;
            this.GameVersion = GameVersion;

            this.Application = Application;
            this.Regions = Regions;
            this.CreateRoom = CreateRoom;
        }
    }
}
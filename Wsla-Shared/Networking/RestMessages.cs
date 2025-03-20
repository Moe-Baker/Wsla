using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json.Serialization;

namespace Wsla
{
    public struct CreateRoomRequest
    {
        [JsonInclude]
        public ServerRegion Region;

        [JsonInclude]
        public CreateRoomCommand Command;

        public CreateRoomRequest(ServerRegion Region, CreateRoomCommand Command)
        {
            this.Command = Command;
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

    public struct CreateRoomCommand
    {
        [JsonInclude]
        public string Name;

        [JsonInclude]
        public byte Capacity;

        [JsonInclude]
        public string Password;

        public CreateRoomCommand(string Name, byte Capacity, string Password)
        {
            this.Name = Name;
            this.Capacity = Capacity;
            this.Password = Password;
        }
    }
    public struct CreateRoomConfirmation
    {
        [JsonInclude]
        public Guid ID;

        [JsonInclude]
        public ushort Port;

        public CreateRoomConfirmation(Guid ID, ushort Port)
        {
            this.ID = ID;
            this.Port = Port;
        }
    }

    public struct RegisterRelayRequest
    {
        [JsonInclude]
        public RelayServerInfo Info;

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
}
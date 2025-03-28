using LiteNetLib;

using System;

using Wsla.Serialization;

namespace Wsla
{
    public struct WslaError : IAutoNetworkSerialization
    {
        public WslaErrorCode Code;
        public string Description;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref Code);
            context.Select(ref Description);
        }

        public override string ToString()
        {
            if (string.IsNullOrEmpty(Description))
                return Code.ToString();
            else
                return $"{Code} | {Description}";
        }

        public Exception ToException() => new Exception($"Wsla Error: {this}");

        public WslaError(WslaErrorCode code, string description)
        {
            this.Code = code;
            this.Description = description;
        }

        public static WslaError From(WslaErrorCode code) => new(code, string.Empty);

        public static implicit operator WslaError(WslaErrorCode code) => From(code);

        public static WslaError From(DisconnectInfo info)
        {
            switch (info.Reason)
            {
                case DisconnectReason.ConnectionRejected:
                case DisconnectReason.RemoteConnectionClose:
                    return NetworkSerializer.ReadValue<WslaError>(info.AdditionalData);
            }

            return From(WslaErrorCode.TransportFailure);
        }

        public static WslaError From(Exception exception) => From(WslaErrorCode.Exception, exception);
        public static WslaError From(WslaErrorCode code, Exception exception) => new WslaError(code, exception.ToString());

        public static WslaError From(RestResponse response) => new WslaError(WslaErrorCode.HttpRequestFailed, response.ToString());
    }

    public enum WslaErrorCode : byte
    {
        TransportFailure = 1,
        RequestDeserializationFailure = 2,
        ClientIDGeneratorOverloaded = 3,
        EntityIDGeneratorOverloaded = 4,
        SpawnTokenContractBroken = 5,
        SyncedPrefabNotFound = 6,
        SyncedPrefabWithoutNetworkEntity = 7,
        NoAuthority = 8,
        NoEntityFoundInScene = 9,
        NoRegion = 10,
        CapacityFull = 11,
        InvalidPassword = 12,
        OperationCanceled = 13,
        SocketClosed = 14,
        HttpRequestFailed = 15,
        Exception = 16,
        RoomLocked = 17,
        NoRoomFound = 18,
    }
}
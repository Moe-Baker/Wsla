using LiteNetLib;

using MemoryPack;

namespace Wsla.Shared.Global
{
    [MemoryPackable]
    public partial struct WslaError
    {
        public WslaErrorCode Code { get; }
        public string Description { get; }

        public override string ToString()
        {
            if (string.IsNullOrEmpty(Description))
                return Code.ToString();
            else
                return $"{Code} | {Description}";
        }

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
                    return MemoryPackSerializer.Deserialize<WslaError>(info.AdditionalData.GetRemainingBytesSpan());
            }

            return From(WslaErrorCode.TransportFailure);
        }
    }

    public enum WslaErrorCode : ushort
    {
        TransportFailure = 1,
        RequestDeserializationFailure = 2,
        ClientIDGeneratorOverloaded = 3,
        EntityIDGeneratorOverloaded = 4,
        SpawnTokenContractBroken = 5,
        SyncedPrefabNotFound = 6,
        SyncedPrefabWithoutNetworkEntity = 7,
    }
}
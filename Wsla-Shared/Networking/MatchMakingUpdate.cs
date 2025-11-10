using Wsla.Serialization;

namespace Wsla
{
    public struct StartMatchMakingRequest : IAutoNetworkSerialization
    {
        public NetworkVersion APIVersion;
        public NetworkVersion GameVersion;

        public FixedString<FS20> Application;
        public FixedString<FS20> Pool;

        public SparseArray<ServerRegion> Regions;
        public SparseArray<NetworkSceneID> Scenes;
        public MatchMakingParameters Parameters;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref APIVersion);
            context.Select(ref GameVersion);

            context.Select(ref Application);
            context.Select(ref Pool);

            context.Select(ref Regions);
            context.Select(ref Scenes);
            context.Select(ref Parameters);
        }

        public StartMatchMakingRequest(NetworkVersion GameVersion, FixedString<FS20> Application, FixedString<FS20> Pool, SparseArray<ServerRegion> Regions, SparseArray<NetworkSceneID> Scenes, MatchMakingParameters Parameters)
        {
            this.APIVersion = Constants.ApiVersion;
            this.GameVersion = GameVersion;

            this.Application = Application;
            this.Pool = Pool;
            this.Regions = Regions;
            this.Scenes = Scenes;
            this.Parameters = Parameters;
        }
    }

    public struct MatchmakingSuccessResponse : IAutoNetworkSerialization
    {
        public RoomConnectionInfo Info;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref Info);
        }

        public MatchmakingSuccessResponse(RoomConnectionInfo Info)
        {
            this.Info = Info;
        }
    }
    public struct MatchmakingFailResponse : IAutoNetworkSerialization
    {
        public WslaError Error;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref Error);
        }

        public MatchmakingFailResponse(WslaErrorCode code) : this(WslaError.From(code)) { }
        public MatchmakingFailResponse(WslaError error)
        {
            this.Error = error;
        }
    }
}
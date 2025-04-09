using Wsla.Serialization;

namespace Wsla
{
    public struct StartMatchMakingRequest : IAutoNetworkSerialization
    {
        public FixedString<FS20> Application;
        public FixedString<FS20> Pool;
        public SparseArray<ServerRegion> Regions;
        public NetworkSceneID Scene;
        public MatchMakingParameters Parameters;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref Application);
            context.Select(ref Pool);
            context.Select(ref Regions);
            context.Select(ref Scene);
            context.Select(ref Parameters);
        }

        public StartMatchMakingRequest(FixedString<FS20> Application, FixedString<FS20> Pool, SparseArray<ServerRegion> Regions, NetworkSceneID Scene, MatchMakingParameters Parameters)
        {
            this.Application = Application;
            this.Pool = Pool;
            this.Regions = Regions;
            this.Scene = Scene;
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
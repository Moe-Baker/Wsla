using Wsla.Serialization;

namespace Wsla
{
    public struct StartMatchMakingRequest : IAutoNetworkSerialization
    {
        public void Select(ref AutoSerializationContext context)
        {

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
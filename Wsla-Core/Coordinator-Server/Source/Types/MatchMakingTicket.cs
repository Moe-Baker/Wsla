using System;

namespace Wsla.Server
{
    public class MatchMakingTicket
    {
        public readonly MessagingPeer Peer;
        public readonly MatchMakingPool Pool;

        StartMatchMakingRequest Request;
        public ref MatchMakingParameters Parameters => ref Request.Parameters;
        public ref SparseArray<ServerRegion> Regions => ref Request.Regions;
        public ref SparseArray<NetworkSceneID> Scenes => ref Request.Scenes;

        readonly DateTime Timestamp;
        public TimeSpan CalculateAge() => (TimeNow - Timestamp).Duration();
        public bool IsExpired() => (CalculateAge() > Pool.Duration);

        public void Accept(RoomConnectionInfo info) => CoordinatorServer.Matchmaking.Queue.Accept(Peer, info);

        public void Fail() => CoordinatorServer.Matchmaking.Queue.Fail(Peer, WslaErrorCode.NoRoomFound);
        public void Fail(WslaErrorCode code) => CoordinatorServer.Matchmaking.Queue.Fail(Peer, code);

        public void Unregister() => Pool.Unregister(this);

        public MatchMakingTicket(MessagingPeer Peer, MatchMakingPool Pool, StartMatchMakingRequest Request)
        {
            this.Peer = Peer;
            this.Pool = Pool;
            this.Request = Request;

            Timestamp = TimeNow;
        }

        static DateTime TimeNow => DateTime.UtcNow;
    }
}
using System;

namespace Wsla
{
    public enum MatchMakingProgress : byte
    {
        Searching = 0,
        Found = 1,
        NotFound = 2,
    }

    public struct MatchMakingRequest
    {
        public Guid ID;

        public MatchMakingRequest(Guid ID)
        {
            this.ID = ID;
        }
    }

    public struct MatchMakingUpdate
    {
        public MatchMakingProgress Progress;
        public RoomConnectionInfo? Info;

        public MatchMakingUpdate(MatchMakingProgress Progress, RoomConnectionInfo? Info)
        {
            this.Progress = Progress;
            this.Info = Info;
        }

        public static MatchMakingUpdate Searching => new MatchMakingUpdate(MatchMakingProgress.Searching, null);
        public static MatchMakingUpdate NotFound => new MatchMakingUpdate(MatchMakingProgress.NotFound, null);
        public static MatchMakingUpdate Found(RoomConnectionInfo info) => new MatchMakingUpdate(MatchMakingProgress.Found, info);
    }
}
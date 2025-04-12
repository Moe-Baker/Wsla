using System;

namespace Wsla
{
    public class Constants
    {
        public static NetworkVersion ApiVersion { get; } = new NetworkVersion(0, 0, 1);

        public const ushort RelayRealtimePort = 4527;

        public const ushort CoordinatorHttpPort = 4895;
        public const ushort CoordinatorMessagingPort = 4724;

        public static TimeSpan Timeout = TimeSpan.FromSeconds(10);

        public const byte ChannelCount = 32;

        public static class RestRoutes
        {
            public const string RegisterRelay = "register-relay";
            public const string ListRegions = "list-regions";

            public const string ListRooms = "list-rooms";

            public const string FindRoom = "find-room";

            public const string CreateRoom = "create-room";
            public const string RemoveRoom = "remove-room";

            public const string RequestMatch = "request-match";
            public const string UpdateMatch = "update-match";
            public const string CancelMatch = "cancel-match";
        }

        public const string WslaContentType = "application/x-wsla";
    }
}
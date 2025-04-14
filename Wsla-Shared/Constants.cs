using System;

namespace Wsla
{
    public class Constants
    {
        public static NetworkVersion ApiVersion { get; } = new(0, 0, 1);

        public const ushort RelayRealtimePort = 4527;

        public const ushort CoordinatorHttpPort = 4895;
        public const ushort CoordinatorMessagingPort = 4724;

        public static TimeSpan Timeout = TimeSpan.FromSeconds(10);

        public const byte ChannelCount = 32;

        public static class RestRoutes
        {
            public const string Root = "api/";

            public const string RegisterRelay = Root + "register-relay";
            public const string ListRegions = Root + "list-regions";

            public const string ListRooms = Root + "list-rooms";

            public const string FindRoom = Root + "find-room";

            public const string CreateRoom = Root + "create-room";
            public const string RemoveRoom = Root + "remove-room";

            public const string RequestMatch = Root + "request-match";
            public const string UpdateMatch = Root + "update-match";
            public const string CancelMatch = Root + "cancel-match";
        }

        public const string WslaContentType = "application/x-wsla";
    }
}
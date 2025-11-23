using System;

namespace Wsla
{
    public class Constants
    {
        public static NetworkVersion ApiVersion { get; } = new(0, 0, 1);

        public const ushort RelayRealtimePort = 4527;

        public const ushort CoordinatorHttpPort = 80;
        public const ushort CoordinatorMessagingPort = 4724;

        public static TimeSpan Timeout = TimeSpan.FromSeconds(10);

        public const byte ChannelCount = 32;

        public static class RestRoutes
        {
            public const string Root = "api/";

            public static class Service
            {
                public const string Root = RestRoutes.Root + "service/";

                public const string ListRegions = Root + "list-regions";

                public const string QueryRooms = Root + "query-rooms";
                public const string FindRoom = Root + "find-room";
                public const string CreateRoom = Root + "create-room";
                public const string RemoveRoom = Root + "remove-room";
            }

            public static class Administration
            {
                public const string Root = RestRoutes.Root + "admin/";

                public const string ListRelays = Root + "list-relays";
                public const string ListPlugins = Root + "list-plugins";
                public const string ListRooms = Root + "list-rooms";
            }
        }

        public const string WslaContentType = "application/x-wsla";
    }
}
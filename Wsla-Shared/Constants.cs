using System;

namespace Wsla
{
    public class Constants
    {
        public static NetworkVersion ApiVersion { get; } = new NetworkVersion(0, 0, 1);

        public const ushort RelayMessagingPort = 4785;
        public const ushort RelayRealtimePort = 4527;
        public const ushort CoordinatorMessagingPort = 4724;

        public static TimeSpan Timeout = TimeSpan.FromSeconds(10);

        public const byte ChannelCount = 32;
    }
}
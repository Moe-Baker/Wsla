using System;

namespace Wsla
{
    public class Constants
    {
        public static NetworkVersion ApiVersion { get; } = new NetworkVersion(0, 0, 1);

        public const ushort RelayManagementPort = 4785;
        public const ushort CoordinatorServicePort = 4724;

        public static TimeSpan Timeout = TimeSpan.FromSeconds(10);

        public const byte ChannelCount = 32;
    }
}
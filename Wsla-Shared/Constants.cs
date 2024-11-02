using System;

namespace Wsla
{
    public class Constants
    {
        public static Version ApiVersion { get; } = new Version(0, 0, 1);

        public const ushort RelayManagementPort = 4785;
        public const ushort CoordinatorServicePort = 4724;

        public static TimeSpan Timeout = TimeSpan.FromSeconds(10);
    }
}
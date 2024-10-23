using Wsla.Serialization;

namespace Wsla.Shared.Global
{
    public class Constants
    {
        public static Version ApiVersion { get; } = new Version(0, 0, 1);

        public const ushort RelayManagementPort = 4785;
        public const ushort CoordinatorServicePort = 4724;

        void Call()
        {
            Wsla.Serialization.NetworkSerializer.Clone(new A());
        }
    }

    [NetworkBlittable]
    struct A
    {

    }
}
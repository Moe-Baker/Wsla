using Microsoft.CodeAnalysis;

namespace Wsla.Generator
{
    [Generator]
    public class NetworkSyncMembersGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {

        }

        public class CodeConstants : GlobalNetworkGenerator.Constants
        {
            public static readonly string Namespace = $"{Name}.Serialization";

            public static readonly string NetworkSerializationMarkerAttribute = $"{Namespace}.{nameof(NetworkSerializationMarkerAttribute)}";

            public static readonly string ArrayNetworkSerializationResolver = $"{Namespace}.{nameof(ArrayNetworkSerializationResolver)}";
            public static readonly string ArraySegmentNetworkSerializationResolver = $"{Namespace}.{nameof(ArraySegmentNetworkSerializationResolver)}";
            public static readonly string ListNetworkSerializationResolver = $"{Namespace}.{nameof(ListNetworkSerializationResolver)}";

            public static readonly string ManualNetworkSerializationResolver = $"{Namespace}.{nameof(ManualNetworkSerializationResolver)}";
            public static readonly string IAutoNetworkSerialization = $"{Namespace}.{nameof(IAutoNetworkSerialization)}";

            public static readonly string AutoNetworkSerializationResolver = $"{Namespace}.{nameof(AutoNetworkSerializationResolver)}";
            public static readonly string IManualNetworkSerialization = $"{Namespace}.{nameof(IManualNetworkSerialization)}";

            public static readonly string BlittableNetworkSerializationResolver = $"{Namespace}.{nameof(BlittableNetworkSerializationResolver)}";

            public static readonly string NetworkSerializationResolverRegisterationAttribute = $"{Namespace}.{nameof(NetworkSerializationResolverRegisterationAttribute)}";
        }
    }
}
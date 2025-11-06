using Wsla.Serialization;

namespace Wsla
{
    [NetworkBlittable]
    public struct BroadcastNetworkVariableRequest
    {
        public NetworkSyncMemberParameters Parameters;

        public BroadcastNetworkVariableRequest(NetworkSyncMemberParameters Parameters)
        {
            this.Parameters = Parameters;
        }
    }

    [NetworkBlittable]
    public struct BufferNetworkVariableRequest
    {
        public NetworkSyncMemberParameters Parameters;

        public BufferNetworkVariableRequest(NetworkSyncMemberParameters Parameters)
        {
            this.Parameters = Parameters;
        }
    }

    [NetworkBlittable]
    public struct NetworkVariableCommand
    {
        public NetworkClientID Sender;
        public NetworkSyncMemberParameters Parameters;

        public NetworkVariableCommand(NetworkClientID Sender, NetworkSyncMemberParameters Parameters)
        {
            this.Sender = Sender;
            this.Parameters = Parameters;
        }

        public static void WriteValue(INetworkStream input, INetworkStream output)
        {
            var source = input.PeekAvailableMemory();
            var destination = output.AllocateMemory(source.Length);
            source.CopyTo(destination);
        }
        public static BinarySource ReadValue(INetworkStream stream)
        {
            return BinarySource.From(stream);
        }
    }
}
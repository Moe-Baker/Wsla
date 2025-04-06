using Wsla.Serialization;

namespace Wsla
{
    [NetworkBlittable]
    public struct BroadcastNetworkRpcRequest
    {
        public RemoteBufferMode Buffer;
        public NetworkSyncMemberParameters Parameters;

        public BroadcastNetworkRpcRequest(RemoteBufferMode Buffer, NetworkSyncMemberParameters Parameters)
        {
            this.Buffer = Buffer;
            this.Parameters = Parameters;
        }
    }

    [NetworkBlittable]
    public struct BufferNetworkRpcRequest
    {
        public RemoteBufferMode Buffer;
        public NetworkSyncMemberParameters Parameters;

        public BufferNetworkRpcRequest(RemoteBufferMode Buffer, NetworkSyncMemberParameters Parameters)
        {
            this.Buffer = Buffer;
            this.Parameters = Parameters;
        }
    }

    [NetworkBlittable]
    public struct TargetNetworkRpcRequest
    {
        public NetworkClientID Target;
        public NetworkSyncMemberParameters Parameters;

        public TargetNetworkRpcRequest(NetworkClientID Target, NetworkSyncMemberParameters Parameters)
        {
            this.Target = Target;
            this.Parameters = Parameters;
        }
    }

    [NetworkBlittable]
    public struct NetworkRpcCommand
    {
        public NetworkClientID Sender;
        public NetworkSyncMemberParameters Parameters;

        public NetworkRpcCommand(NetworkClientID Sender, NetworkSyncMemberParameters Parameters)
        {
            this.Sender = Sender;
            this.Parameters = Parameters;
        }
    }
}
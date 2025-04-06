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
    }
}
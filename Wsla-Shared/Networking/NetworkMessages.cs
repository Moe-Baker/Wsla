using MemoryPack;

namespace Wsla.Shared.Global
{
    [MemoryPackable]
    public partial struct ClientConnectionRequest
    {
        public string Username { get; }

        public override string ToString() => $"(Username: {Username})";

        public ClientConnectionRequest(string username)
        {
            this.Username = username;
        }
    }
    [MemoryPackable]
    public partial struct ClientConnectionResponse
    {
        public NetworkClientID LocalID { get; }

        public override string ToString() => $"(ClientID: {LocalID})";

        public ClientConnectionResponse(NetworkClientID localID)
        {
            this.LocalID = localID;
        }
    }

    [MemoryPackable]
    public partial struct ClientConnectEvent
    {
        public NetworkClientData Data { get; }

        public ClientConnectEvent(NetworkClientData data)
        {
            this.Data = data;
        }
    }
    [MemoryPackable]
    public partial struct ClientDisconnectEvent
    {
        public NetworkClientID ID { get; }

        public ClientDisconnectEvent(NetworkClientID id)
        {
            this.ID = id;
        }
    }

    [MemoryPackable]
    public partial struct NetworkPingEvent
    {

    }

    [MemoryPackable]
    public partial struct NetworkPongEvent
    {

    }
}
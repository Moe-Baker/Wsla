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
        public NetworkClientID ID { get; }

        public byte Clients { get; }
        public byte SpawnTokens { get; }
        public ushort Entities { get; }

        public override string ToString() => $"(ClientID: {ID})";

        public ClientConnectionResponse(NetworkClientID id, byte clients, byte spawnTokens, ushort entities)
        {
            this.ID = id;

            this.Clients = clients;
            this.SpawnTokens = spawnTokens;
            this.Entities = entities;
        }
    }

    [MemoryPackable]
    public partial struct ClientConnectMessage { }
    [MemoryPackable]
    public partial struct ClientDisconnectMessage
    {
        public NetworkClientID ID { get; }

        public ClientDisconnectMessage(NetworkClientID id)
        {
            this.ID = id;
        }
    }

    [MemoryPackable]
    public partial struct NetworkPingRequest
    {

    }
    [MemoryPackable]
    public partial struct NetworkPongResponse
    {

    }

    [MemoryPackable]
    public partial struct SpawnEntityRequest
    {
        public NetworkEntityID SpawnToken { get; }
        public NetworkEntityResource Resource { get; }

        public SpawnEntityRequest(NetworkEntityID SpawnToken, NetworkEntityResource Resource)
        {
            this.SpawnToken = SpawnToken;
            this.Resource = Resource;
        }
    }

    [MemoryPackable]
    public partial struct SpawnEntityResponse
    {
        public NetworkEntityID SourceToken { get; }
        public NetworkEntityID ReplacementToken { get; }

        public SpawnEntityResponse(NetworkEntityID SourceToken, NetworkEntityID ReplacementToken)
        {
            this.SourceToken = SourceToken;
            this.ReplacementToken = ReplacementToken;
        }
    }

    [MemoryPackable]
    public partial struct SpawnEntityCommand { }

    [MemoryPackable]
    public partial struct DespawnEntityCommand
    {
        public NetworkEntityID ID { get; }

        public DespawnEntityCommand(NetworkEntityID ID)
        {
            this.ID = ID;
        }
    }
}
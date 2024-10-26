using System.Collections.Generic;

using Wsla.Serialization;

namespace Wsla
{
    public partial struct ClientConnectionRequest : IAutoNetworkSerialization
    {
        public string Username;

        public override string ToString() => $"(Username: {Username})";

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref Username);
        }

        public ClientConnectionRequest(string Username)
        {
            this.Username = Username;
        }
    }
    [NetworkBlittable]
    public partial struct ClientConnectionResponse
    {
        public NetworkClientID LocalID;
        public NetworkClientID MasterID;

        public byte Clients;
        public byte SpawnTokens;
        public byte Scenes;
        public ushort Entities;

        public override string ToString() => $"(ClientID: {LocalID})";

        public ClientConnectionResponse(NetworkClientID LocalID, NetworkClientID MasterID, byte Clients, byte SpawnTokens, byte Scenes, ushort Entities)
        {
            this.LocalID = LocalID;
            this.MasterID = MasterID;

            this.Clients = Clients;
            this.SpawnTokens = SpawnTokens;
            this.Scenes = Scenes;
            this.Entities = Entities;
        }
    }

    [NetworkBlittable]
    public partial struct ClientConnectMessage
    {

    }
    [NetworkBlittable]
    public partial struct ClientDisconnectMessage
    {
        public NetworkClientID ID;

        public ClientDisconnectMessage(NetworkClientID ID)
        {
            this.ID = ID;
        }
    }

    [NetworkBlittable]
    public partial struct SpawnEntityRequest
    {
        public NetworkEntityID SpawnToken;
        public NetworkEntityResource Resource;

        public NetworkEntityAuthorityMode Authority;
        public NetworkEntityLifetimeMode Lifetime;

        public NetworkSceneID Scene;

        public SpawnEntityRequest(NetworkEntityID SpawnToken, NetworkEntityResource Resource, NetworkEntityAuthorityMode Authority, NetworkEntityLifetimeMode Lifetime, NetworkSceneID Scene)
        {
            this.SpawnToken = SpawnToken;

            this.Resource = Resource;
            this.Scene = Scene;

            this.Authority = Authority;
            this.Lifetime = Lifetime;
        }
    }

    [NetworkBlittable]
    public partial struct SpawnEntityResponse
    {
        public NetworkEntityID SourceToken;
        public NetworkEntityID ReplacementToken;

        public SpawnEntityResponse(NetworkEntityID SourceToken, NetworkEntityID ReplacementToken)
        {
            this.SourceToken = SourceToken;
            this.ReplacementToken = ReplacementToken;
        }
    }

    [NetworkBlittable]
    public partial struct SpawnEntityCommand
    {

    }

    [NetworkBlittable]
    public partial struct DespawnEntityCommand
    {
        public NetworkEntityID ID;

        public DespawnEntityCommand(NetworkEntityID ID)
        {
            this.ID = ID;
        }
    }

    public partial struct ChangeScenesRequest : IAutoNetworkSerialization
    {
        public NetworkSceneLoadMode LoadMode;
        public List<NetworkSceneID> Scenes;

        public const int Capacity = 10;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref LoadMode);
            context.Select(ref Scenes);
        }

        public ChangeScenesRequest(NetworkSceneLoadMode LoadMode, List<NetworkSceneID> Scenes)
        {
            this.LoadMode = LoadMode;
            this.Scenes = Scenes;
        }
    }

    public partial struct ChangeScenesCommand : IAutoNetworkSerialization
    {
        public NetworkSceneLoadMode LoadMode;
        public List<NetworkSceneID> Scenes;

        public const int Capacity = ChangeScenesRequest.Capacity;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref LoadMode);
            context.Select(ref Scenes);
        }

        public ChangeScenesCommand(NetworkSceneLoadMode LoadMode, List<NetworkSceneID> Scenes)
        {
            this.LoadMode = LoadMode;
            this.Scenes = Scenes;
        }

        public static ChangeScenesCommand From(ChangeScenesRequest request) => new ChangeScenesCommand(request.LoadMode, request.Scenes);
    }
}
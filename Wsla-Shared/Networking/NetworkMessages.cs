using Wsla.Serialization;

namespace Wsla
{
    public struct ClientConnectionRequest : IAutoNetworkSerialization
    {
        public FixedString20 Username;

        public override string ToString() => $"(Username: {Username})";

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref Username);
        }

        public ClientConnectionRequest(FixedString20 Username)
        {
            this.Username = Username;
        }
    }
    [NetworkBlittable]
    public struct ClientConnectionResponse
    {
        public NetworkClientID LocalID;
        public NetworkClientID MasterID;

        public byte Clients;
        public byte SpawnTokens;
        public ushort Entities;

        public override string ToString() => $"(ClientID: {LocalID})";

        public ClientConnectionResponse(NetworkClientID LocalID, NetworkClientID MasterID, byte Clients, byte SpawnTokens, ushort Entities)
        {
            this.LocalID = LocalID;
            this.MasterID = MasterID;

            this.Clients = Clients;
            this.SpawnTokens = SpawnTokens;
            this.Entities = Entities;
        }
    }

    [NetworkBlittable]
    public struct ClientConnectMessage
    {

    }
    [NetworkBlittable]
    public struct ClientDisconnectMessage
    {
        public NetworkClientID ID;

        public ClientDisconnectMessage(NetworkClientID ID)
        {
            this.ID = ID;
        }
    }

    [NetworkBlittable]
    public struct SpawnPrefabEntityRequest
    {
        public NetworkEntityID SpawnToken;
        public NetworkEntityResource Resource;

        public NetworkEntityAuthorityMode Authority;

        public NetworkSceneVersion Scene;

        public SpawnPrefabEntityRequest(NetworkEntityID SpawnToken, NetworkEntityResource Resource, NetworkEntityAuthorityMode Authority, NetworkSceneVersion Scene)
        {
            this.SpawnToken = SpawnToken;

            this.Resource = Resource;
            this.Scene = Scene;

            this.Authority = Authority;
        }
    }

    [NetworkBlittable]
    public struct SpawnPrefabEntityResponse
    {
        public NetworkEntityID SourceToken;
        public NetworkEntityID ReplacementToken;

        public SpawnPrefabEntityResponseBehaviour Behaviour
        {
            get
            {
                if (SourceToken == ReplacementToken)
                    return SpawnPrefabEntityResponseBehaviour.Despawn;
                else
                    return SpawnPrefabEntityResponseBehaviour.Replicate;
            }
        }

        public SpawnPrefabEntityResponse(NetworkEntityID SourceToken, NetworkEntityID ReplacementToken)
        {
            this.SourceToken = SourceToken;
            this.ReplacementToken = ReplacementToken;
        }

        public SpawnPrefabEntityResponse Despawn(NetworkEntityID id) => new(id, id);
    }
    public enum SpawnPrefabEntityResponseBehaviour : byte
    {
        /// <summary>
        /// Replicate the entity on local client
        /// </summary>
        Replicate,

        /// <summary>
        /// Despawn the entity from the local client and re-use the spawn token
        /// </summary>
        Despawn,
    }

    public struct SpawnPrefabEntityCommand : IAutoNetworkSerialization
    {
        public NetworkEntityID ID;
        public NetworkEntityResource Resource;

        public NetworkEntityAuthorityMode Authority;
        public NetworkClientID Owner;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref ID);
            context.Select(ref Resource);

            context.Select(ref Authority);
            if (Authority is not NetworkEntityAuthorityMode.Authoritative)
                context.Select(ref Owner);
        }

        public SpawnPrefabEntityCommand(NetworkEntityID ID, NetworkEntityResource Resource, NetworkEntityAuthorityMode Authority, NetworkClientID Owner)
        {
            this.ID = ID;
            this.Resource = Resource;
            this.Authority = Authority;
            this.Owner = Owner;
        }
    }

    [NetworkBlittable]
    public struct SpawnScenenRequest { }

    [NetworkBlittable]
    public struct SpawnSceneCommand
    {

    }

    [NetworkBlittable]
    public struct DespawnEntityRequest
    {
        public NetworkEntityID ID;

        public DespawnEntityRequest(NetworkEntityID ID)
        {
            this.ID = ID;
        }
    }
    [NetworkBlittable]
    public struct DespawnEntityCommand
    {
        public NetworkEntityID ID;

        public DespawnEntityCommand(NetworkEntityID ID)
        {
            this.ID = ID;
        }
    }

    [NetworkBlittable]
    public struct ChangeSceneRequest
    {
        public NetworkSceneID Scene;

        public ChangeSceneRequest(NetworkSceneID Scene)
        {
            this.Scene = Scene;
        }
    }

    [NetworkBlittable]
    public struct ChangeSceneCommand
    {
        public NetworkSceneID ID;
        public NetworkSceneVersion Version;

        public ChangeSceneCommand(NetworkSceneID ID, NetworkSceneVersion Version)
        {
            this.ID = ID;
            this.Version = Version;
        }
    }

    [NetworkBlittable]
    public struct ChangeMasterClientCommand
    {
        public NetworkClientID MasterID;

        public ChangeMasterClientCommand(NetworkClientID MasterID)
        {
            this.MasterID = MasterID;
        }
    }
}
using System.Collections.Generic;

using Wsla.Serialization;

namespace Wsla
{
    public partial struct ClientConnectionRequest : IAutoNetworkSerialization
    {
        public string Username;

        public override string ToString() => $"(Username: {Username})";

        public void Select<TStream>(ref TStream stream, ref AutoSerializationContext context)
            where TStream : INetworkStream
        {
            context.Select(ref Username, ref stream);
        }

        public ClientConnectionRequest(string Username)
        {
            this.Username = Username;
        }
    }
    public partial struct ClientConnectionResponse : IAutoNetworkSerialization
    {
        public NetworkClientID ID;

        public byte Clients;
        public byte SpawnTokens;
        public byte Scenes;
        public ushort Entities;

        public override string ToString() => $"(ClientID: {ID})";

        public void Select<TStream>(ref TStream stream, ref AutoSerializationContext context)
            where TStream : INetworkStream
        {
            context.Select(ref ID, ref stream);

            context.Select(ref Clients, ref stream);
            context.Select(ref SpawnTokens, ref stream);
            context.Select(ref Scenes, ref stream);
            context.Select(ref Entities, ref stream);
        }

        public ClientConnectionResponse(NetworkClientID ID, byte Clients, byte SpawnTokens, byte Scenes, ushort Entities)
        {
            this.ID = ID;

            this.Clients = Clients;
            this.SpawnTokens = SpawnTokens;
            this.Scenes = Scenes;
            this.Entities = Entities;
        }
    }

    public partial struct ClientConnectMessage : IAutoNetworkSerialization
    {
        public void Select<TStream>(ref TStream stream, ref AutoSerializationContext context)
            where TStream : INetworkStream
        {

        }
    }
    public partial struct ClientDisconnectMessage : IAutoNetworkSerialization
    {
        public NetworkClientID ID;

        public void Select<TStream>(ref TStream stream, ref AutoSerializationContext context)
            where TStream : INetworkStream
        {
            context.Select(ref ID, ref stream);
        }

        public ClientDisconnectMessage(NetworkClientID ID)
        {
            this.ID = ID;
        }
    }

    public partial struct SpawnEntityRequest : IAutoNetworkSerialization
    {
        public NetworkEntityID SpawnToken;
        public NetworkEntityResource Resource;

        public void Select<TStream>(ref TStream stream, ref AutoSerializationContext context)
            where TStream : INetworkStream
        {
            context.Select(ref SpawnToken, ref stream);
            context.Select(ref Resource, ref stream);
        }

        public SpawnEntityRequest(NetworkEntityID SpawnToken, NetworkEntityResource Resource)
        {
            this.SpawnToken = SpawnToken;
            this.Resource = Resource;
        }
    }

    public partial struct SpawnEntityResponse : IAutoNetworkSerialization
    {
        public NetworkEntityID SourceToken;
        public NetworkEntityID ReplacementToken;

        public void Select<TStream>(ref TStream stream, ref AutoSerializationContext context)
            where TStream : INetworkStream
        {
            context.Select(ref SourceToken, ref stream);
            context.Select(ref ReplacementToken, ref stream);
        }

        public SpawnEntityResponse(NetworkEntityID SourceToken, NetworkEntityID ReplacementToken)
        {
            this.SourceToken = SourceToken;
            this.ReplacementToken = ReplacementToken;
        }
    }

    public partial struct SpawnEntityCommand : IAutoNetworkSerialization
    {
        public void Select<TStream>(ref TStream stream, ref AutoSerializationContext context)
            where TStream : INetworkStream
        {

        }
    }

    public partial struct DespawnEntityCommand : IAutoNetworkSerialization
    {
        public NetworkEntityID ID;

        public void Select<TStream>(ref TStream stream, ref AutoSerializationContext context)
            where TStream : INetworkStream
        {
            context.Select(ref ID, ref stream);
        }

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

        public void Select<TStream>(ref TStream stream, ref AutoSerializationContext context)
            where TStream : INetworkStream
        {
            context.Select(ref LoadMode, ref stream);
            context.Select(ref Scenes, ref stream);
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

        public void Select<TStream>(ref TStream stream, ref AutoSerializationContext context)
            where TStream : INetworkStream
        {
            context.Select(ref LoadMode, ref stream);
            context.Select(ref Scenes, ref stream);
        }

        public ChangeScenesCommand(NetworkSceneLoadMode LoadMode, List<NetworkSceneID> Scenes)
        {
            this.LoadMode = LoadMode;
            this.Scenes = Scenes;
        }

        public static ChangeScenesCommand From(ChangeScenesRequest request) => new ChangeScenesCommand(request.LoadMode, request.Scenes);
    }
}
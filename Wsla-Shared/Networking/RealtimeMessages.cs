using System;
using System.Collections;
using System.Collections.Generic;

using Wsla.Serialization;

namespace Wsla
{
    public struct ClientConnectionRequest : IAutoNetworkSerialization
    {
        public FixedString<FS20> Username;
        public FixedString<FS20> Password;

        public NetworkGroupCollection Groups;

        public RoomTimeRequest TimeRequest;

        public override string ToString() => $"(Username: {Username})";

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref Username);
            context.Select(ref Password);
            context.Select(ref Groups);
            context.Select(ref TimeRequest);
        }

        public ClientConnectionRequest(FixedString<FS20> Username, FixedString<FS20> Password, NetworkGroupCollection Groups)
        {
            this.Username = Username;
            this.Password = Password;
            this.Groups = Groups;

            TimeRequest = default;
        }
    }
    public struct ClientConnectionResponse : IAutoNetworkSerialization
    {
        public NetworkClientID LocalID;
        public NetworkClientID MasterID;

        public RoomTimeResponse TimeResponse;

        public byte Clients;
        public byte SpawnTokens;
        public ushort Entities;

        public NetworkSceneDefinition Scene;

        public override string ToString() => $"(ClientID: {LocalID})";

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref LocalID);
            context.Select(ref MasterID);

            context.Select(ref TimeResponse);

            context.Select(ref Clients);
            context.Select(ref SpawnTokens);
            context.Select(ref Entities);

            context.Select(ref Scene);
        }

        public ClientConnectionResponse(NetworkClientID LocalID, NetworkClientID MasterID, RoomTimeResponse TimeResponse, byte Clients, byte SpawnTokens, ushort Entities, NetworkSceneDefinition Scene)
        {
            this.LocalID = LocalID;
            this.MasterID = MasterID;

            this.TimeResponse = TimeResponse;

            this.Clients = Clients;
            this.SpawnTokens = SpawnTokens;
            this.Entities = Entities;

            this.Scene = Scene;
        }
    }

    public struct ClientConnectMessage : IAutoNetworkSerialization
    {
        public NetworkClientDefinition Client;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref Client);
        }

        public ClientConnectMessage(NetworkClientDefinition Client)
        {
            this.Client = Client;
        }
    }
    [NetworkBlittable]
    public struct ClientDisconnectMessage
    {
        public NetworkClientID ClientID;

        NetworkClientID? MasterID;

        public bool IsMasterClientChange() => MasterID.HasValue;
        public bool IsMasterClientChange(out NetworkClientID id)
        {
            if (MasterID.HasValue is false)
            {
                id = default;
                return false;
            }

            id = MasterID.Value;
            return true;
        }

        public ClientDisconnectMessage(NetworkClientID ClientID, NetworkClientID? MasterID)
        {
            this.ClientID = ClientID;
            this.MasterID = MasterID;
        }

        public struct EntityHandlingPayload : IAutoNetworkSerialization
        {
            public NetworkEntityID ID;
            public EntityDisconnectBehaviour Behaviour;

            public void Select(ref AutoSerializationContext context)
            {
                context.Select(ref ID);
                context.Select(ref Behaviour);
            }

            public EntityHandlingPayload(NetworkEntityID ID, EntityDisconnectBehaviour Behaviour)
            {
                this.ID = ID;
                this.Behaviour = Behaviour;
            }

            public ref struct Writer
            {
                public void Write(NetworkEntityID ID, EntityDisconnectBehaviour Behaviour)
                {
                    var handling = new EntityHandlingPayload(ID, Behaviour);
                    NetworkSerializer.WriteValue(handling, Stream);
                }

                readonly INetworkStream Stream;
                public Writer(INetworkStream Stream)
                {
                    this.Stream = Stream;
                }
            }
            public ref struct Reader
            {
                readonly INetworkStream Stream;
                public EntityHandlingPayload Current { get; private set; }

                public bool MoveNext()
                {
                    if (Stream.Available == 0)
                        return false;

                    Current = NetworkSerializer.ReadValue<EntityHandlingPayload>(Stream);
                    return true;
                }

                public Reader GetEnumerator() => this;

                public Reader(INetworkStream Stream)
                {
                    this.Stream = Stream;
                    Current = default;
                }
            }
        }
    }

    [NetworkBlittable]
    public struct SpawnPrefabEntityRequest
    {
        public NetworkEntityID SpawnToken;
        public NetworkResourceID Resource;

        public NetworkEntityAuthorityMode Authority;

        public NetworkSceneVersion Scene;

        public SpawnPrefabEntityRequest(NetworkEntityID SpawnToken, NetworkResourceID Resource, NetworkEntityAuthorityMode Authority, NetworkSceneVersion Scene)
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
        public NetworkResourceID Resource;

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

        public SpawnPrefabEntityCommand(NetworkEntityID ID, NetworkResourceID Resource, NetworkEntityAuthorityMode Authority, NetworkClientID Owner)
        {
            this.ID = ID;
            this.Resource = Resource;
            this.Authority = Authority;
            this.Owner = Owner;
        }
    }

    [NetworkBlittable]
    public struct SpawnSceneRequest
    {
        public class AuthorizationPayload
        {
            public ref struct Writer
            {
                readonly INetworkStream Stream;
                readonly byte Capacity;

                byte Counter;

                public void Write(NetworkEntityAuthorityMode ID)
                {
                    Counter += 1;

                    NetworkSerializer.WriteValue(ID, Stream);
                }

                public void Dispose()
                {
                    if (Capacity != Counter)
                        throw new InvalidOperationException($"Not Enough Entities Written, Expected {Capacity}, Got {Counter}");
                }

                public Writer(INetworkStream Stream, byte Capacity)
                {
                    this.Stream = Stream;
                    this.Capacity = Capacity;
                    Counter = 0;

                    NetworkSerializer.WriteValue(Capacity, Stream);
                }
            }
            public ref struct Reader
            {
                readonly INetworkStream Stream;
                public byte Count { get; }

                byte Index;

                public NetworkEntityAuthorityMode Read()
                {
                    Index += 1;
                    return NetworkSerializer.ReadValue<NetworkEntityAuthorityMode>(Stream);
                }

                public void Dispose()
                {
                    if (Count != Index)
                        throw new InvalidOperationException($"({typeof(Reader).FullName}) Didn't Read All Data, Read {Index}, Total: {Count}");

                    if (Stream.Available > 0)
                        throw new InvalidOperationException($"({typeof(Reader).FullName}) Still Contains {Stream.Available} Bytes After Reading All ({Count}) Entities");
                }

                public Reader(INetworkStream Stream)
                {
                    this.Stream = Stream;
                    Count = NetworkSerializer.ReadValue<byte>(Stream);

                    Index = default;
                }
            }
        }
    }

    [NetworkBlittable]
    public struct SpawnSceneCommand
    {
        public class EntityIDPayload
        {
            public ref struct Writer
            {
                readonly INetworkStream Stream;
                readonly byte Capacity;

                byte Counter;

                public void Write(NetworkEntityID ID)
                {
                    Counter += 1;

                    NetworkSerializer.WriteValue(ID, Stream);
                }

                public void Dispose()
                {
                    if (Capacity != Counter)
                        throw new InvalidOperationException($"Not Enough Entities Written, Expected {Capacity}, Got {Counter}");
                }

                public Writer(INetworkStream Stream, byte Capacity)
                {
                    this.Stream = Stream;
                    this.Capacity = Capacity;
                    Counter = 0;
                }
            }
            public ref struct Reader
            {
                readonly INetworkStream Stream;
                public byte Count { get; }

                byte Index;

                public NetworkEntityID Read()
                {
                    Index += 1;
                    return NetworkSerializer.ReadValue<NetworkEntityID>(Stream);
                }

                public void Dispose()
                {
                    if (Count != Index)
                        throw new InvalidOperationException($"({typeof(Reader).FullName}) Didn't Read All Data, Read {Index}, Total: {Count}");

                    if (Stream.Available > 0)
                        throw new InvalidOperationException($"({typeof(Reader).FullName}) Still Contains {Stream.Available} Bytes After Reading All ({Count}) Entities");
                }

                public Reader(INetworkStream Stream, byte Count)
                {
                    this.Stream = Stream;
                    this.Count = Count;

                    Index = default;
                }
            }
        }
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
    public struct TakeEntityOwnershipRequest
    {
        public NetworkEntityID ID;
        public NetworkEntityTransferToken Token;

        public TakeEntityOwnershipRequest(NetworkEntityID Entity, NetworkEntityTransferToken Token)
        {
            this.ID = Entity;
            this.Token = Token;
        }
    }

    [NetworkBlittable]
    public struct TransferEntityOwnershipCommand
    {
        public NetworkClientID Client;
        public NetworkEntityID Entity;

        public NetworkEntityTransferToken Token;

        public TransferEntityOwnershipCommand(NetworkClientID Client, NetworkEntityID Entity, NetworkEntityTransferToken Token)
        {
            this.Client = Client;
            this.Entity = Entity;
            this.Token = Token;
        }
    }

    public enum EntityDisconnectBehaviour : byte
    {
        /// <summary>
        /// Despawn the Entity
        /// </summary>
        Despawn,

        /// <summary>
        /// Transfer the Entity to the Master Client
        /// </summary>
        Transfer,
    }

    [NetworkBlittable]
    public struct NetworkPingMessage
    {
        public DateTime Time { get; }

        public NetworkPingMessage(DateTime Time)
        {
            this.Time = Time;
        }

        public static NetworkPingMessage Create() => new NetworkPingMessage(DateTime.Now);
    }
    [NetworkBlittable]
    public struct NetworkPongMessage
    {
        public DateTime Time { get; }

        public NetworkPongMessage(DateTime Time)
        {
            this.Time = Time;
        }

        public static NetworkPongMessage From(NetworkPingMessage ping) => new NetworkPongMessage(ping.Time);
    }

    [NetworkBlittable]
    public struct RoomTimeRequest
    {
        public TimeSpan ClientTime;

        public RoomTimeRequest(TimeSpan ClientTime)
        {
            this.ClientTime = ClientTime;
        }
    }
    [NetworkBlittable]
    public struct RoomTimeResponse
    {
        public RoomTimeRequest ClientRequest;
        public TimeSpan RoomTime;

        public RoomTimeResponse(RoomTimeRequest ClientRequest, TimeSpan RoomTime)
        {
            this.ClientRequest = ClientRequest;
            this.RoomTime = RoomTime;
        }
    }

    [NetworkBlittable]
    public struct ChangeGroupsRequest
    {
        public NetworkGroupCollection Groups;

        public ChangeGroupsRequest(NetworkGroupCollection Groups)
        {
            this.Groups = Groups;
        }
    }
}
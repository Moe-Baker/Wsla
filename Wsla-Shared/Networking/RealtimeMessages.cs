using System;

using Wsla.Serialization;

namespace Wsla
{
    #region Client Connection
    public struct ClientConnectionRequest : IAutoNetworkSerialization
    {
        public NetworkVersion ApiVersion;
        public NetworkVersion GameVersion;

        public FixedString<FS20> Username;
        public FixedString<FS20> Password;

        public NetworkGroupCollection Groups;

        public RoomTimeRequest TimeRequest;

        public override string ToString() => $"(Username: {Username})";

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref ApiVersion);
            context.Select(ref GameVersion);

            context.Select(ref Username);
            context.Select(ref Password);
            context.Select(ref Groups);
            context.Select(ref TimeRequest);
        }

        public ClientConnectionRequest(NetworkVersion GameVersion, FixedString<FS20> Username, FixedString<FS20> Password, NetworkGroupCollection Groups)
        {
            this.ApiVersion = Constants.ApiVersion;
            this.GameVersion = GameVersion;

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

        public SparseArray<NetworkSceneState> Scenes;

        public override string ToString() => $"(ClientID: {LocalID})";

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref LocalID);
            context.Select(ref MasterID);

            context.Select(ref TimeResponse);

            context.Select(ref Clients);
            context.Select(ref SpawnTokens);
            context.Select(ref Entities);

            context.Select(ref Scenes);
        }

        public ClientConnectionResponse(NetworkClientID LocalID, NetworkClientID MasterID, RoomTimeResponse TimeResponse, byte Clients, byte SpawnTokens, ushort Entities, SparseArray<NetworkSceneState> Scenes)
        {
            this.LocalID = LocalID;
            this.MasterID = MasterID;

            this.TimeResponse = TimeResponse;

            this.Clients = Clients;
            this.SpawnTokens = SpawnTokens;
            this.Entities = Entities;

            this.Scenes = Scenes;
        }

        public static class SpawnTokenPayload
        {
            public ref struct Writer
            {
                readonly INetworkStream Stream;

                public void Write(NetworkEntityID token)
                {
                    NetworkSerializer.WriteValue(token, Stream);
                }

                public void Dispose() { }

                public Writer(INetworkStream Stream)
                {
                    this.Stream = Stream;
                }
            }
            public ref struct Reader
            {
                readonly INetworkStream Stream;
                public int Count { get; }

                int Index;

                public NetworkEntityID Read()
                {
                    Index += 1;
                    return NetworkSerializer.ReadValue<NetworkEntityID>(Stream);
                }

                public void Dispose()
                {
                    if (Count != Index)
                        throw new InvalidOperationException($"({typeof(Reader).FullName}) Mismatched Read, Read {Index}, Expected {Count}");
                }

                public Reader(INetworkStream Stream, int Count)
                {
                    this.Stream = Stream;
                    this.Count = Count;

                    Index = 0;
                }
            }
        }
    }
    #endregion

    #region Client Connected/Disconnected
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
    #endregion

    #region Spawn Prefab
    [NetworkBlittable]
    public struct SpawnPrefabEntityRequest
    {
        public NetworkEntityID SpawnToken;
        public NetworkResourceID Resource;

        public NetworkEntityAuthorityMode Authority;

        public NetworkSceneDefinition Scene;

        public SpawnPrefabEntityRequest(NetworkEntityID SpawnToken, NetworkResourceID Resource, NetworkEntityAuthorityMode Authority, NetworkSceneDefinition Scene)
        {
            this.SpawnToken = SpawnToken;

            this.Resource = Resource;
            this.Scene = Scene;

            this.Authority = Authority;
        }

        public static class SyncMemberInitializationPayload
        {
            public ref struct Writer
            {
                int Cursor;
                BinarySource Header;

                public void Dispose()
                {
                    var length = (ushort)(Stream.Position - Cursor);
                    NetworkSerializer.WriteValue(in length, ref Header);
                }

                public INetworkStream Stream { get; }
                public Writer(INetworkStream Stream, NetworkBehaviourID behaviour, SyncMemberType type, NetworkSyncMemberID member)
                {
                    this.Stream = Stream;

                    NetworkSerializer.WriteValue(behaviour, Stream);
                    NetworkSerializer.WriteValue(type, Stream);
                    NetworkSerializer.WriteValue(member, Stream);

                    //Allocate Header
                    {
                        var span = Stream.AllocateMemory(sizeof(ushort));
                        Header = BinarySource.From(span);
                    }

                    Cursor = Stream.Position;
                }
            }
            public ref struct Reader
            {
                public bool TryRead(out NetworkBehaviourID behaviour, out SyncMemberType type, out NetworkSyncMemberID member, out Memory<byte> data)
                {
                    if (Stream.Available is 0)
                    {
                        behaviour = default;
                        type = default;
                        member = default;
                        data = default;
                        return false;
                    }

                    NetworkSerializer.ReadValue(Stream, out behaviour);
                    NetworkSerializer.ReadValue(Stream, out type);
                    NetworkSerializer.ReadValue(Stream, out member);

                    NetworkSerializer.ReadValue(Stream, out ushort length);
                    data = Stream.ReadMemory(length);

                    return true;
                }

                public void Dispose() { }

                readonly INetworkStream Stream;
                public Reader(INetworkStream Stream)
                {
                    this.Stream = Stream;
                }
            }
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

        public NetworkSceneID Scene;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref ID);
            context.Select(ref Resource);

            context.Select(ref Authority);
            if (Authority is not NetworkEntityAuthorityMode.Authoritative)
                context.Select(ref Owner);

            context.Select(ref Scene);
        }

        public SpawnPrefabEntityCommand(NetworkEntityID ID, NetworkResourceID Resource, NetworkEntityAuthorityMode Authority, NetworkClientID Owner, NetworkSceneID Scene)
        {
            this.ID = ID;
            this.Resource = Resource;
            this.Authority = Authority;
            this.Owner = Owner;
            this.Scene = Scene;
        }
    }
    #endregion

    #region Despawn Entity
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
    #endregion

    #region Spawn Scene
    [NetworkBlittable]
    public struct SpawnSceneRequest
    {
        public class AuthorizationPayload
        {
            public ref struct SceneWriter
            {
                public byte Count { get; }

                public EntryWriter Write(NetworkSceneID ID, byte Entries)
                {
                    NetworkSerializer.WriteValue(ID, Stream);
                    return new EntryWriter(Stream, Entries);
                }

                public void Dispose() { }

                readonly INetworkStream Stream;
                public SceneWriter(INetworkStream Stream, byte Count)
                {
                    this.Stream = Stream;
                    this.Count = Count;

                    Stream.WriteByte(Count);
                }
            }
            public ref struct SceneReader
            {
                public byte Count { get; }

                public EntryReader Read()
                {
                    NetworkSerializer.ReadValue(Stream, out NetworkSceneID id);
                    return new EntryReader(Stream, id);
                }

                public void Dispose() { }

                readonly INetworkStream Stream;
                public SceneReader(INetworkStream Stream)
                {
                    this.Stream = Stream;

                    Count = Stream.ReadByte();
                }
            }

            public ref struct EntryWriter
            {
                public byte Count { get; }

                public void Write(NetworkEntityAuthorityMode entry) => NetworkSerializer.WriteValue(entry, Stream);

                public void Dispose() { }

                readonly INetworkStream Stream;
                public EntryWriter(INetworkStream Stream, byte Count)
                {
                    this.Stream = Stream;
                    this.Count = Count;

                    Stream.WriteByte(Count);
                }
            }
            public ref struct EntryReader
            {
                public NetworkSceneID Scene { get; }
                public byte Count { get; }

                public NetworkEntityAuthorityMode Read() => NetworkSerializer.ReadValue<NetworkEntityAuthorityMode>(Stream);

                public void Dispose() { }

                readonly INetworkStream Stream;
                public EntryReader(INetworkStream Stream, NetworkSceneID Scene)
                {
                    this.Stream = Stream;
                    this.Scene = Scene;

                    Count = Stream.ReadByte();
                }
            }
        }
    }

    [NetworkBlittable]
    public struct SpawnSceneCommand
    {
        public class EntityIDPayload
        {
            public ref struct SceneWriter
            {
                public byte Count { get; }

                public EntryWriter Write(NetworkSceneID ID, byte Entries)
                {
                    NetworkSerializer.WriteValue(ID, Stream);
                    return new EntryWriter(Stream, Entries);
                }

                public void Dispose() { }

                readonly INetworkStream Stream;
                public SceneWriter(INetworkStream Stream, byte Count)
                {
                    this.Stream = Stream;
                    this.Count = Count;

                    Stream.WriteByte(Count);
                }
            }
            public ref struct SceneReader
            {
                public byte Count { get; }

                public EntryReader Read()
                {
                    NetworkSerializer.ReadValue(Stream, out NetworkSceneID id);
                    return new EntryReader(Stream, id);
                }

                public void Dispose() { }

                readonly INetworkStream Stream;
                public SceneReader(INetworkStream Stream)
                {
                    this.Stream = Stream;

                    Count = Stream.ReadByte();
                }
            }

            public ref struct EntryWriter
            {
                public byte Count { get; }

                public void Write(NetworkEntityID entry) => NetworkSerializer.WriteValue(entry, Stream);

                public void Dispose() { }

                readonly INetworkStream Stream;
                public EntryWriter(INetworkStream Stream, byte Count)
                {
                    this.Stream = Stream;
                    this.Count = Count;

                    Stream.WriteByte(Count);
                }
            }
            public ref struct EntryReader
            {
                public NetworkSceneID Scene { get; }
                public byte Count { get; }

                public NetworkEntityID Read() => NetworkSerializer.ReadValue<NetworkEntityID>(Stream);

                public void Dispose() { }

                readonly INetworkStream Stream;
                public EntryReader(INetworkStream Stream, NetworkSceneID Scene)
                {
                    this.Stream = Stream;
                    this.Scene = Scene;

                    Count = Stream.ReadByte();
                }
            }
        }
    }
    #endregion

    #region Change Scene
    public struct ChangeSceneRequest : IAutoNetworkSerialization
    {
        public SparseArray<NetworkSceneID> Scenes;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref Scenes);
        }

        public ChangeSceneRequest(SparseArray<NetworkSceneID> Scenes)
        {
            this.Scenes = Scenes;
        }
    }

    public struct ChangeSceneCommand : IAutoNetworkSerialization
    {
        public SparseArray<NetworkSceneDefinition> Scenes;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref Scenes);
        }

        public ChangeSceneCommand(SparseArray<NetworkSceneDefinition> Scenes)
        {
            this.Scenes = Scenes;
        }
    }
    #endregion

    #region Modify Scene
    public struct ModifyScenesRequest : IAutoNetworkSerialization
    {
        public SparseArray<NetworkSceneID> Unload;
        public SparseArray<NetworkSceneID> Load;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref Unload);
            context.Select(ref Load);
        }

        public ModifyScenesRequest(SparseArray<NetworkSceneID> Unload, SparseArray<NetworkSceneID> Load)
        {
            this.Unload = Unload;
            this.Load = Load;
        }
    }
    public struct ModifyScenesCommand : IAutoNetworkSerialization
    {
        public SparseArray<NetworkSceneID> Unload;
        public SparseArray<NetworkSceneDefinition> Load;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref Unload);
            context.Select(ref Load);
        }

        public ModifyScenesCommand(SparseArray<NetworkSceneID> Unload, SparseArray<NetworkSceneDefinition> Load)
        {
            this.Unload = Unload;
            this.Load = Load;
        }
    }
    #endregion

    #region Entity Ownership
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
    #endregion

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

    #region Ping/Pong
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
    #endregion

    #region Time
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
    #endregion

    #region Group
    [NetworkBlittable]
    public struct ChangeGroupsRequest
    {
        public NetworkGroupCollection Groups;

        public ChangeGroupsRequest(NetworkGroupCollection Groups)
        {
            this.Groups = Groups;
        }
    }
    #endregion
}
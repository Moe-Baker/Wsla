using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using Cysharp.Threading.Tasks;

using LiteNetLib;
using LiteNetLib.Utils;

using NUnit.Framework;

using Toolbox;

using UnityEngine;
using UnityEngine.SceneManagement;

using Wsla;
using Wsla.Serialization;

namespace Wsla.Unity
{
    [Serializable]
    public class RoomAPI : NetworkAPI.Property
    {
        public RoomInstance Instance { get; private set; }

        public async UniTask<Response<RoomInstance, WslaError>> Connect(IPAddress address, ushort port, ClientConnectionRequest request)
        {
            var target = new RoomInstance(address, port);

            var response = await target.Start(request);

            if (response.IsError)
                return response.Error;

            Instance = target;

            return target;
        }
    }

    [Serializable]
    public class RoomInstance
    {
        static NetworkAPI NetworkAPI => NetworkAPI.Instance;

        public TransportProperty Transport { get; }
        public class TransportProperty
        {
            public IPAddress Address { get; }
            public ushort Port { get; }

            public DispatcherProperty Dispatcher { get; }
            public class DispatcherProperty
            {
                ActionDelegate[] Handlers;
                public delegate void ActionDelegate(NetPacketReader reader, byte channel, DeliveryMethod delivery);

                void ReceiveCallback(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod delivery)
                {
                    var id = NetworkTypeSerializationResolver.ReadValue(reader);

                    var handler = Handlers[id];
                    if (handler is null)
                    {
                        NetworkLog.Error($"No Dispatch Handler Provided for {NetworkTypes.Get(id)} Message");
                        return;
                    }

                    handler(reader, channel, delivery);

                    reader.Recycle();
                }

                public delegate void TypeDelegate<T>(ref T message, NetPacketReader reader, byte channel, DeliveryMethod delivery);
                public void Register<[NetworkSerializationMarker] T>(TypeDelegate<T> handler)
                {
                    var id = NetworkTypes.Get<T>();

                    Handlers[id] = Surrogate;

                    void Surrogate(NetPacketReader reader, byte channel, DeliveryMethod delivery)
                    {
                        NetworkSerializer.ReadValue(reader, out T data);
                        handler(ref data, reader, channel, delivery);
                    }
                }

                readonly TransportProperty Transport;
                public DispatcherProperty(TransportProperty transport)
                {
                    this.Transport = transport;

                    Handlers = new ActionDelegate[NetworkTypes.Capacity];

                    Transport.Listener.OnNetworkReceive += ReceiveCallback;
                }
            }

            public readonly SinglePacketWriter PacketWriter;

            public NetManager Manager { get; }

            public BufferListener Listener { get; }
            public class BufferListener : INetEventListener, IDisposable
            {
                public bool Active { get; private set; }

                public void Pause()
                {
                    Active = false;
                }
                public void Resume()
                {
                    Active = true;

                    while (EventQueue.TryDequeue(out var type))
                    {
                        switch (type)
                        {
                            case EventType.PeerConnect:
                            {
                                var peer = PeerConnectedQueue.Dequeue();
                                OnPeerConnected?.Invoke(peer);
                            }
                            break;

                            case EventType.PeerDisconnect:
                            {
                                var (peer, info) = PeerDisconnectedQueue.Dequeue();
                                OnPeerDisconnected?.Invoke(peer, info);
                            }
                            break;

                            case EventType.NetworkReceive:
                            {
                                var (peer, reader, channel, delivery) = NetworkReceiveQueue.Dequeue();
                                OnNetworkReceive?.Invoke(peer, reader, channel, delivery);
                            }
                            break;
                        }
                    }
                }

                Queue<EventType> EventQueue;
                public enum EventType
                {
                    PeerConnect,
                    PeerDisconnect,
                    NetworkReceive,
                }

                #region Peer Connected
                Queue<NetPeer> PeerConnectedQueue;

                void INetEventListener.OnPeerConnected(NetPeer peer)
                {
                    if (Active is false)
                    {
                        EventQueue.Enqueue(EventType.PeerConnect);
                        PeerConnectedQueue.Enqueue(peer);

                        return;
                    }

                    OnPeerConnected?.Invoke(peer);
                }

                public event PeerConnectedDelegate OnPeerConnected;
                public delegate void PeerConnectedDelegate(NetPeer peer);
                #endregion

                #region Peer Disconnected
                Queue<(NetPeer Peer, DisconnectInfo Info)> PeerDisconnectedQueue;

                void INetEventListener.OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
                {
                    if (Active is false)
                    {
                        EventQueue.Enqueue(EventType.PeerDisconnect);
                        PeerDisconnectedQueue.Enqueue((peer, info));

                        return;
                    }

                    OnPeerDisconnected?.Invoke(peer, info);
                }

                public event PeerDisconnectedDelegate OnPeerDisconnected;
                public delegate void PeerDisconnectedDelegate(NetPeer peer, DisconnectInfo info);
                #endregion

                #region Network Receive
                Queue<(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod delivery)> NetworkReceiveQueue;

                void INetEventListener.OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod delivery)
                {
                    if (Active is false)
                    {
                        EventQueue.Enqueue(EventType.NetworkReceive);
                        NetworkReceiveQueue.Enqueue((peer, reader, channel, delivery));

                        return;
                    }

                    OnNetworkReceive?.Invoke(peer, reader, channel, delivery);
                }

                public delegate void NetworkReceiveDelegate(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod delivery);
                public event NetworkReceiveDelegate OnNetworkReceive;
                #endregion

                #region Unused
                void INetEventListener.OnConnectionRequest(ConnectionRequest request) { }
                void INetEventListener.OnNetworkError(IPEndPoint endPoint, SocketError socketError) { }
                void INetEventListener.OnNetworkLatencyUpdate(NetPeer peer, int latency) { }
                void INetEventListener.OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) { }
                #endregion

                public void Dispose()
                {
                    foreach (var packet in NetworkReceiveQueue)
                        packet.reader.Recycle();
                }

                public BufferListener()
                {
                    Active = true;

                    EventQueue = new(50);
                    PeerConnectedQueue = new(1);
                    PeerDisconnectedQueue = new(50);
                    NetworkReceiveQueue = new(1);
                }
            }

            public NetPeer Peer { get; private set; }

            CancellationTokenSource CancellationSource;

            internal async UniTask<Response<WslaError>> Start(ClientConnectionRequest request)
            {
                Manager.Start();

                var operation = new UniTaskCompletionSource<Response<WslaError>>();

                //Register Hooks
                Listener.OnPeerConnected += Connected;
                void Connected(NetPeer peer) => operation.TrySetResult(true);

                Listener.OnPeerDisconnected += Disconnect;
                void Disconnect(NetPeer peer, DisconnectInfo info) => operation.TrySetResult(WslaError.From(info));

                //Request
                {
                    var packet = Room.Transport.PacketWriter.Take();

                    NetworkSerializer.WriteValue(in request, packet);

                    var endpoint = new IPEndPoint(Address, Port);

                    Peer = Manager.Connect(endpoint, packet);
                }

                CancellationSource = new CancellationTokenSource();
                Poll(CancellationSource.Token).Forget();

                var response = await operation.Task;

                //Clear Hooks
                Listener.OnPeerConnected -= Connected;
                Listener.OnPeerDisconnected -= Disconnect;

                if (response.IsError)
                {
                    Stop();
                    return response.Error;
                }

                return true;
            }
            internal void Stop()
            {
                CancellationSource.Cancel();
                Listener.Dispose();
            }

            async UniTask Poll(CancellationToken cancellation)
            {
                while (true)
                {
                    Manager.PollEvents();

                    await UniTask.NextFrame();

                    if (cancellation.IsCancellationRequested)
                        break;
                }
            }

            public void SendData<[NetworkSerializationMarker] T>(in T data, byte channel = 0, DeliveryMethod delivery = DeliveryMethod.ReliableOrdered)
            {
                var writer = PacketWriter.Take();

                NetworkSerializer.WriteHeader(data, writer);

                SendWriter(writer, channel, delivery);
            }
            public void SendWriter(in NetDataWriter writer, byte channel = 0, DeliveryMethod delivery = DeliveryMethod.ReliableOrdered)
            {
                Peer.Send(writer, channel, delivery);
            }

            RoomInstance Room;
            public TransportProperty(RoomInstance room, IPAddress address, ushort port)
            {
                this.Room = room;

                this.Address = address;
                this.Port = port;

                Listener = new BufferListener();

                Manager = new NetManager(Listener);
                Manager.AutoRecycle = false;

                PacketWriter = SinglePacketWriter.Create(256);

                Dispatcher = new DispatcherProperty(this);
            }
        }

        public ClientsProperty Clients { get; }
        public class ClientsProperty
        {
            public NetworkClient Master { get; private set; }
            public LocalNetworkClient Local { get; private set; }

            ExpandArray<NetworkClient> Collection;
            public byte Count => (byte)Collection.Count;
            public bool TryGet(NetworkClientID id, out NetworkClient client) => Collection.TryGet(id.Value, out client);

            TransportProperty Transport => Room.Transport;

            void ConnectResponseHandler(ref ClientConnectionResponse message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                Procedure(message, reader, channel, delivery).Forget();
                async UniTask Procedure(ClientConnectionResponse message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
                {
                    Transport.Listener.Pause();

                    //Sync Clients
                    ReadState(reader, message);

                    //Check if we didn't recieve our local client
                    if (Local is null)
                        throw new InvalidOperationException("No Local Client Received in Response");

                    //Check if we didn't recieve the master client
                    if (Master is null)
                        throw new InvalidOperationException("No Master Client Received in Response");

                    //Sync Spawn Tokens
                    Local.ReadSpawnTokens(reader, message);

                    //Sync Scenes
                    await Room.Scenes.ReadState(reader, message);

                    //Sync Entities
                    Room.Entities.ReadState(reader, message);

                    Transport.Listener.Resume();
                }
            }

            void ReadState(NetPacketReader reader, ClientConnectionResponse message)
            {
                for (int i = 0; i < message.Clients; i++)
                {
                    var id = NetworkClient.ReadID(reader);

                    NetworkClient client;

                    if (id == message.LocalID)
                        client = Local = new LocalNetworkClient(Room, id, message.SpawnTokens);
                    else
                        client = new RemoteNetworkClient(Room, id);

                    if (id == message.MasterID)
                        Master = client;

                    client.ReadState(reader);

                    Register(client);
                }
            }

            void ClientConnectHandler(ref ClientConnectMessage message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                var client = RemoteNetworkClient.ReadInstance(Room, ref reader);

                Register(client);
            }
            void ClientDisconnectHandler(ref ClientDisconnectMessage message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                Unregister(message.ID);
            }

            void Register(NetworkClient client)
            {
                Debug.Log($"Registerd Client {client}");

                Collection.Add(client.ID.Value, client);
            }
            void Unregister(NetworkClientID id)
            {
                Collection.Remove(id.Value, out var client);

                Debug.Log($"Unregisterd Client {client}");
            }

            readonly RoomInstance Room;
            public ClientsProperty(RoomInstance room)
            {
                this.Room = room;

                Collection = new ExpandArray<NetworkClient>(10, NetworkClientID.MaxValue, 10);

                Transport.Dispatcher.Register<ClientConnectionResponse>(ConnectResponseHandler);
                Transport.Dispatcher.Register<ClientConnectMessage>(ClientConnectHandler);
                Transport.Dispatcher.Register<ClientDisconnectMessage>(ClientDisconnectHandler);
            }
        }

        public EntitiesProperty Entities { get; }
        public class EntitiesProperty
        {
            public Dictionary<NetworkEntityID, NetworkEntity> Dictionary { get; }
            public int Count => Dictionary.Count;

            public bool TryGet(NetworkEntityID id, out NetworkEntity entity) => Dictionary.TryGetValue(id, out entity);

            TransportProperty Transport => Room.Transport;

            internal void ReadState(NetPacketReader reader, ClientConnectionResponse message)
            {
                for (int i = 0; i < message.Entities; i++)
                    Room.Entities.SpawnBuffered(reader);
            }

            public SpawnOptions Spawn() => new SpawnOptions(Room);
            public ref struct SpawnOptions
            {
                readonly RoomInstance Room;

                internal NetworkEntityID Token;
                internal NetworkEntityResource Resource;
                internal NetworkEntityAuthorityMode Authority;
                internal NetworkEntityLifetimeMode Lifetime;
                internal NetworkScene Scene;

                public SpawnOptions SetResource(NetworkEntityResource value)
                {
                    ResourceAssigned = true;
                    Resource = value;
                    return this;
                }
                public SpawnOptions SetPrefab(GameObject prefab)
                {
                    if (NetworkAPI.SyncedPrefabs.TryGet(prefab, out var value) is false)
                    {
                        NetworkLog.Error($"prefab {prefab} not Registerd as Sync Prefab");
                        return this;
                    }

                    ResourceAssigned = true;
                    Resource = value;
                    return this;
                }

                public SpawnOptions SetAuthority(NetworkEntityAuthorityMode mode)
                {
                    if (mode is NetworkEntityAuthorityMode.Authoritative && Room.Clients.Local.IsMaster is false)
                    {
                        NetworkLog.Error($"Can Only Spawn Items with {NetworkEntityAuthorityMode.Authoritative} Authority if Master Client");
                        return this;
                    }

                    Authority = mode;
                    return this;
                }

                public SpawnOptions SetLifetime(NetworkEntityLifetimeMode mode)
                {
                    Lifetime = mode;
                    return this;
                }

                public SpawnOptions SetScene(NetworkSceneID id)
                {
                    if (Room.Scenes.TryGet(id, out var instance) is false)
                    {
                        NetworkLog.Error($"Can't Spawn Entity on Scene {id.Value}, Scene is not Loaded");
                        return this;
                    }

                    Scene = instance.Component;
                    return this;
                }

                bool ResourceAssigned;
                bool Validate()
                {
                    if (Room.Clients.Local.SpawnAllowance is 0)
                    {
                        NetworkLog.Error($"Client's Spawn Allowance Exceeded, Need to Wait for More");
                        return false;
                    }

                    if (ResourceAssigned is false)
                    {
                        NetworkLog.Error($"No Resource/Prefab Specified");
                        return false;
                    }

                    if (Scene is null)
                    {
                        if (Room.Scenes.Active is null)
                        {
                            NetworkLog.Error("Can't Spawn Objects Untill a Network Scene is Loaded");
                            return false;
                        }

                        Scene = Room.Scenes.Active.Component;
                    }

                    return true;
                }

                public NetworkEntity Send()
                {
                    if (Validate() is false)
                        return default;

                    Token = Room.Clients.Local.RemoveSpawnToken();

                    var request = new SpawnEntityRequest(Token, Resource, Authority, Lifetime, Scene.ID);
                    Room.Transport.SendData(in request);

                    return Room.Entities.SpawnLocal(this);
                }

                public SpawnOptions(RoomInstance Room)
                {
                    this.Room = Room;

                    Token = default;
                    Resource = default;
                    Authority = NetworkEntityAuthorityMode.Explicit;
                    Lifetime = NetworkEntityLifetimeMode.Scene;
                    Scene = default;

                    ResourceAssigned = false;
                }
            }

            void SpawnResponseHandler(ref SpawnEntityResponse message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                Room.Clients.Local.AddSpawnToken(message.ReplacementToken);

                if (TryGet(message.SourceToken, out var entity) is false)
                {
                    NetworkLog.Error($"No Network Entity with ID {message.SourceToken} Found");
                    return;
                }

                entity.Replicate();
            }
            void SpawnCommandHandler(ref SpawnEntityCommand message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                NetworkSerializer.ReadValue(reader, out NetworkEntityDefinition definition);

                var response = RetrieveInstance(definition);
                if (response.IsError)
                {
                    NetworkLog.Error(response.Error);
                    Room.Shutdown();
                    return;
                }

                var instance = response.Value;

                instance.AssignRoom(Room);
                instance.AssignDefinition(definition);

                instance.Spawn();
                instance.Replicate();

                Register(instance);
            }

            internal void SpawnBuffered(NetPacketReader reader)
            {
                NetworkSerializer.ReadValue(reader, out NetworkEntityDefinition definition);

                var response = RetrieveInstance(definition);
                if (response.IsError)
                {
                    NetworkLog.Error(response.Error);
                    Room.Shutdown();
                    return;
                }

                var instance = response.Value;

                instance.AssignRoom(Room);
                instance.AssignDefinition(definition);

                instance.Spawn();
                instance.Replicate();

                Register(instance);
            }

            NetworkEntity SpawnLocal(SpawnOptions options)
            {
                var definition = new NetworkEntityDefinition(options.Token, NetworkEntityOrigin.Prefab, options.Resource, options.Authority, options.Lifetime, Room.Clients.Local.ID, options.Scene.ID);

                var response = InstantiatePrefab(options.Resource);
                if (response.IsError)
                {
                    NetworkLog.Error(response.Error);
                    Room.Shutdown();
                    return default;
                }

                var instance = response.Value;

                instance.AssignRoom(Room);
                instance.AssignDefinition(definition);

                instance.Spawn();

                Register(instance);

                return instance;
            }

            Response<NetworkEntity, WslaError> RetrieveInstance(NetworkEntityDefinition definition)
            {
                switch (definition.Origin)
                {
                    case NetworkEntityOrigin.Prefab:
                        return InstantiatePrefab(definition.Resource);

                    case NetworkEntityOrigin.Scene:
                    {
                        if (Room.Scenes.TryGet(definition.Scene, out var scene) is false)
                            return WslaError.From(WslaErrorCode.NoSceneFoundForEntity);

                        if (scene.Component.TryGet(definition.Resource, out var entity) is false)
                            return WslaError.From(WslaErrorCode.NoEntityFoundInScene);

                        return entity;
                    }

                    default:
                        throw new NotImplementedException();
                }
            }

            Response<NetworkEntity, WslaError> InstantiatePrefab(NetworkEntityResource resource)
            {
                if (NetworkAPI.SyncedPrefabs.TryGet(resource, out var prefab) is false)
                {
                    NetworkLog.Error($"No Synced Prefab found With ID {prefab}");
                    return WslaError.From(WslaErrorCode.SyncedPrefabNotFound);
                }

                return InstantiatePrefab(prefab);
            }
            Response<NetworkEntity, WslaError> InstantiatePrefab(GameObject prefab)
            {
                var gameObject = GameObject.Instantiate(prefab);

                if (gameObject.TryGetComponent<NetworkEntity>(out var entity) is false)
                {
                    NetworkLog.Error($"Synced Prefab {prefab} Has no NetworkEntity Component");
                    return WslaError.From(WslaErrorCode.SyncedPrefabWithoutNetworkEntity);
                }

                return entity;
            }

            void Register(NetworkEntity entity)
            {
                Dictionary.Add(entity.ID, entity);
            }

            void Unregister(NetworkEntityID id)
            {
                if (Dictionary.TryGetValue(id, out var entity) is false)
                {
                    NetworkLog.Error($"No Network Entity with ID {id} Found");
                    return;
                }

                Unregister(entity);
            }
            void Unregister(NetworkEntity entity)
            {
                Dictionary.Remove(entity.ID);
            }

            readonly RoomInstance Room;
            public EntitiesProperty(RoomInstance Room)
            {
                this.Room = Room;

                Dictionary = new Dictionary<NetworkEntityID, NetworkEntity>(40);

                Transport.Dispatcher.Register<SpawnEntityCommand>(SpawnCommandHandler);
                Transport.Dispatcher.Register<SpawnEntityResponse>(SpawnResponseHandler);
            }
        }

        public ScenesProperty Scenes { get; }
        public class ScenesProperty
        {
            public List<Constructor> Collection { get; }
            public class Constructor
            {
                public NetworkSceneID ID { get; }
                public int BuildIndex => ID.Value;

                public NetworkScene Component { get; private set; }

                public AsyncOperation Operation { get; private set; }

                internal async UniTask Load(LoadSceneMode mode)
                {
                    Operation = SceneManager.LoadSceneAsync(BuildIndex, mode);
                    await Operation;

                    while (IsRegistered is false)
                        await UniTask.NextFrame();
                }

                public bool IsRegistered => Component != null;
                public void Register(NetworkScene Component)
                {
                    this.Component = Component;

                    OnRegister?.Invoke();
                }
                public event Action OnRegister;

                internal void Spawn() => Component.Spawn();
                internal void Despawn() => Component.Despawn();

                public Constructor(NetworkSceneID ID)
                {
                    this.ID = ID;
                }
            }
            public bool TryGet(NetworkSceneID id, out Constructor target)
            {
                for (int i = 0; i < Collection.Count; i++)
                {
                    target = Collection[i];
                    if (target.ID == id)
                        return true;
                }

                target = default;
                return false;
            }

            public Constructor Active
            {
                get
                {
                    if (Collection.Count is 0)
                        return default;

                    return Collection[0];
                }
            }

            internal UniTask ReadState(NetPacketReader reader, ClientConnectionResponse message)
            {
                var list = ChangeOptions.ListPool.Take();

                for (int i = 0; i < message.Scenes; i++)
                {
                    NetworkSerializer.ReadValue(reader, out NetworkSceneID id);
                    list.Add(id);
                }

                return ChangeProcedure(NetworkSceneLoadMode.Single, list);
            }

            public ChangeOptions Load(NetworkSceneID scene, NetworkSceneLoadMode mode) => new ChangeOptions(Room, mode, scene);
            public struct ChangeOptions
            {
                readonly RoomInstance Room;
                internal readonly NetworkSceneLoadMode LoadMode;

                internal List<NetworkSceneID> List;
                public int Count => List.Count;
                public const int Capacity = ChangeScenesRequest.Capacity;

                public NetworkSceneID this[int index]
                {
                    get
                    {
                        if (index >= Capacity || index < 0)
                            throw new ArgumentOutOfRangeException(nameof(index), index, $"Range is (0 to {Capacity})");

                        return List[index];
                    }
                }

                public ChangeOptions Add(NetworkSceneID scene)
                {
                    if (Count >= Capacity)
                        throw new InvalidOperationException($"Can Only Load upto {Capacity} Scenes in One Request");

                    List.Add(scene);

                    return this;
                }

                public UniTask Send()
                {
                    //Replicate
                    {
                        var message = new ChangeScenesRequest(LoadMode, List);
                        Room.Transport.SendData(message);
                    }

                    //Local
                    return Room.Scenes.AuthorChange(ref this);
                }

                public ChangeOptions(RoomInstance Room, NetworkSceneLoadMode LoadMode, NetworkSceneID Scene)
                {
                    this.Room = Room;
                    this.LoadMode = LoadMode;

                    List = ListPool.Take();
                    List.Add(Scene);
                }

                internal static SingleInstancePool<List<NetworkSceneID>> ListPool = new(new(Capacity), x => x.Clear());
            }

            void ChangeCommandHandler(ref ChangeScenesCommand message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                Procedure(message).Forget();
                async UniTask Procedure(ChangeScenesCommand message)
                {
                    Room.Transport.Listener.Pause();
                    await ChangeProcedure(message.LoadMode, message.Scenes);
                    Room.Transport.Listener.Resume();
                }
            }

            UniTask AuthorChange(ref ChangeOptions options) => Room.Scenes.ChangeProcedure(options.LoadMode, options.List);

            public async UniTask ChangeProcedure(NetworkSceneLoadMode mode, List<NetworkSceneID> ids)
            {
                if (mode is NetworkSceneLoadMode.Single)
                {
                    for (int i = 0; i < Collection.Count; i++)
                        Collection[i].Despawn();

                    Collection.Clear();
                }

                for (int i = 0; i < ids.Count; i++)
                {
                    var instance = new Constructor(ids[i]);

                    Collection.Add(instance);

                    var choice = (i is 0) ? ConvertLoadMode(mode) : LoadSceneMode.Additive;
                    await instance.Load(choice);
                }
            }

            internal void Register(NetworkScene scene)
            {
                if (TryGet(scene.ID, out var constructor) is false)
                {
                    Debug.LogError($"NetworkScene Registered Without a Loading Operation");
                    return;
                }

                constructor.Register(scene);
            }

            readonly RoomInstance Room;
            public ScenesProperty(RoomInstance Room)
            {
                this.Room = Room;

                Collection = new List<Constructor>(1);

                Room.Transport.Dispatcher.Register<ChangeScenesCommand>(ChangeCommandHandler);
            }

            static LoadSceneMode ConvertLoadMode(NetworkSceneLoadMode mode) => (LoadSceneMode)mode;
            static NetworkSceneLoadMode ConvertLoadMode(LoadSceneMode mode) => (NetworkSceneLoadMode)mode;
        }

        public async UniTask<Response<WslaError>> Start(ClientConnectionRequest request)
        {
            //Start Transport
            {
                var response = await Transport.Start(request);
                if (response.IsError)
                    return response.Error;
            }

            return true;
        }
        public async UniTask Stop()
        {
            Transport.Stop();
        }

        public void Shutdown()
        {
            //TODO: Implement
            throw new NotImplementedException();
        }

        public RoomInstance(IPAddress address, ushort port)
        {
            Transport = new TransportProperty(this, address, port);
            Clients = new ClientsProperty(this);
            Entities = new EntitiesProperty(this);
            Scenes = new ScenesProperty(this);
        }
    }

    public abstract class NetworkClient
    {
        public NetworkClientID ID { get; }
        public string Username { get; private set; }

        public bool IsLocal => this is LocalNetworkClient;
        public bool IsMaster => Room.Clients.Master == this;

        public RoomInstance Room { get; }

        public static NetworkClientID ReadID(NetPacketReader reader)
        {
            return NetworkSerializer.ReadValue<NetworkClientID>(reader);
        }
        public virtual void ReadState(NetPacketReader reader)
        {
            Username = NetworkSerializer.ReadValue<string>(reader);
        }

        public override string ToString() => $"(ID: {ID}, Username: {Username})";

        public NetworkClient(RoomInstance Room, NetworkClientID ID)
        {
            this.Room = Room;
            this.ID = ID;
        }
    }

    public class RemoteNetworkClient : NetworkClient
    {
        public static RemoteNetworkClient ReadInstance(RoomInstance room, ref NetPacketReader reader)
        {
            var id = ReadID(reader);

            var client = new RemoteNetworkClient(room, id);

            client.ReadState(reader);

            return client;
        }

        public RemoteNetworkClient(RoomInstance Room, NetworkClientID ID) : base(Room, ID) { }
    }
    public class LocalNetworkClient : NetworkClient
    {
        #region Spawn Tokens
        public Queue<NetworkEntityID> SpawnTokens { get; }
        public byte SpawnAllowance => (byte)SpawnTokens.Count;

        public void AddSpawnToken(NetworkEntityID id)
        {
            SpawnTokens.Enqueue(id);
        }
        public NetworkEntityID RemoveSpawnToken()
        {
            return SpawnTokens.Dequeue();
        }

        internal void ReadSpawnTokens(NetPacketReader reader, ClientConnectionResponse message)
        {
            for (int i = 0; i < message.SpawnTokens; i++)
            {
                var token = NetworkSerializer.ReadValue<NetworkEntityID>(reader);
                AddSpawnToken(token);
            }
        }
        #endregion

        public LocalNetworkClient(RoomInstance Room, NetworkClientID ID, int SpawnTokenCapacity) : base(Room, ID)
        {
            SpawnTokens = new Queue<NetworkEntityID>(SpawnTokenCapacity);
        }
    }
}
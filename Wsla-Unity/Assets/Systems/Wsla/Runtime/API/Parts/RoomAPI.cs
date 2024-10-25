using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using Cysharp.Threading.Tasks;

using LiteNetLib;
using LiteNetLib.Utils;

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
                    var id = NetworkTypeSerializationResolver.ReadValue(ref reader);

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
                        NetworkSerializer.ReadValue(ref reader, out T data);
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

                    NetworkSerializer.WriteValue(in request, ref packet);

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

            public void Send<[NetworkSerializationMarker] T>(in T data, byte channel = 0, DeliveryMethod delivery = DeliveryMethod.ReliableOrdered)
            {
                var writer = PacketWriter.Take();

                NetworkSerializer.WriteHeader(data, ref writer);

                Send(writer, channel, delivery);
            }
            public void Send(in NetDataWriter writer, byte channel = 0, DeliveryMethod delivery = DeliveryMethod.ReliableOrdered)
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

                    //Sync Spawn Tokens
                    Local.ReadSpawnTokens(ref reader, message);

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
                    var id = NetworkClient.ReadID(ref reader);

                    var isLocal = id == message.ID;

                    NetworkClient client;

                    if (isLocal)
                        client = Local = new LocalNetworkClient(id, message.SpawnTokens);
                    else
                        client = new RemoteNetworkClient(id);

                    client.ReadState(ref reader);

                    Register(client);
                }
            }

            void ClientConnectHandler(ref ClientConnectMessage message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                var client = RemoteNetworkClient.ReadInstance(ref reader);

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

                bool ResourceAssigned;
                internal NetworkEntityResource Resource;

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

                    return true;
                }

                public NetworkEntity Send()
                {
                    if (Validate() is false)
                        return default;

                    Token = Room.Clients.Local.RemoveSpawnToken();

                    var request = new SpawnEntityRequest(Token, Resource);
                    Room.Transport.Send(in request);

                    return Room.Entities.SpawnLocal(this);
                }

                public SpawnOptions(RoomInstance Room)
                {
                    this.Room = Room;

                    Token = default;

                    ResourceAssigned = false;
                    Resource = default;
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
                NetworkEntity.ReadProperties(reader, out var source, out var resource, out var id);

                var response = RetrieveInstance(source, resource);
                if (response.IsError)
                {
                    NetworkLog.Error(response.Error);
                    Room.Shutdown();
                    return;
                }

                var instance = response.Value;

                instance.Set(Room);
                instance.SetProperties(id, source, resource);

                instance.Spawn();
                instance.Replicate();

                Register(instance);
            }

            internal void SpawnBuffered(NetPacketReader reader)
            {
                NetworkEntity.ReadProperties(reader, out var source, out var resource, out var id);

                var response = RetrieveInstance(source, resource);
                if (response.IsError)
                {
                    NetworkLog.Error(response.Error);
                    Room.Shutdown();
                    return;
                }

                var instance = response.Value;

                instance.Set(Room);
                instance.SetProperties(id, source, resource);

                instance.Spawn();
                instance.Replicate();

                Register(instance);
            }

            NetworkEntity SpawnLocal(SpawnOptions options)
            {
                var response = InstantiatePrefab(options.Resource);
                if (response.IsError)
                {
                    NetworkLog.Error(response.Error);
                    Room.Shutdown();
                    return default;
                }

                var instance = response.Value;

                instance.Set(Room);
                instance.SetProperties(options.Token, NetworkEntitySource.Prefab, options.Resource);

                instance.Spawn();

                Register(instance);

                return instance;
            }

            Response<NetworkEntity, WslaError> RetrieveInstance(NetworkEntitySource source, NetworkEntityResource resource)
            {
                switch (source)
                {
                    case NetworkEntitySource.Prefab:
                        return InstantiatePrefab(resource);

                    case NetworkEntitySource.Scene:
                        throw new NotImplementedException();

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
                }

                internal void Unload()
                {
                    throw new NotImplementedException();
                }

                public bool IsRegistered => Component != null;
                public void Register(NetworkScene Component)
                {
                    this.Component = Component;

                    OnRegister?.Invoke();
                }
                public event Action OnRegister;

                public Constructor(NetworkSceneID ID)
                {
                    this.ID = ID;
                }
            }
            public bool TryFind(NetworkSceneID id, out Constructor target)
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

            internal UniTask ReadState(NetPacketReader reader, ClientConnectionResponse message)
            {
                var list = ChangeOptions.List;

                list.Clear();

                for (int i = 0; i < message.Scenes; i++)
                {
                    var id = NetworkSerializer.ReadValue<NetworkSceneID, NetPacketReader>(ref reader);
                    list.Add(id);
                }

                return ChangeProcedure(NetworkSceneLoadMode.Single, list);
            }

            public ChangeOptions Load(NetworkSceneID scene, NetworkSceneLoadMode mode) => new ChangeOptions(Room, mode, scene);
            public ref struct ChangeOptions
            {
                readonly RoomInstance Room;
                internal readonly NetworkSceneLoadMode LoadMode;

                internal static List<NetworkSceneID> List = new List<NetworkSceneID>(Capacity);
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
                        Room.Transport.Send(message);
                    }

                    //Local
                    {
                        return Room.Scenes.ChangeProcedure(LoadMode, List);
                    }
                }

                public ChangeOptions(RoomInstance Room, NetworkSceneLoadMode LoadMode, NetworkSceneID Scene)
                {
                    this.Room = Room;
                    this.LoadMode = LoadMode;

                    List.Clear();

                    List.Add(Scene);
                }
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

            public async UniTask ChangeProcedure(NetworkSceneLoadMode mode, List<NetworkSceneID> ids)
            {
                if (mode is NetworkSceneLoadMode.Single)
                {
                    for (int i = 0; i < Collection.Count; i++)
                        Collection[i].Unload();

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
                if (TryFind(scene.ID, out var constructor) is false)
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
        public NetworkClientID ID { get; private set; }
        public string Username { get; private set; }

        public bool IsLocal => this is LocalNetworkClient;

        public static NetworkClientID ReadID(ref NetPacketReader reader)
        {
            return NetworkSerializer.ReadValue<NetworkClientID, NetPacketReader>(ref reader);
        }
        public virtual void ReadState(ref NetPacketReader reader)
        {
            Username = NetworkSerializer.ReadValue<string, NetPacketReader>(ref reader);
        }

        public override string ToString() => $"(ID: {ID}, Username: {Username})";

        public NetworkClient(NetworkClientID id)
        {
            this.ID = id;
        }
    }

    public class RemoteNetworkClient : NetworkClient
    {
        public static RemoteNetworkClient ReadInstance(ref NetPacketReader reader)
        {
            var id = ReadID(ref reader);

            var client = new RemoteNetworkClient(id);

            client.ReadState(ref reader);

            return client;
        }

        public RemoteNetworkClient(NetworkClientID id) : base(id) { }
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

        internal void ReadSpawnTokens(ref NetPacketReader reader, ClientConnectionResponse message)
        {
            for (int i = 0; i < message.SpawnTokens; i++)
            {
                var token = NetworkSerializer.ReadValue<NetworkEntityID, NetPacketReader>(ref reader);
                AddSpawnToken(token);
            }
        }
        #endregion

        public LocalNetworkClient(NetworkClientID id, int spawnTokenCapacity) : base(id)
        {
            SpawnTokens = new Queue<NetworkEntityID>(spawnTokenCapacity);
        }
    }
}
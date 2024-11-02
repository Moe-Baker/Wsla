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

using Wsla.Serialization;

namespace Wsla.Unity
{
    [Serializable]
    public class RoomAPI : NetworkAPI.Property
    {
        public RoomInstance Current { get; private set; }

        public async UniTask<Response<RoomInstance, WslaError>> Connect(IPAddress address, ushort port, ClientConnectionRequest request)
        {
            var target = new RoomInstance(address, port);

            var response = await target.Start(request);

            if (response.IsError)
                return response.Error;

            Current = target;

            return target;
        }
    }

    [Serializable]
    public class RoomInstance
    {
        static NetworkAPI NetworkAPI => NetworkAPI.Instance;

        public PoolsProperty Pools { get; }
        public struct PoolsProperty
        {
            public SingleInstancePool<NetDataWriter> SinglePackerWriter { get; private set; }
            public SingleInstancePool<List<NetworkEntity>> EntityList { get; private set; }

            public static PoolsProperty Create() => new PoolsProperty()
            {
                SinglePackerWriter = new(new(true, 2048), x => x.SetPosition(0)),
                EntityList = new(new(100), x => x.Clear()),
            };
        }

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

                    if (reader.KeepAlive is false)
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
                    var packet = Room.Pools.SinglePackerWriter.Take();

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
                var writer = Room.Pools.SinglePackerWriter.Take();

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
                Manager.DisconnectTimeout = (int)Constants.Timeout.TotalMilliseconds;
                Manager.AutoRecycle = false;

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

            void ConnectionResponseHandler(ref ClientConnectionResponse message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                reader.KeepAlive = true;

                Room.ReadState(message, reader).Forget();
            }

            internal void ReadState(NetPacketReader reader, ClientConnectionResponse message)
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

                //Check if we didn't recieve our local client
                if (Local is null)
                    throw new InvalidOperationException("No Local Client Received in Response");

                //Check if we didn't recieve the master client
                if (Master is null)
                    throw new InvalidOperationException("No Master Client Received in Response");
            }

            void ConnectHandler(ref ClientConnectMessage message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                var client = RemoteNetworkClient.ReadInstance(Room, ref reader);

                Register(client);
            }
            void DisconnectHandler(ref ClientDisconnectMessage message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                Unregister(message.ID);
            }

            public delegate void MasterChangeDelegate(NetworkClient client);
            public event MasterChangeDelegate OnMasterChange;
            void ChangeMasterHandler(ref ChangeMasterClientCommand message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                var previous = Master;

                if (TryGet(message.MasterID, out var current) is false)
                {
                    NetworkLog.Error($"No Client with ID {message.MasterID} Found");
                    Room.Shutdown();
                    return;
                }

                Master = current;

                NetworkLog.Info($"Master Client Changed to {Master}");

                foreach (var entity in previous.Entities)
                    Room.Entities.Transfer(entity, current);

                OnMasterChange?.Invoke(Master);
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

                Collection = new ExpandArray<NetworkClient>(10, NetworkClientID.Max.Value, 10);

                Transport.Dispatcher.Register<ClientConnectionResponse>(ConnectionResponseHandler);

                Transport.Dispatcher.Register<ClientConnectMessage>(ConnectHandler);
                Transport.Dispatcher.Register<ClientDisconnectMessage>(DisconnectHandler);

                Transport.Dispatcher.Register<ChangeMasterClientCommand>(ChangeMasterHandler);
            }
        }

        public EntitiesProperty Entities { get; }
        public class EntitiesProperty
        {
            public Dictionary<NetworkEntityID, NetworkEntity> Dictionary { get; }
            public int Count => Dictionary.Count;

            public bool TryGet(NetworkEntityID id, out NetworkEntity entity) => Dictionary.TryGetValue(id, out entity);

            TransportProperty Transport => Room.Transport;

            internal void ReadState(NetPacketReader reader, ClientConnectionResponse message, List<NetworkEntity> list)
            {
                for (int i = 0; i < message.Entities; i++)
                {
                    var definition = NetworkSerializer.ReadValue<NetworkEntityDefinition>(reader);
                    var instance = Assimilate(definition);
                    list.Add(instance);
                }
            }

            public SpawnOptions Spawn() => new SpawnOptions(Room);
            public ref struct SpawnOptions
            {
                readonly RoomInstance Room;

                internal NetworkEntityID Token;
                internal NetworkEntityResource Resource;
                internal NetworkEntityAuthorityMode Authority;

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

                    return true;
                }

                public NetworkEntity Send()
                {
                    if (Validate() is false)
                        return default;

                    Token = Room.Clients.Local.RemoveSpawnToken();

                    var request = new SpawnPrefabEntityRequest(Token, Resource, Authority, Room.Scene.Version);
                    Room.Transport.SendData(in request);

                    return Room.Entities.SpawnLocal(this);
                }

                public SpawnOptions(RoomInstance Room)
                {
                    this.Room = Room;

                    Token = default;
                    Resource = default;
                    Authority = NetworkEntityAuthorityMode.Explicit;

                    ResourceAssigned = false;
                }
            }

            void SpawnPrefabResponseHandler(ref SpawnPrefabEntityResponse message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                Room.Clients.Local.AddSpawnToken(message.ReplacementToken);

                if (TryGet(message.SourceToken, out var entity) is false)
                {
                    NetworkLog.Error($"No Network Entity with ID {message.SourceToken} Found");
                    return;
                }

                entity.Replicate();
            }
            void SpawnPrefabCommandHandler(ref SpawnPrefabEntityCommand message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                var definition = new NetworkEntityDefinition(message.ID, NetworkEntityOrigin.Prefab, message.Resource, message.Authority, message.Owner);

                var instance = Assimilate(definition);

                instance.Spawn();
                instance.Replicate();
            }

            NetworkEntity Assimilate(NetworkEntityDefinition definition)
            {
                var instance = RetrieveInstance(definition);

                instance.Assign(Room, definition);

                Register(instance);

                return instance;
            }

            NetworkEntity SpawnLocal(SpawnOptions options)
            {
                var definition = new NetworkEntityDefinition(options.Token, NetworkEntityOrigin.Prefab, options.Resource, options.Authority, Room.Clients.Local.ID);

                var instance = Assimilate(definition);

                instance.Spawn();

                return instance;
            }

            NetworkEntity RetrieveInstance(NetworkEntityDefinition definition)
            {
                switch (definition.Origin)
                {
                    case NetworkEntityOrigin.Prefab:
                        return InstantiatePrefab(definition.Resource);

                    case NetworkEntityOrigin.Scene:
                        return FindInScene(definition.Resource);

                    default:
                        throw new NotImplementedException();
                }
            }
            NetworkEntity FindInScene(NetworkEntityResource resource)
            {
                if (Room.Scene.Component.TryGetLocal(resource, out var entity) is false)
                    throw new ArgumentException($"No Resource {resource} found in Scene {Room.Scene.Component}");

                return entity;
            }
            NetworkEntity InstantiatePrefab(NetworkEntityResource resource)
            {
                if (NetworkAPI.SyncedPrefabs.TryGet(resource, out var prefab) is false)
                    throw new ArgumentException($"No Synced Prefab found With ID {prefab}");

                return InstantiatePrefab(prefab);
            }
            NetworkEntity InstantiatePrefab(GameObject prefab)
            {
                var gameObject = GameObject.Instantiate(prefab);

                if (gameObject.TryGetComponent<NetworkEntity>(out var entity) is false)
                    throw new ArgumentException($"Synced Prefab {prefab} Has no NetworkEntity Component");

                return entity;
            }

            void Register(NetworkEntity entity)
            {
                Dictionary.Add(entity.ID, entity);

                entity.Owner.RegisterEntity(entity);
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

            internal void Transfer(NetworkEntity entity, NetworkClient to)
            {
                var from = entity.Owner;

                from.UnregisterEntity(entity);
                to.RegisterEntity(entity);

                entity.TransferOwner(to);
            }

            readonly RoomInstance Room;
            public EntitiesProperty(RoomInstance Room)
            {
                this.Room = Room;

                Dictionary = new Dictionary<NetworkEntityID, NetworkEntity>(40);

                Transport.Dispatcher.Register<SpawnPrefabEntityCommand>(SpawnPrefabCommandHandler);
                Transport.Dispatcher.Register<SpawnPrefabEntityResponse>(SpawnPrefabResponseHandler);
            }
        }

        public RpcsProperty RPCs { get; }
        public class RpcsProperty
        {
            TransportProperty Transport => Room.Transport;

            void CommandHandler(ref NetworkRpcCommand message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                if (Get(ref message.Parameters, out var bind) is false)
                {
                    Debug.LogError($"No Network RPC Found for Parameters of {message.Parameters}");
                    return;
                }

                var info = RpcInfo.From(Room, ref message, channel, delivery);

                bind.Invoke(reader, info);
            }

            bool Get(ref NetworkRpcParameters parameters, out BaseRpcBind bind)
            {
                if (Room.Entities.TryGet(parameters.Entity, out var entity) is false)
                {
                    NetworkLog.Error($"No Network Entity with ID {parameters.Entity} Found");
                    bind = default;
                    return false;
                }

                return Get(entity, parameters.Behaviour, parameters.RPC, out bind);
            }
            bool Get(NetworkEntity entity, NetworkBehaviourID behaviourID, NetworkRpcID rpcID, out BaseRpcBind bind)
            {
                if (entity.Behaviours.TryGet(behaviourID, out var behaviour) is false)
                {
                    NetworkLog.Error($"No Network Behaviour with ID {behaviourID} Found on {entity}");
                    bind = default;
                    return false;
                }

                if (behaviour.RPC.TryGet(rpcID, out bind) is false)
                {
                    NetworkLog.Error($"No Network RPC with ID {rpcID} Found on {entity} on Behaviour {behaviour}");
                    bind = default;
                    return false;
                }

                return true;
            }

            internal void ReadState(NetPacketReader reader)
            {
                var total = NetworkSerializer.ReadValue<ushort>(reader);

                for (int x = 0; x < total; x++)
                {
                    var entityID = NetworkSerializer.ReadValue<NetworkEntityID>(reader);
                    if (Room.Entities.TryGet(entityID, out var entity) is false)
                        throw new InvalidOperationException($"No Entity found With ID {entityID}");

                    var count = NetworkSerializer.ReadValue<ushort>(reader);

                    for (int y = 0; y < count; y++)
                    {
                        var behaviourID = NetworkSerializer.ReadValue<NetworkBehaviourID>(reader);
                        var rpcID = NetworkSerializer.ReadValue<NetworkRpcID>(reader);

                        if (Get(entity, behaviourID, rpcID, out var bind))
                        {
                            var info = RpcInfo.Buffered();
                            bind.Invoke(reader, info);
                        }
                    }
                }
            }

            readonly RoomInstance Room;
            public RpcsProperty(RoomInstance Room)
            {
                this.Room = Room;

                Transport.Dispatcher.Register<NetworkRpcCommand>(CommandHandler);
            }
        }

        public VariablesProperty Variables { get; }
        public class VariablesProperty
        {
            TransportProperty Transport => Room.Transport;

            void CommandHandler(ref NetworkVariableCommand message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                if (Get(ref message.Parameters, out var bind) is false)
                {
                    Debug.LogError($"No Network RPC Found for Parameters of {message.Parameters}");
                    return;
                }

                var info = NetworkVariableInfo.From(Room, ref message, channel, delivery);

                bind.Set(reader, info);
            }

            bool Get(ref NetworkVariableParameters parameters, out NetworkVariable variable)
            {
                if (Room.Entities.TryGet(parameters.Entity, out var entity) is false)
                {
                    NetworkLog.Error($"No Network Entity with ID {parameters.Entity} Found");
                    variable = default;
                    return false;
                }

                return Get(entity, parameters.Behaviour, parameters.Variable, out variable);
            }
            bool Get(NetworkEntity entity, NetworkBehaviourID behaviourID, NetworkVariableID variableID, out NetworkVariable variable)
            {
                if (entity.Behaviours.TryGet(behaviourID, out var behaviour) is false)
                {
                    NetworkLog.Error($"No Network Behaviour with ID {behaviourID} Found on {entity}");
                    variable = default;
                    return false;
                }

                if (behaviour.Variables.TryGet(variableID, out variable) is false)
                {
                    NetworkLog.Error($"No Network Variable with ID {variableID} Found on {entity} on Behaviour {behaviour}");
                    variable = default;
                    return false;
                }

                return true;
            }

            internal void ReadState(NetPacketReader reader)
            {
                var total = NetworkSerializer.ReadValue<ushort>(reader);

                for (int x = 0; x < total; x++)
                {
                    var entityID = NetworkSerializer.ReadValue<NetworkEntityID>(reader);
                    if (Room.Entities.TryGet(entityID, out var entity) is false)
                        throw new NotImplementedException();

                    var count = NetworkSerializer.ReadValue<ushort>(reader);

                    for (int y = 0; y < count; y++)
                    {
                        var behaviourID = NetworkSerializer.ReadValue<NetworkBehaviourID>(reader);
                        var variableID = NetworkSerializer.ReadValue<NetworkVariableID>(reader);

                        if (Get(entity, behaviourID, variableID, out var variable))
                        {
                            var info = NetworkVariableInfo.Buffered();
                            variable.Set(reader, info);
                        }
                    }
                }
            }

            readonly RoomInstance Room;
            public VariablesProperty(RoomInstance Room)
            {
                this.Room = Room;

                Transport.Dispatcher.Register<NetworkVariableCommand>(CommandHandler);
            }
        }

        public SceneProperty Scene { get; }
        public class SceneProperty
        {
            public NetworkSceneID ID { get; private set; }
            public int BuildIndex => ID.Value;

            public NetworkSceneVersion Version { get; private set; }

            public NetworkScene Component { get; private set; }

            public bool IsLoaded => Component != null;

            AsyncOperation Operation;

            public bool IsRegistered => Component != null;
            public void Register(NetworkScene Component)
            {
                this.Component = Component;

                OnRegister?.Invoke();
            }
            public event Action OnRegister;

            internal UniTask ReadState(NetPacketReader reader, ClientConnectionResponse message)
            {
                ID = NetworkSerializer.ReadValue<NetworkSceneID>(reader);
                Version = NetworkSerializer.ReadValue<NetworkSceneVersion>(reader);

                return ChangeProcedure(ID, Version);
            }

            public void Change(NetworkSceneID target)
            {
                if (Room.Clients.Local.IsMaster is false)
                    throw new InvalidOperationException($"Only the Master Client can Change Scenes");

                var request = new ChangeSceneRequest(target);
                Room.Transport.SendData(in request);
            }

            void ChangeCommandHandler(ref ChangeSceneCommand message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                Procedure(message).Forget();
                async UniTask Procedure(ChangeSceneCommand message)
                {
                    Room.Transport.Listener.Pause();
                    {
                        await ChangeProcedure(message.ID, message.Version);
                    }
                    Room.Transport.Listener.Resume();
                }
            }

            public async UniTask ChangeProcedure(NetworkSceneID ID, NetworkSceneVersion Version)
            {
                this.ID = ID;
                this.Version = Version;

                if (IsLoaded)
                    Component.Despawn();

                Operation = SceneManager.LoadSceneAsync(BuildIndex);
                await Operation;

                while (IsRegistered is false)
                    await UniTask.NextFrame();

                if (Room.Clients.Local.IsMaster)
                {
                    var writer = Room.Pools.SinglePackerWriter.Take();

                    var message = new SpawnScenenRequest();
                    NetworkSerializer.WriteHeader(in message, writer);

                    Component.WriteRequest(writer);

                    Room.Transport.SendWriter(writer);
                }
            }

            void SpawnSceneCommandHandler(ref SpawnSceneCommand message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                var component = Room.Scene.Component;
                var count = Room.Scene.Component.Locals.Length;

                for (byte i = 0; i < count; i++)
                {
                    var entity = component.Locals[i];

                    var id = NetworkSerializer.ReadValue<NetworkEntityID>(reader);
                    var resource = new NetworkEntityResource(i);
                    var authority = component.Locals[i].Authority;
                    var ownerID = (authority is NetworkEntityAuthorityMode.Authoritative) ? Room.Clients.Master.ID : NetworkSerializer.ReadValue<NetworkClientID>(reader);

                    var definition = new NetworkEntityDefinition(id, NetworkEntityOrigin.Scene, resource, authority, ownerID);

                    entity.Assign(Room, definition);

                    entity.Spawn();
                    entity.Replicate();
                }

                Room.Scene.Component.Spawn();
            }

            readonly RoomInstance Room;
            public SceneProperty(RoomInstance Room)
            {
                this.Room = Room;

                Room.Transport.Dispatcher.Register<ChangeSceneCommand>(ChangeCommandHandler);
                Room.Transport.Dispatcher.Register<SpawnSceneCommand>(SpawnSceneCommandHandler);
            }
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

        async UniTask ReadState(ClientConnectionResponse message, NetPacketReader reader)
        {
            Transport.Listener.Pause();
            {
                //Sync Clients
                Clients.ReadState(reader, message);

                //Sync Spawn Tokens
                Clients.Local.ReadSpawnTokens(reader, message);

                //Sync Scenes
                await Scene.ReadState(reader, message);

                var entities = Pools.EntityList.Take();

                //Sync Entities
                Entities.ReadState(reader, message, entities);

                //Sync Variables
                Variables.ReadState(reader);

                //Sync RPCs
                RPCs.ReadState(reader);

                //Spawn && Replicate Entities
                foreach (var entity in entities)
                {
                    entity.Spawn();
                    entity.Replicate();
                }

                //Spawn Scene
                Scene.Component.Spawn();
            }
            Transport.Listener.Resume();

            reader.Recycle();
        }

        public void Shutdown()
        {
            //TODO: Implement
            throw new NotImplementedException();
        }

        public RoomInstance(IPAddress address, ushort port)
        {
            Pools = PoolsProperty.Create();

            Transport = new TransportProperty(this, address, port);
            Clients = new ClientsProperty(this);
            Entities = new EntitiesProperty(this);
            Scene = new SceneProperty(this);
            RPCs = new RpcsProperty(this);
            Variables = new VariablesProperty(this);
        }
    }

    public abstract class NetworkClient
    {
        public NetworkClientID ID { get; }
        public FixedString20 Username { get; private set; }

        public bool IsLocal => this is LocalNetworkClient;
        public bool IsRemote => IsLocal is false;

        public bool IsMaster => Room.Clients.Master == this;

        #region Entities
        public ExpandList<NetworkEntity> Entities { get; }

        public void RegisterEntity(NetworkEntity target)
        {
            target.OwnerRegisteration = Entities.Add(target);
        }
        public void UnregisterEntity(NetworkEntity target)
        {
            Entities.RemoveAt(target.OwnerRegisteration);
        }
        #endregion

        public RoomInstance Room { get; }

        public static NetworkClientID ReadID(NetPacketReader reader)
        {
            return NetworkSerializer.ReadValue<NetworkClientID>(reader);
        }
        public virtual void ReadState(NetPacketReader reader)
        {
            Username = NetworkSerializer.ReadValue<FixedString20>(reader);
        }

        public override string ToString() => $"(ID: {ID}, Username: {Username})";

        public NetworkClient(RoomInstance Room, NetworkClientID ID)
        {
            this.Room = Room;
            this.ID = ID;

            Entities = new(0);
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
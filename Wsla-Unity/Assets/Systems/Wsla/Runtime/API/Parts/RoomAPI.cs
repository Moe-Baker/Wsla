using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

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
        public bool IsConnected { get; private set; }

        public RoomConnectionInfo ConnectionInfo { get; private set; }

        public override void Set(NetworkAPI value)
        {
            base.Set(value);

            IsConnected = false;

            Pools = PoolsProperty.Create();

            API.OnDispose += Dispose;
        }
        void Dispose()
        {
            API.OnDispose -= Dispose;

            if (IsConnected)
                Disconnect();
        }

        public UniTask<WslaResponse<WslaError>> Connect(RoomConnectionInfo info, ClientConnectionRequest request)
        {
            return Connect(info.Address, info.Port, request);
        }
        public async UniTask<WslaResponse<WslaError>> Connect(IPAddress address, ushort port, ClientConnectionRequest request)
        {
            if (IsConnected is true)
                throw new InvalidOperationException($"Client Already Connected to A Room");

            ConnectionInfo = new RoomConnectionInfo(address, port);

            Application.runInBackground = true;

            //Assign Properties
            {
                Transport = new TransportProperty(this);
                Time = new TimeProperty(this);

                Clients = new ClientsProperty(this);
                Clients.Groups = request.Groups;

                Entities = new EntitiesProperty(this);
                Scene = new SceneProperty(this);
                RPCs = new RpcsProperty(this);
                Variables = new VariablesProperty(this);
            }

            //Start Transport
            {
                var response = await Transport.Start(address, port, request);

                if (response.IsError)
                {
                    Disconnect(DisconnectReason.InvalidProtocol);
                    return response.Error;
                }
            }

            IsConnected = true;

            return true;
        }

        public void Disconnect() => Disconnect(DisconnectReason.DisconnectPeerCalled);
        public void Disconnect(DisconnectReason reason)
        {
            var info = new DisconnectInfo()
            {
                Reason = reason,
            };

            Disconnect(info);
        }
        void Disconnect(DisconnectInfo info)
        {
            if (IsConnected is false)
            {
                NetworkLog.Error("Client not Connected to a Room");
                return;
            }

            ConnectionInfo = default;

            //Stop
            {
                Transport?.Stop();
                Time?.Stop();
                Entities?.Stop();

                Transport = default;
                Clients = default;
                Entities = default;
                Scene = default;
                RPCs = default;
                Variables = default;
                Time = default;

                IsConnected = false;
            }

            OnDisconnect?.Invoke(info.Reason);
            OnDisconnect = default;
        }

        public event DisconnectDelegate OnDisconnect;
        public delegate void DisconnectDelegate(DisconnectReason reason);

        internal PoolsProperty Pools;
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

        public TransportProperty Transport { get; private set; }
        public class TransportProperty
        {
            public DispatcherProperty Dispatcher { get; }
            public class DispatcherProperty
            {
                ActionDelegate[] Handlers;
                public delegate void ActionDelegate(NetPacketReader reader, byte channel, DeliveryMethod delivery);

                NetworkAPI API => NetworkAPI.Instance;
                RoomAPI Room => API.Room;

                void ReceiveCallback(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod delivery)
                {
                    var source = BinarySource.From(reader);
                    var id = NetworkTypeSerializationResolver.ReadValue(ref source);

                    var handler = Handlers[id];
                    if (handler is null)
                    {
                        NetworkLog.Error($"No Dispatch Handler Provided for {NetworkTypes.Get(id)} Message");
                        return;
                    }

                    handler(reader, channel, delivery);
                }

                public delegate void TypeDelegate<T>(ref T message, NetPacketReader reader, byte channel, DeliveryMethod delivery);
                public void Register<[NetworkSerializationMarker] T>(TypeDelegate<T> handler)
                {
                    var id = NetworkTypes.Get<T>();

                    Handlers[id] = Surrogate;

                    void Surrogate(NetPacketReader reader, byte channel, DeliveryMethod delivery)
                    {
                        try
                        {
                            NetworkSerializer.ReadValue(reader, out T data);
                            handler(ref data, reader, channel, delivery);
                        }
                        catch (Exception ex)
                        {
                            NetworkLog.Error(ex);
                            Room.Disconnect(DisconnectReason.InvalidProtocol);
                        }
                        finally
                        {
                            reader.Recycle();
                        }
                    }
                }

                public delegate UniTask AsyncTypeDelegate<T>(T message, NetPacketReader reader, byte channel, DeliveryMethod delivery);
                public void RegisterAsync<[NetworkSerializationMarker] T>(AsyncTypeDelegate<T> handler)
                {
                    var id = NetworkTypes.Get<T>();

                    Handlers[id] = Surrogate;

                    async void Surrogate(NetPacketReader reader, byte channel, DeliveryMethod delivery)
                    {
                        try
                        {
                            NetworkSerializer.ReadValue(reader, out T data);
                            await handler(data, reader, channel, delivery);
                        }
                        catch (Exception ex)
                        {
                            NetworkLog.Error(ex);
                            Room.Disconnect(DisconnectReason.InvalidProtocol);
                        }
                        finally
                        {
                            reader.Recycle();
                        }
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

            NetworkAPI API => Room.API;

            internal async UniTask<WslaResponse<WslaError>> Start(IPAddress address, ushort port, ClientConnectionRequest request)
            {
                Manager.Start();

                var operation = new UniTaskCompletionSource<WslaResponse<WslaError>>();

                //Register Hooks
                Listener.OnPeerConnected += Connected;
                void Connected(NetPeer peer) => operation.TrySetResult(true);

                Listener.OnPeerDisconnected += Disconnect;
                void Disconnect(NetPeer peer, DisconnectInfo info) => operation.TrySetResult(WslaError.From(info));

                //Request
                {
                    var endpoint = new IPEndPoint(address, port);

                    var packet = Room.Pools.SinglePackerWriter.Take();

                    Room.Time.Start();
                    request.TimeRequest = Room.Time.CreateRequest();

                    NetworkSerializer.WriteValue(in request, packet);

                    Peer = Manager.Connect(endpoint, packet);
                }

                API.NetworkUpdate.OnEarlyUpdate += PollEvents;
                API.NetworkUpdate.OnLateUpdate += SendData;

                var response = await operation.Task;

                //Clear Hooks
                Listener.OnPeerConnected -= Connected;
                Listener.OnPeerDisconnected -= Disconnect;

                if (response.IsError)
                {
                    Stop();
                    return response.Error;
                }

                Listener.OnPeerDisconnected += (peer, info) => Room.Disconnect(info);

                return true;
            }
            internal void Stop()
            {
                Peer.Disconnect();
                Manager.Stop();

                API.NetworkUpdate.OnEarlyUpdate -= PollEvents;
                API.NetworkUpdate.OnLateUpdate -= SendData;

                Listener.Dispose();
            }

            void PollEvents() => Manager.PollEvents();
            void SendData() => Manager.TriggerUpdate();

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

            RoomAPI Room;
            public TransportProperty(RoomAPI Room)
            {
                this.Room = Room;

                Listener = new BufferListener();

                Manager = new NetManager(Listener);
                Manager.AutoRecycle = false;
                Manager.ChannelsCount = Constants.ChannelCount;
                Manager.DisconnectTimeout = (int)Constants.Timeout.TotalMilliseconds;
                Manager.UpdateTime = 10_000; //Set a high number as to have send not invoked automatically by the library but manually by us instead.

                Dispatcher = new DispatcherProperty(this);
            }
        }

        public TimeProperty Time { get; private set; }
        public class TimeProperty
        {
            /// <summary>
            /// An estimate value of the round trip time between the client and server
            /// including packet processing time of client + server
            /// </summary>
            public TimeSpan RTT { get; private set; }

            Stopwatch Timer;
            TimeSpan Offset;

            readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(5);

            /// <summary>
            /// Calculate the time since this room was created
            /// </summary>
            /// <returns></returns>
            public TimeSpan CalculateElapsed()
            {
                return Timer.Elapsed + Offset;
            }

            /// <summary>
            /// Calculates the time in seconds since this room was created
            /// </summary>
            /// <returns></returns>
            public double CalculateSeconds() => CalculateElapsed().TotalSeconds;

            internal void Start()
            {
                Timer = Stopwatch.StartNew();

                Poll(Timer).Forget();
                async UniTask Poll(Stopwatch Timer)
                {
                    while (Timer.IsRunning is true)
                    {
                        await UniTask.Delay(RefreshInterval);
                        if (Timer.IsRunning is false)
                            break;

                        RequestUpdate();
                    }
                }
            }
            internal void Stop()
            {
                Timer.Stop();
            }

            internal RoomTimeRequest CreateRequest() => new RoomTimeRequest()
            {
                ClientTime = Timer.Elapsed
            };
            internal void ConsumeResponse(in RoomTimeResponse response)
            {
                RTT = (Timer.Elapsed - response.ClientRequest.ClientTime);

                var time = (Room: response.RoomTime, Local: response.ClientRequest.ClientTime + RTT);

                Offset = (time.Room - time.Local);

                NetworkLog.Info($"Room RTT: {RTT.TotalMilliseconds.ToString("N1")}ms");
            }

            internal void RequestUpdate()
            {
                var request = CreateRequest();
                Room.Transport.SendData(in request, delivery: DeliveryMethod.ReliableUnordered);
            }
            void ResponseHandler(ref RoomTimeResponse message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                ConsumeResponse(message);
            }

            readonly RoomAPI Room;
            public TimeProperty(RoomAPI Room)
            {
                this.Room = Room;

                Room.Transport.Dispatcher.Register<RoomTimeResponse>(ResponseHandler);
            }
        }

        public ClientsProperty Clients { get; private set; }
        public class ClientsProperty
        {
            public NetworkClient Master { get; private set; }
            public LocalNetworkClient Local { get; private set; }

            ExpandArray<NetworkClient> Collection;
            public byte Count => (byte)Collection.Count;
            public bool TryGet(NetworkClientID id, out NetworkClient client) => Collection.TryGet(id.Value, out client);

            /// <summary>
            /// The Groups that the local client is in
            /// </summary>
            public NetworkGroupCollection Groups { get; internal set; }
            public void JoinGroups(NetworkGroupCollection target)
            {
                ChangeGroups(Groups + target);
            }
            public void LeaveGroups(NetworkGroupCollection target)
            {
                ChangeGroups(Groups - target);
            }
            public void ChangeGroups(NetworkGroupCollection target)
            {
                Groups = target;

                var request = new ChangeGroupsRequest(Groups);

                Transport.SendData(request, delivery: DeliveryMethod.ReliableUnordered);
            }

            TransportProperty Transport => Room.Transport;

            UniTask ConnectionResponseHandler(ClientConnectionResponse message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                Room.Time.ConsumeResponse(message.TimeResponse);

                return Room.ReadState(message, reader);
            }

            internal void ReadState(NetPacketReader reader, ClientConnectionResponse message)
            {
                for (int i = 0; i < message.Clients; i++)
                {
                    var id = NetworkClient.ReadID(reader);

                    NetworkClient client;

                    if (id == message.LocalID)
                        client = Local = new LocalNetworkClient(id, message.SpawnTokens);
                    else
                        client = new RemoteNetworkClient(id);

                    if (id == message.MasterID)
                        Master = client;

                    client.ReadState(reader);

                    Register(client);
                }

                //Check if we didn't receive our local client
                if (Local is null)
                    throw new InvalidOperationException("No Local Client Received in Response");

                //Check if we didn't receive the master client
                if (Master is null)
                    throw new InvalidOperationException("No Master Client Received in Response");
            }

            void ConnectHandler(ref ClientConnectMessage message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                var client = RemoteNetworkClient.ReadInstance(ref reader);

                Register(client);

                OnConnect?.Invoke(client);
            }
            public event ClientDelegate OnConnect;

            void DisconnectHandler(ref ClientDisconnectMessage message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                //Set Master Client
                if (message.IsMasterClientChange(out var MasterID))
                    ChangeMaster(MasterID);

                //Get Disconnected Client
                if (TryGet(message.ClientID, out var client) is false)
                    throw new InvalidOperationException($"No Client Found with ID {message.ClientID}");

                //Handle Local Entities
                foreach (var entity in client.Entities)
                {
                    switch (entity.Authority)
                    {
                        case NetworkEntityAuthorityMode.Authoritative:
                            entity.IncrementTransferToken();
                            Room.Entities.Transfer(entity, Master);
                            break;

                        case NetworkEntityAuthorityMode.Explicit:
                            Room.Entities.InvokeDespawn(entity);
                            break;
                    }
                }

                //Despawn Instructed Entities
                while (reader.AvailableBytes > 0)
                {
                    var id = NetworkSerializer.ReadValue<NetworkEntityID>(reader);
                    var behaviour = NetworkSerializer.ReadValue<EntityDisconnectBehaviour>(reader);

                    if (Room.Entities.TryGet(id, out var entity) is false)
                        throw new InvalidOperationException($"No Entity with ID {entity} Found");

                    switch (behaviour)
                    {
                        case EntityDisconnectBehaviour.Despawn:
                            Room.Entities.InvokeDespawn(entity);
                            break;

                        case EntityDisconnectBehaviour.Transfer:
                        {
                            entity.IncrementTransferToken();
                            Room.Entities.Transfer(entity, Master);
                        }
                        break;

                        default: throw new NotImplementedException();
                    }
                }

                Unregister(client.ID);

                OnDisconnect?.Invoke(client);
            }
            public event ClientDelegate OnDisconnect;

            void ChangeMaster(NetworkClientID id)
            {
                if (TryGet(id, out var current) is false)
                    throw new InvalidOperationException($"No Client with ID {id} Found to Assign as Master");

                ChangeMaster(current);
            }
            void ChangeMaster(NetworkClient target)
            {
                var change = new ChangePairData<NetworkClient>(Master, target);

                Master = target;

                NetworkLog.Info($"Master Client Changed to {Master}");

                OnChangeMaster?.Invoke(change);
            }
            public event MasterChangeDelegate OnChangeMaster;
            public delegate void MasterChangeDelegate(ChangePairData<NetworkClient> client);

            void Register(NetworkClient client)
            {
                NetworkLog.Info($"Registered Client {client}");

                Collection.Add(client.ID.Value, client);
            }
            void Unregister(NetworkClientID id)
            {
                Collection.Remove(id.Value, out var client);

                NetworkLog.Info($"Unregistered Client {client}");
            }

            readonly RoomAPI Room;
            public ClientsProperty(RoomAPI room)
            {
                this.Room = room;

                Collection = new ExpandArray<NetworkClient>(10, NetworkClientID.Max.Value, 10);

                Transport.Dispatcher.RegisterAsync<ClientConnectionResponse>(ConnectionResponseHandler);

                Transport.Dispatcher.Register<ClientConnectMessage>(ConnectHandler);
                Transport.Dispatcher.Register<ClientDisconnectMessage>(DisconnectHandler);
            }

            public delegate void ClientDelegate(NetworkClient client);
        }

        public EntitiesProperty Entities { get; private set; }
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

            #region Controls
            public NetworkEntity InstantiatePrefab(GameObject prefab)
            {
                if (Room.API.SyncedPrefabs.TryGet(prefab, out var resource) is false)
                    throw new ArgumentException($"prefab {prefab} not Registered as Sync Prefab");

                return InstantiatePrefab(prefab, resource);
            }
            public NetworkEntity InstantiatePrefab(NetworkResourceID resource)
            {
                if (Room.API.SyncedPrefabs.TryGet(resource, out var prefab) is false)
                    throw new ArgumentOutOfRangeException($"resource {resource} not Registered as Sync Prefab");

                return InstantiatePrefab(prefab, resource);
            }
            NetworkEntity InstantiatePrefab(GameObject prefab, NetworkResourceID resource)
            {
                var gameObject = GameObject.Instantiate(prefab).GetComponent<NetworkEntity>();

                if (gameObject.TryGetComponent(out NetworkEntity entity) is false)
                    throw new ArgumentException($"Synced Prefab ({prefab}) Has no NetworkEntity Component Attached");

                entity.SetResource(resource);
                entity.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

                return entity;
            }

            public EntitySpawnOptions Spawn() => EntitySpawnOptions.CreateDefault();

            public void Despawn(NetworkEntity entity)
            {
                if (entity.IsSpawned is false)
                    throw new InvalidOperationException($"Can Only Despawn Spawned Entities");

                if (entity.Authority is NetworkEntityAuthorityMode.Authoritative && Room.Clients.Local.IsMaster is false)
                    throw new InvalidOperationException($"Only the Master Client can Despawn {NetworkEntityAuthorityMode.Authoritative} Entities");

                var request = new DespawnEntityRequest(entity.ID);
                Transport.SendData(in request);

                InvokeDespawn(entity);
            }

            public void TakeOwnership(NetworkEntity entity)
            {
                if (entity.IsSpawned is false)
                    throw new InvalidOperationException($"Can Only Take Ownership of Spawned Entities");

                if (entity.Authority is not NetworkEntityAuthorityMode.Transferable)
                    throw new InvalidOperationException($"Can Only Take Ownership of {NetworkEntityAuthorityMode.Transferable} Entities");

                var request = new TakeEntityOwnershipRequest(entity.ID, entity.TransferToken);
                Transport.SendData(in request);

                entity.TransferToken = NetworkEntityTransferToken.Increment(entity.TransferToken);
                Transfer(entity, Room.Clients.Local);
            }
            #endregion

            #region Handlers
            void SpawnPrefabResponseHandler(ref SpawnPrefabEntityResponse message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                Room.Clients.Local.AddSpawnToken(message.ReplacementToken);

                switch (message.Behaviour)
                {
                    case SpawnPrefabEntityResponseBehaviour.Replicate:
                    {
                        if (TryGet(message.SourceToken, out var entity) is false)
                        {
                            NetworkLog.Error($"No Network Entity with ID {message.SourceToken} Found");
                            return;
                        }

                        entity.Replicate();
                    }
                    break;

                    case SpawnPrefabEntityResponseBehaviour.Despawn:
                    {
                        if (TryGet(message.SourceToken, out var entity))
                            InvokeDespawn(entity);
                    }
                    break;

                    default: throw new NotImplementedException();
                }
            }

            void SpawnPrefabCommandHandler(ref SpawnPrefabEntityCommand message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                var definition = new NetworkEntityDefinition(message.ID, NetworkEntityOrigin.Prefab, message.Resource, message.Authority, message.Owner, NetworkEntityTransferToken.Zero);

                var instance = Assimilate(definition);

                //Read Network Variables
                {
                    var initialization = new EntitySpawnRequestInitializationDataReader(reader);

                    foreach (var entry in initialization)
                    {
                        if (instance.Behaviours.TryGet(entry.Behaviour, out var behaviour) is false)
                            throw new Exception($"No Behaviour {entry.Behaviour} Found on {instance}");

                        switch (entry.Type)
                        {
                            case SyncMemberType.RPC:
                            {
                                if (behaviour.RPC.TryGet(entry.Member, out var bind) is false)
                                    throw new Exception($"No RPC {entry.Member} Found on {behaviour}");

                                var source = BinarySource.From(entry.Binary.Span);
                                var info = RpcInfo.FromInitialization(message.Owner);

                                bind.Invoke(ref source, info);
                            }
                            break;

                            case SyncMemberType.Variable:
                            {
                                if (behaviour.Variables.TryGet(entry.Member, out var variable) is false)
                                    throw new Exception($"No Variable {entry.Member} Found on {behaviour}");

                                var source = BinarySource.From(entry.Binary.Span);
                                var info = NetworkVariableInfo.FromInitialization(message.Owner);

                                variable.Read(ref source, info);
                            }
                            break;

                            default: throw new NotImplementedException();
                        }
                    }
                }

                instance.Spawn();
                instance.Replicate();
            }

            void DespawnCommandHandler(ref DespawnEntityCommand message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                if (Dictionary.TryGetValue(message.ID, out var entity) is false)
                {
                    NetworkLog.Error($"No Network Entity with ID {message.ID} Found");
                    return;
                }

                InvokeDespawn(entity);
            }

            void TransferOwnershipCommandHandler(ref TransferEntityOwnershipCommand message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                if (Room.Clients.TryGet(message.Client, out var client) is false)
                {
                    NetworkLog.Error($"No Network Client with ID {message.Client} Found");
                    return;
                }

                if (Dictionary.TryGetValue(message.Entity, out var entity) is false)
                {
                    NetworkLog.Error($"No Network Entity with ID {message.Entity} Found");
                    return;
                }

                entity.AssignTransferToken(message.Token);
                Transfer(entity, client);
            }
            #endregion

            #region Modifiers
            internal NetworkEntity Assimilate(NetworkEntityDefinition definition)
            {
                var instance = RetrieveInstance(definition);

                instance.Assign(definition);
                Register(instance);

                return instance;
            }

            internal NetworkEntity SpawnLocal(ref EntitySpawnOptions options)
            {
                var entity = options.Instance;

                var definition = new NetworkEntityDefinition(options.Token, NetworkEntityOrigin.Prefab, entity.Resource, options.Authority, Room.Clients.Local.ID, NetworkEntityTransferToken.Zero);

                entity.Assign(definition);
                Register(entity);

                entity.Spawn();

                return entity;
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
            NetworkEntity FindInScene(NetworkResourceID resource)
            {
                if (Room.Scene.Component.TryGetLocal(resource, out var entity) is false)
                    throw new ArgumentException($"No Resource {resource} found in Scene {Room.Scene.Component}");

                return entity;
            }

            internal void Register(NetworkEntity entity)
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

                entity.Owner.UnregisterEntity(entity);
            }

            internal void InvokeDespawn(NetworkEntityID id)
            {
                if (Dictionary.TryGetValue(id, out var entity) is false)
                {
                    NetworkLog.Error($"No Network Entity with ID {id} Found");
                    return;
                }

                InvokeDespawn(entity);
            }
            internal void InvokeDespawn(NetworkEntity entity)
            {
                Unregister(entity);

                entity.Despawn();
            }

            internal void DespawnAll()
            {
                var list = Room.Pools.EntityList.Take();

                foreach (var (id, entity) in Dictionary)
                    list.Add(entity);

                foreach (var entity in list)
                    InvokeDespawn(entity);
            }

            internal void Transfer(NetworkEntity entity, NetworkClient to)
            {
                var from = entity.Owner;

                from.UnregisterEntity(entity);
                to.RegisterEntity(entity);

                entity.TransferOwner(to);
            }

            internal void Stop()
            {
                DespawnAll();
            }
            #endregion

            readonly RoomAPI Room;
            public EntitiesProperty(RoomAPI Room)
            {
                this.Room = Room;

                Dictionary = new Dictionary<NetworkEntityID, NetworkEntity>(40);

                Transport.Dispatcher.Register<SpawnPrefabEntityCommand>(SpawnPrefabCommandHandler);
                Transport.Dispatcher.Register<SpawnPrefabEntityResponse>(SpawnPrefabResponseHandler);
                Transport.Dispatcher.Register<DespawnEntityCommand>(DespawnCommandHandler);
                Transport.Dispatcher.Register<TransferEntityOwnershipCommand>(TransferOwnershipCommandHandler);
            }
        }

        public RpcsProperty RPCs { get; private set; }
        public class RpcsProperty
        {
            TransportProperty Transport => Room.Transport;

            void CommandHandler(ref NetworkRpcCommand message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                if (Get(ref message.Parameters, out var bind) is false)
                {
                    NetworkLog.Error($"No Network RPC Found for Parameters of {message.Parameters}");
                    return;
                }

                var info = RpcInfo.FromRemote(ref message, channel, delivery);

                bind.Invoke(reader, info);
            }

            bool Get(ref NetworkSyncMemberParameters parameters, out BaseRpcBind bind)
            {
                if (Room.Entities.TryGet(parameters.Entity, out var entity) is false)
                {
                    NetworkLog.Error($"No Network Entity with ID {parameters.Entity} Found");
                    bind = default;
                    return false;
                }

                return Get(entity, parameters.Behaviour, parameters.Member, out bind);
            }
            bool Get(NetworkEntity entity, NetworkBehaviourID behaviourID, NetworkSyncMemberID rpcID, out BaseRpcBind bind)
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
                while (true)
                {
                    var entityID = NetworkSerializer.ReadValue<NetworkEntityID>(reader);
                    if (entityID == NetworkEntityID.None)
                        break;

                    if (Room.Entities.TryGet(entityID, out var entity) is false)
                        throw new InvalidOperationException($"No Entity found With ID {entityID}");

                    var count = NetworkSerializer.ReadValue<ushort>(reader);

                    for (int y = 0; y < count; y++)
                    {
                        var behaviourID = NetworkSerializer.ReadValue<NetworkBehaviourID>(reader);
                        var rpcID = NetworkSerializer.ReadValue<NetworkSyncMemberID>(reader);

                        var senderID = NetworkSerializer.ReadValue<NetworkClientID>(reader);

                        if (Get(entity, behaviourID, rpcID, out var bind))
                        {
                            var info = RpcInfo.FromBuffer(senderID);
                            bind.Invoke(reader, info);
                        }
                    }
                }
            }

            readonly RoomAPI Room;
            public RpcsProperty(RoomAPI Room)
            {
                this.Room = Room;

                Transport.Dispatcher.Register<NetworkRpcCommand>(CommandHandler);
            }
        }

        public VariablesProperty Variables { get; private set; }
        public class VariablesProperty
        {
            TransportProperty Transport => Room.Transport;

            void CommandHandler(ref NetworkVariableCommand message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                if (Get(ref message.Parameters, out var bind) is false)
                {
                    NetworkLog.Error($"No Network RPC Found for Parameters of {message.Parameters}");
                    return;
                }

                var info = NetworkVariableInfo.FromRemote(ref message, channel, delivery);
                var source = NetworkVariableCommand.ReadValue(reader);

                bind.Read(ref source, info);
            }

            bool Get(ref NetworkSyncMemberParameters parameters, out NetworkVariable variable)
            {
                if (Room.Entities.TryGet(parameters.Entity, out var entity) is false)
                {
                    NetworkLog.Error($"No Network Entity with ID {parameters.Entity} Found");
                    variable = default;
                    return false;
                }

                return Get(entity, parameters.Behaviour, parameters.Member, out variable);
            }
            bool Get(NetworkEntity entity, NetworkBehaviourID behaviourID, NetworkSyncMemberID variableID, out NetworkVariable variable)
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
                while (true)
                {
                    var entityID = NetworkSerializer.ReadValue<NetworkEntityID>(reader);
                    if (entityID == NetworkEntityID.None)
                        break;

                    if (Room.Entities.TryGet(entityID, out var entity) is false)
                        throw new NotImplementedException();

                    var count = NetworkSerializer.ReadValue<ushort>(reader);

                    for (int y = 0; y < count; y++)
                    {
                        var behaviourID = NetworkSerializer.ReadValue<NetworkBehaviourID>(reader);
                        var variableID = NetworkSerializer.ReadValue<NetworkSyncMemberID>(reader);

                        var senderID = NetworkSerializer.ReadValue<NetworkClientID>(reader);

                        if (Get(entity, behaviourID, variableID, out var variable))
                        {
                            var info = NetworkVariableInfo.FromBuffer(senderID);
                            var source = BinarySource.From(reader);

                            variable.Read(ref source, info);
                        }
                    }
                }
            }

            readonly RoomAPI Room;
            public VariablesProperty(RoomAPI Room)
            {
                this.Room = Room;

                Transport.Dispatcher.Register<NetworkVariableCommand>(CommandHandler);
            }
        }

        public SceneProperty Scene { get; private set; }
        public class SceneProperty
        {
            public NetworkSceneID ID { get; private set; }
            public int BuildIndex => ID.Value;

            public NetworkSceneVersion Version { get; private set; }
            public bool IsSpawned { get; private set; }

            public NetworkScene Component { get; private set; }

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
                IsSpawned = NetworkSerializer.ReadValue<bool>(reader);

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
                NetworkLog.Trace($"Changing Scene From (ID: {ID}, Version: {Version}) To (ID: {message.ID}, Version: {message.Version})");

                Procedure(message).Forget();
                async UniTask Procedure(ChangeSceneCommand message)
                {
                    Room.Transport.Listener.Pause();
                    {
                        Room.Entities.DespawnAll();

                        await ChangeProcedure(message.ID, message.Version);

                        if (Room.Clients.Local.IsMaster)
                            RequestSpawn();
                    }
                    Room.Transport.Listener.Resume();
                }
            }

            public async UniTask ChangeProcedure(NetworkSceneID ID, NetworkSceneVersion Version)
            {
                this.ID = ID;
                this.Version = Version;

                if (IsRegistered)
                    Component.Despawn();

                Operation = SceneManager.LoadSceneAsync(BuildIndex);
                await Operation;

                while (IsRegistered is false)
                    await UniTask.NextFrame();
            }

            internal void RequestSpawn()
            {
                var writer = Room.Pools.SinglePackerWriter.Take();

                var message = new SpawnSceneRequest();
                NetworkSerializer.WriteHeader(in message, writer);

                Component.WriteRequest(writer);

                Room.Transport.SendWriter(writer);
            }
            internal void InvokeSpawn()
            {
                Component.Spawn();
            }

            internal void ApplyState()
            {
                if (IsSpawned)
                    InvokeSpawn();
                else if (Room.Clients.Local.IsMaster)
                    RequestSpawn();
            }

            void SpawnSceneCommandHandler(ref SpawnSceneCommand message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                var component = Room.Scene.Component;
                var count = Room.Scene.Component.Locals.Length;

                for (byte i = 0; i < count; i++)
                {
                    var entity = component.Locals[i];

                    var id = NetworkSerializer.ReadValue<NetworkEntityID>(reader);
                    var resource = new NetworkResourceID(i);
                    var authority = component.Locals[i].Authority;
                    var ownerID = Room.Clients.Master.ID;

                    var transferToken = NetworkEntityTransferToken.Zero;

                    var definition = new NetworkEntityDefinition(id, NetworkEntityOrigin.Scene, resource, authority, ownerID, transferToken);

                    entity.Assign(definition);
                    Room.Entities.Register(entity);

                    entity.Spawn();
                    entity.Replicate();
                }

                InvokeSpawn();
            }

            readonly RoomAPI Room;
            public SceneProperty(RoomAPI Room)
            {
                this.Room = Room;

                Room.Transport.Dispatcher.Register<ChangeSceneCommand>(ChangeCommandHandler);
                Room.Transport.Dispatcher.Register<SpawnSceneCommand>(SpawnSceneCommandHandler);
            }
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

                //Apply Scene State (Spawn if Spawned, else Request if Master)
                Scene.ApplyState();
            }
            Transport.Listener.Resume();
        }
    }

    public ref struct EntitySpawnOptions
    {
        internal NetworkEntityID Token;
        internal NetworkEntity Instance;
        internal NetworkEntityAuthorityMode Authority;

        static NetworkAPI API => NetworkAPI.Instance;
        static RoomAPI Room => API.Room;

        public EntitySpawnOptions SetResource(NetworkResourceID resource)
        {
            var instance = Room.Entities.InstantiatePrefab(resource);
            SetInstance(instance);
            return this;
        }
        public EntitySpawnOptions SetPrefab(GameObject prefab)
        {
            var instance = Room.Entities.InstantiatePrefab(prefab);
            SetInstance(instance);
            return this;
        }
        public EntitySpawnOptions SetInstance(NetworkEntity value)
        {
            Instance = value;
            return this;
        }

        public EntitySpawnOptions SetAuthority(NetworkEntityAuthorityMode mode)
        {
            if (mode is NetworkEntityAuthorityMode.Authoritative && Room.Clients.Local.IsMaster is false)
            {
                NetworkLog.Error($"Can Only Spawn Items with {NetworkEntityAuthorityMode.Authoritative} Authority if Master Client");
                return this;
            }

            Authority = mode;
            return this;
        }

        bool Validate()
        {
            if (Room.Clients.Local.SpawnAllowance is 0)
            {
                NetworkLog.Error($"Client's Spawn Allowance Exceeded, Need to Wait for More");
                return false;
            }

            if (Instance == null)
            {
                NetworkLog.Error($"No Entity (Resource/Prefab/Instance) Specified");
                return false;
            }

            return true;
        }

        public EntitySpawnTicket Ticket()
        {
            if (Validate() is false)
                throw new InvalidOperationException($"Invalid Spawn Options");

            Token = Room.Clients.Local.RemoveSpawnToken();

            return new EntitySpawnTicket(ref this);
        }

        public NetworkEntity Send() => Ticket().Send();

        internal SpawnPrefabEntityRequest CreateSpawnRequest() => new SpawnPrefabEntityRequest(Token, Instance.Resource, Authority, Room.Scene.Version);

        public static EntitySpawnOptions CreateDefault() => new EntitySpawnOptions()
        {
            Authority = NetworkEntityAuthorityMode.Explicit,
        };
    }
    public ref struct EntitySpawnTicket
    {
        EntitySpawnOptions Options;
        NetDataWriter Writer;

        public NetworkEntity Entity => Options.Instance;

        static NetworkAPI API => NetworkAPI.Instance;
        static RoomAPI Room => API.Room;

        public NetworkEntity Send()
        {
            Room.Transport.SendWriter(in Writer);

            //Spawn Local
            return Room.Entities.SpawnLocal(ref Options);
        }

        internal void WriteRPC<TBind, TParameters>(TBind bind, TParameters parameters, bool local)
            where TBind : BaseRpcBind, IBaseRpcBind<TParameters>
            where TParameters : IRpcParameters
        {
            if (bind.Entity != Entity)
                throw new ArgumentException($"Invalid Entity, Initializing Variable for {bind.Entity} on Ticket for {Entity}");

            if (Entity.IsSpawned)
            {
                NetworkLog.Error($"Entity Already Spawned, Can Only Initialize Variable Before Entity Spawn");
                return;
            }

            var source = BinarySource.From(Writer);

            NetworkSerializer.WriteValue(bind.Behaviour.ID, ref source);
            NetworkSerializer.WriteValue(SyncMemberType.RPC, ref source);
            NetworkSerializer.WriteValue(bind.ID, ref source);

            BinarySource LengthHeader;
            //Allocate Length
            {
                var span = source.AllocateSpan(sizeof(ushort));
                LengthHeader = BinarySource.From(span);
            }

            //Invoke Local Method
            if (local)
            {
                var info = RpcInfo.FromInitialization();
                bind.Invoke(parameters, info);
            }

            var cursor = source.Position;
            parameters.WriteTo(Writer);

            //Write Length
            {
                var length = (ushort)(source.Position - cursor);
                NetworkSerializer.WriteValue(in length, ref LengthHeader);
            }
        }
        internal void WriteVariable<T>(NetworkVariable<T> variable, in T value)
        {
            if (variable.Entity != Entity)
                throw new ArgumentException($"Invalid Entity, Initializing Variable for {variable.Entity} on Ticket for {Entity}");

            if (Entity.IsSpawned)
            {
                NetworkLog.Error($"Entity Already Spawned, Can Only Initialize Variable Before Entity Spawn");
                return;
            }

            var source = BinarySource.From(Writer);

            NetworkSerializer.WriteValue(variable.Behaviour.ID, ref source);
            NetworkSerializer.WriteValue(SyncMemberType.Variable, ref source);
            NetworkSerializer.WriteValue(variable.ID, ref source);

            BinarySource LengthHeader;
            //Allocate Length
            {
                var span = source.AllocateSpan(sizeof(ushort));
                LengthHeader = BinarySource.From(span);
            }

            //Set Local Variable
            {
                var info = NetworkVariableInfo.FromInitialization();
                variable.Set(value, info);
            }

            var cursor = source.Position;
            variable.Write(ref source);

            //Write Length
            {
                var length = (ushort)(source.Position - cursor);
                NetworkSerializer.WriteValue(in length, ref LengthHeader);
            }
        }

        public EntitySpawnTicket(ref EntitySpawnOptions Options)
        {
            this.Options = Options;

            Writer = Room.Pools.SinglePackerWriter.Take();

            var request = Options.CreateSpawnRequest();

            NetworkSerializer.WriteHeader(in request, Writer);
        }
    }
}
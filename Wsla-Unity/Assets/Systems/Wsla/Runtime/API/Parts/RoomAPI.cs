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
                Scenes = new ScenesProperty(this);
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
                Scenes = default;
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

                            if (reader.AvailableBytes != 0)
                                NetworkLog.Warning($"Payload ({typeof(T)}) Handler's ({handler}) Reader Still Has {reader.AvailableBytes} Bytes Available");
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

                            if (reader.AvailableBytes != 0)
                                NetworkLog.Warning($"Payload ({typeof(T)}) Handler's ({handler}) Reader Still Has {reader.AvailableBytes} Bytes Available");
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

            internal void ReadState(NetPacketReader stream, ClientConnectionResponse message)
            {
                using var reader = new NetworkClientDefinition.PayloadReader(stream, message.Clients);

                for (int i = 0; i < reader.Count; i++)
                {
                    var definition = reader.Read();

                    NetworkClient client;

                    if (definition.ID == message.LocalID)
                        client = Local = new LocalNetworkClient(definition);
                    else
                        client = new RemoteNetworkClient(definition);

                    if (client.ID == message.MasterID)
                        Master = client;

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
                var client = new RemoteNetworkClient(message.Client);

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
                foreach (var handling in new ClientDisconnectMessage.EntityHandlingPayload.Reader(reader))
                {
                    if (Room.Entities.TryGet(handling.ID, out var entity) is false)
                        throw new InvalidOperationException($"No Entity with ID {entity} Found");

                    switch (handling.Behaviour)
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

            internal void ReadState(NetPacketReader stream, ClientConnectionResponse message, List<NetworkEntity> list)
            {
                using var payload = new NetworkEntityDefinition.PayloadReader(stream, message.Entities);

                for (int i = 0; i < payload.Count; i++)
                {
                    var definition = payload.Read();
                    var instance = Assimilate(definition);
                    list.Add(instance);
                }
            }

            #region Controls
            public NetworkEntity InstantiatePrefab(NetworkResourceID resource, NetworkScene scene)
            {
                if (Room.API.SyncedPrefabs.TryGet(resource, out var prefab) is false)
                    throw new ArgumentOutOfRangeException($"resource {resource} not Registered as Sync Prefab");

                return InstantiatePrefab(prefab, resource, scene);
            }
            public NetworkEntity InstantiatePrefab(GameObject prefab, NetworkScene scene)
            {
                if (Room.API.SyncedPrefabs.TryGet(prefab, out var resource) is false)
                    throw new ArgumentException($"prefab {prefab} not Registered as Sync Prefab");

                return InstantiatePrefab(prefab, resource, scene);
            }
            NetworkEntity InstantiatePrefab(GameObject prefab, NetworkResourceID resource, NetworkScene scene)
            {
                var gameObject = GameObject.Instantiate(prefab).GetComponent<NetworkEntity>();

                if (gameObject.TryGetComponent(out NetworkEntity entity) is false)
                    throw new ArgumentException($"Synced Prefab ({prefab}) Has no NetworkEntity Component Attached");

                entity.SetNetworkScene(scene);
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
                var definition = new NetworkEntityDefinition(message.ID, NetworkEntityOrigin.Prefab, message.Resource, message.Authority, message.Owner, NetworkEntityTransferToken.Zero, message.Scene);

                var instance = Assimilate(definition);

                //Read RPC & Variable Initialization Data
                using (var payload = new SpawnPrefabEntityRequest.SyncMemberInitializationPayload.Reader(reader))
                {
                    while (payload.TryRead(out var behaviourID, out var type, out var memberID, out var binary))
                    {
                        if (instance.Behaviours.TryGet(behaviourID, out var behaviour) is false)
                            throw new Exception($"No Behaviour {behaviourID} Found on {instance}");

                        switch (type)
                        {
                            case SyncMemberType.RPC:
                            {
                                if (behaviour.RPC.TryGet(memberID, out var bind) is false)
                                    throw new Exception($"No RPC {memberID} Found on {behaviour}");

                                var source = BinarySource.From(binary.Span);
                                var info = RpcInfo.FromInitialization(message.Owner);

                                bind.Invoke(ref source, info);
                            }
                            break;

                            case SyncMemberType.Variable:
                            {
                                if (behaviour.Variables.TryGet(memberID, out var variable) is false)
                                    throw new Exception($"No Variable {memberID} Found on {behaviour}");

                                var source = BinarySource.From(binary.Span);
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
                var entity = options.EntityInstance;

                var definition = new NetworkEntityDefinition(options.Token, NetworkEntityOrigin.Prefab, entity.Resource, options.Authority, Room.Clients.Local.ID, NetworkEntityTransferToken.Zero, options.Scene.ID);

                entity.Assign(definition);
                Register(entity);

                entity.Spawn();

                return entity;
            }

            NetworkEntity RetrieveInstance(NetworkEntityDefinition definition)
            {
                if (NetworkScene.Manager.TryGet(definition.Scene, out var scene) is false)
                    throw new ArgumentException($"No Network Scene With ID {definition.Scene} Found");

                switch (definition.Origin)
                {
                    case NetworkEntityOrigin.Prefab:
                        return InstantiatePrefab(definition.Resource, scene);

                    case NetworkEntityOrigin.Scene:
                        return FindInScene(scene, definition.Resource);

                    default:
                        throw new NotImplementedException();
                }

            }
            NetworkEntity FindInScene(NetworkScene scene, NetworkResourceID resource)
            {
                if (scene.TryGetLocal(resource, out var entity) is false)
                    throw new ArgumentException($"No Resource {resource} found in Scene {scene}");

                entity.SetNetworkScene(scene);

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
            internal void DespawnScene(NetworkScene scene)
            {
                var list = Room.Pools.EntityList.Take();

                foreach (var (id, entity) in Dictionary)
                {
                    if (entity.Scene != scene)
                        continue;

                    list.Add(entity);
                }

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

            internal void ReadState(NetPacketReader stream)
            {
                using var payload = new NetworkSyncMemberBufferPayload.EntityReader(stream);

                while (payload.TryReadID(out var entityID))
                {
                    if (Room.Entities.TryGet(entityID, out var entity) is false)
                        throw new InvalidOperationException($"No Entity found With ID {entityID}");

                    using var member = payload.ReadMember(entityID);

                    for (int i = 0; i < member.Count; i++)
                    {
                        member.Read(out var behaviour, out var rpc, out var sender, out var data);

                        if (Get(entity, behaviour, rpc, out var bind))
                        {
                            var info = RpcInfo.FromBuffer(sender);
                            bind.Invoke(data, info);
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

            internal void ReadState(NetPacketReader stream)
            {
                using var payload = new NetworkSyncMemberBufferPayload.EntityReader(stream);

                while (payload.TryReadID(out var entityID))
                {
                    if (Room.Entities.TryGet(entityID, out var entity) is false)
                        throw new InvalidOperationException($"No Entity found With ID {entityID}");

                    using var member = payload.ReadMember(entityID);

                    for (int i = 0; i < member.Count; i++)
                    {
                        member.Read(out var behaviour, out var variable, out var sender, out var data);

                        if (Get(entity, behaviour, variable, out var bind))
                        {
                            var info = NetworkVariableInfo.FromBuffer(sender);
                            var source = BinarySource.From(data);
                            bind.Read(ref source, info);
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

        public ScenesProperty Scenes { get; private set; }
        public class ScenesProperty
        {
            List<NetworkScene> Collection;
            public NetworkScene Active => Collection[0];
            byte CountUnspawnedScenes()
            {
                byte count = 0;

                foreach (var scene in Collection)
                    if (scene.IsSpawned is false)
                        count += 1;

                return count;
            }
            public bool TryGet(NetworkSceneID ID, out NetworkScene instance)
            {
                for (int i = 0; i < Collection.Count; i++)
                {
                    instance = Collection[i];

                    if (instance.ID == ID)
                        return true;
                }

                instance = default;
                return false;
            }
            public bool Contains(NetworkSceneID ID)
            {
                for (int i = 0; i < Collection.Count; i++)
                {
                    if (Collection[i].ID == ID)
                        return true;
                }

                return false;
            }

            SceneLoadingController SceneLoadHandler => Room.API.SceneLoadController;

            internal async UniTask ApplyState(ClientConnectionResponse response)
            {
                SceneLoadHandler?.StartLoading(response.Scenes.Length);

                for (int i = 0; i < response.Scenes.Length; i++)
                {
                    var definition = response.Scenes[i];
                    var mode = (i == 0) ? LoadSceneMode.Single : LoadSceneMode.Additive;

                    var progress = SceneLoadHandler?.RetrieveSurrogate();
                    var instance = await NetworkScene.Manager.Loading.Load(definition.ID, definition.Version, mode, progress);
                    Collection.Add(instance);
                }
            }
            internal void RequestSpawn()
            {
                var SceneCount = CountUnspawnedScenes();
                if (SceneCount is 0)
                {
                    NetworkLog.Warning($"No Unspawned Network Scenes Found");
                    return;
                }

                var writer = Room.Pools.SinglePackerWriter.Take();

                var message = new SpawnSceneRequest();
                NetworkSerializer.WriteHeader(in message, writer);

                using var ScenesWriter = new SpawnSceneRequest.AuthorizationPayload.SceneWriter(writer, SceneCount);
                foreach (var scene in Collection)
                {
                    if (scene.IsSpawned)
                        continue;

                    using var EntryWriter = ScenesWriter.Write(scene.ID, (byte)scene.Locals.Length);

                    foreach (var entity in scene.Locals)
                    {
                        if (entity.Authority is NetworkEntityAuthorityMode.Explicit)
                        {
                            NetworkLog.Warning($"Network Entity ({entity.gameObject}) in Scene ({entity.gameObject.scene.name}) Has an {entity.Authority} Authority, Scene Objects Can Only have {NetworkEntityAuthorityMode.Authoritative} & {NetworkEntityAuthorityMode.Transferable} Authority, Switching");
                            entity.Authority = NetworkEntityAuthorityMode.Authoritative;
                        }

                        EntryWriter.Write(entity.Authority);
                    }
                }

                Room.Transport.SendWriter(writer);
            }
            internal void SpawnAll()
            {
                //Locally Spawn All Remote Spawned Scenes 
                foreach (var scene in Collection)
                    if (scene.IsSpawned)
                        scene.Spawn();

                //Remotely Spawn All UnSpawned Scenes
                if (Room.Clients.Local.IsMaster && CountUnspawnedScenes() > 0)
                    RequestSpawn();
            }

            #region Controls
            public void Change(SparseArray<NetworkSceneID> targets)
            {
                if (Room.Clients.Local.IsMaster is false)
                    throw new InvalidOperationException($"Only the Master Client can Change Scenes");

                if (targets.Length is 0)
                    throw new ArgumentException($"Zero Target Scenes Modifications Provided");

                var request = new ChangeSceneRequest(targets);
                Room.Transport.SendData(in request);
            }

            public void Modify(SparseArray<NetworkSceneID> unload, SparseArray<NetworkSceneID> load)
            {
                if (Room.Clients.Local.IsMaster is false)
                    throw new InvalidOperationException($"Only the Master Client can Change Scenes");

                if (unload.Length is 0 && load.Length is 0)
                    throw new ArgumentException($"Zero Unload/Load Scenes Modifications Provided");

                //Validate Unload
                foreach (var entry in unload)
                {
                    if (Contains(entry) is false)
                        throw new ArgumentException($"Can't Unload Scene {entry}, Scene Not Loaded");
                }

                if (unload.Length == Collection.Count)
                {
                    NetworkLog.Warning($"Scene Modification For Unloading All Scenes Are Inefficient, Send A Scene Change Request Instead");
                    Change(load);
                }
                else
                {
                    //Validate Load
                    foreach (var entry in load)
                    {
                        if (Contains(entry))
                            throw new ArgumentException($"Can't Load Scene {entry}, Scene Already Loaded");
                    }

                    var request = new ModifyScenesRequest(unload, load);
                    Room.Transport.SendData(in request);
                }
            }
            public void Load(SparseArray<NetworkSceneID> scenes) => Modify(default, scenes);
            public void Unload(SparseArray<NetworkSceneID> scenes) => Modify(scenes, default);
            #endregion

            async UniTask ChangeCommandHandler(ChangeSceneCommand message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                NetworkLog.Trace($"Changing Scenes");

                Room.Transport.Listener.Pause();
                {
                    SceneLoadHandler?.StartLoading(message.Scenes.Length);

                    //Despawn All Entities
                    Room.Entities.DespawnAll();

                    //Clear Old Scenes
                    {
                        foreach (var scene in Collection)
                            scene.Despawn();

                        Collection.Clear();
                    }

                    //Load New Scenes
                    for (int i = 0; i < message.Scenes.Length; i++)
                    {
                        var definition = message.Scenes[i];
                        var mode = (i == 0) ? LoadSceneMode.Single : LoadSceneMode.Additive;

                        var progress = SceneLoadHandler?.RetrieveSurrogate();
                        var instance = await NetworkScene.Manager.Loading.Load(definition.ID, definition.Version, mode, progress);
                        Collection.Add(instance);
                    }

                    if (Room.Clients.Local.IsMaster)
                        RequestSpawn();
                }
                Room.Transport.Listener.Resume();
            }
            async UniTask ModifyCommandHandler(ModifyScenesCommand message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                NetworkLog.Trace("Modifying Scenes");

                Room.Transport.Listener.Pause();
                {
                    SceneLoadHandler?.StartLoading(message.Load.Length + message.Unload.Length);

                    //Unload
                    {
                        for (int i = 0; i < message.Unload.Length; i++)
                        {
                            var id = message.Unload[i];

                            if (TryGet(id, out var instance) is false)
                                throw new InvalidOperationException($"No Scene {id} Found To Unload");

                            Room.Entities.DespawnScene(instance);
                            Collection.Remove(instance);

                            var progress = SceneLoadHandler?.RetrieveSurrogate();
                            await NetworkScene.Manager.Unloading.Unload(instance, progress);
                        }
                    }

                    //Load
                    {
                        for (int i = 0; i < message.Load.Length; i++)
                        {
                            var definition = message.Load[i];
                            var progress = SceneLoadHandler?.RetrieveSurrogate();
                            var instance = await NetworkScene.Manager.Loading.Load(definition.ID, definition.Version, LoadSceneMode.Additive, progress);
                            Collection.Add(instance);
                        }

                        if (Room.Clients.Local.IsMaster)
                            RequestSpawn();
                    }

                    if (message.Load.Length is 0) SceneLoadHandler?.EndLoading();
                }
                Room.Transport.Listener.Resume();
            }
            void SpawnSceneCommandHandler(ref SpawnSceneCommand message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                using var ScenesReader = new SpawnSceneCommand.EntityIDPayload.SceneReader(reader);

                for (byte x = 0; x < ScenesReader.Count; x++)
                {
                    using var EntryReader = ScenesReader.Read();

                    if (TryGet(EntryReader.Scene, out var instance) is false)
                        throw new InvalidOperationException($"No Scene {EntryReader.Scene} Found to Spawn");

                    for (byte y = 0; y < EntryReader.Count; y++)
                    {
                        var id = EntryReader.Read();
                        var entity = instance.Locals[y];
                        var resource = new NetworkResourceID(y);
                        var authority = entity.Authority;
                        var ownerID = Room.Clients.Master.ID;
                        var transferToken = NetworkEntityTransferToken.Zero;

                        var definition = new NetworkEntityDefinition(id, NetworkEntityOrigin.Scene, resource, authority, ownerID, transferToken, instance.ID);
                        entity.Assign(definition);

                        entity.SetNetworkScene(instance);
                        Room.Entities.Register(entity);

                        entity.Spawn();
                        entity.Replicate();
                    }

                    instance.Spawn();
                }

                SceneLoadHandler?.EndLoading();
            }

            readonly RoomAPI Room;
            public ScenesProperty(RoomAPI Room)
            {
                this.Room = Room;

                Collection = new(1);

                Room.Transport.Dispatcher.RegisterAsync<ChangeSceneCommand>(ChangeCommandHandler);
                Room.Transport.Dispatcher.RegisterAsync<ModifyScenesCommand>(ModifyCommandHandler);

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
                await Scenes.ApplyState(message);

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
                Scenes.SpawnAll();
            }
            Transport.Listener.Resume();
        }
    }

    public ref struct EntitySpawnOptions
    {
        internal NetworkEntityID Token;
        internal NetworkEntityAuthorityMode Authority;
        internal NetworkScene Scene;

        internal GameObject EntityPrefab;
        internal NetworkEntity EntityInstance;

        static NetworkAPI API => NetworkAPI.Instance;
        static RoomAPI Room => API.Room;

        public EntitySpawnOptions SetResource(NetworkResourceID value)
        {
            if (Room.API.SyncedPrefabs.TryGet(value, out EntityPrefab) is false)
                throw new ArgumentException($"No Synced Prefab Found With ID {value}");

            return this;
        }
        public EntitySpawnOptions SetPrefab(GameObject value)
        {
            EntityPrefab = value;
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

        public EntitySpawnOptions SetScene(NetworkSceneID id)
        {
            if (API.Room.Scenes.TryGet(id, out var instance) is false)
                throw new ArgumentException($"No Scene {id} Loaded");

            return SetScene(instance);
        }
        public EntitySpawnOptions SetScene(NetworkScene value)
        {
            if (value == null)
                throw new ArgumentNullException("scene");

            Scene = value;
            return this;
        }

        bool Validate()
        {
            if (Room.Clients.Local.SpawnAllowance is 0)
            {
                NetworkLog.Error($"Client's Spawn Allowance Exceeded, Need to Wait for More");
                return false;
            }

            if (EntityInstance == null)
            {
                NetworkLog.Error($"No Entity (Resource/Prefab/Instance) Specified");
                return false;
            }

            return true;
        }

        public EntitySpawnTicket Ticket()
        {
            EntityInstance = Room.Entities.InstantiatePrefab(EntityPrefab, Scene);

            if (Validate() is false)
                throw new InvalidOperationException($"Invalid Spawn Options");

            Token = Room.Clients.Local.RemoveSpawnToken();

            return new EntitySpawnTicket(ref this);
        }

        public NetworkEntity Send() => Ticket().Send();

        internal SpawnPrefabEntityRequest CreateSpawnRequest() => new SpawnPrefabEntityRequest(Token, EntityInstance.Resource, Authority, Scene.Definition);

        public static EntitySpawnOptions CreateDefault() => new EntitySpawnOptions()
        {
            Authority = NetworkEntityAuthorityMode.Explicit,
            Scene = Room.Scenes.Active,
        };
    }
    public ref struct EntitySpawnTicket
    {
        EntitySpawnOptions Options;
        NetDataWriter Stream;

        public NetworkEntity Entity => Options.EntityInstance;

        static NetworkAPI API => NetworkAPI.Instance;
        static RoomAPI Room => API.Room;

        public NetworkEntity Send()
        {
            Room.Transport.SendWriter(in Stream);

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

            //Invoke Local Method
            if (local)
            {
                var info = RpcInfo.FromInitialization();
                bind.Invoke(parameters, info);
            }

            using (var payload = new SpawnPrefabEntityRequest.SyncMemberInitializationPayload.Writer(Stream, bind.Behaviour.ID, SyncMemberType.RPC, bind.ID))
            {
                parameters.WriteTo(payload.Stream);
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

            //Set Local Variable
            {
                var info = NetworkVariableInfo.FromInitialization();
                variable.Set(value, info);
            }

            using (var payload = new SpawnPrefabEntityRequest.SyncMemberInitializationPayload.Writer(Stream, variable.Behaviour.ID, SyncMemberType.Variable, variable.ID))
            {
                var source = BinarySource.From(payload.Stream);
                variable.Write(ref source);
            }
        }

        public EntitySpawnTicket(ref EntitySpawnOptions Options)
        {
            this.Options = Options;

            Stream = Room.Pools.SinglePackerWriter.Take();

            var request = Options.CreateSpawnRequest();

            NetworkSerializer.WriteHeader(in request, Stream);
        }
    }

}
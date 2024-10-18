using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Threading;

using Cysharp.Threading.Tasks;

using LiteNetLib;
using LiteNetLib.Utils;

using MemoryPack;

using NUnit.Framework;

using Toolbox;

using Unity.Hierarchy;

using UnityEditor.PackageManager;

using UnityEngine;

using Wsla.Shared.Global;

namespace Wsla.Unity
{
    [Serializable]
    public class RoomAPI : NetworkAPI.Property
    {
        public async UniTask<Response<RoomInstance, WslaError>> Connect(IPAddress address, ushort port, ClientConnectionRequest request)
        {
            var room = new RoomInstance(address, port);

            var response = await room.Start(request);

            if (response.IsError)
                return response.Error;

            return room;
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
                    var type = NetworkSerializer.ReadType(in reader);
                    var id = NetworkTypes.Get(type);

                    var handler = Handlers[id];
                    if (handler is null)
                    {
                        NetworkLog.Error($"No Dispatch Handler Provided for {type} Message");
                        return;
                    }

                    handler(reader, channel, delivery);
                }

                public delegate void TypeDelegate<T>(ref T message, NetPacketReader reader, byte channel, DeliveryMethod delivery);
                public void Register<T>(TypeDelegate<T> handler)
                {
                    var id = NetworkTypes.Get<T>();

                    Handlers[id] = Surrogate;

                    void Surrogate(NetPacketReader reader, byte channel, DeliveryMethod delivery)
                    {
                        var data = NetworkSerializer.ReadValue<T>(in reader);
                        handler(ref data, reader, channel, delivery);
                    }
                }

                readonly TransportProperty Transport;
                public DispatcherProperty(TransportProperty transport)
                {
                    this.Transport = transport;

                    Handlers = new ActionDelegate[NetworkTypes.Capacity];

                    Transport.Listener.NetworkReceiveEvent += ReceiveCallback;
                }
            }

            public readonly PacketWriterProperty PacketWriter;
            public struct PacketWriterProperty
            {
                NetDataWriter Instance;

                public NetDataWriter Take()
                {
                    Instance.SetPosition(0);
                    return Instance;
                }

                public PacketWriterProperty(NetDataWriter instance)
                {
                    this.Instance = instance;
                }

                public static PacketWriterProperty Create(int capacity)
                {
                    var instance = new NetDataWriter(true, capacity);
                    return new PacketWriterProperty(instance);
                }
            }

            NetManager Manager;
            EventBasedNetListener Listener;
            NetPeer Peer;

            CancellationTokenSource CancellationSource;

            internal async UniTask<Response<WslaError>> Start(ClientConnectionRequest request)
            {
                //Connect
                {
                    Manager.Start();

                    var operation = new UniTaskCompletionSource<Response<WslaError>>();

                    Listener.PeerConnectedEvent += Connected;
                    void Connected(NetPeer peer) => operation.TrySetResult(true);

                    Listener.PeerDisconnectedEvent += Disconnect;
                    void Disconnect(NetPeer peer, DisconnectInfo info) => operation.TrySetResult(WslaError.From(info));

                    //Request
                    {
                        var packet = new NetDataWriter(true, 128);
                        MemoryPackSerializer.Serialize(packet, request);
                        Peer = Manager.Connect("127.0.0.1", Constants.RelayManagementPort, packet);
                    }

                    CancellationSource = new CancellationTokenSource();
                    Poll(CancellationSource.Token).Forget();

                    var response = await operation.Task;

                    Listener.PeerConnectedEvent -= Connected;
                    Listener.PeerDisconnectedEvent -= Disconnect;

                    if (response.IsError)
                    {
                        Stop();
                        return response.Error;
                    }
                }

                return true;
            }
            internal void Stop()
            {
                CancellationSource.Cancel();
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

            public void Send<T>(in T data, byte channel = 0, DeliveryMethod delivery = DeliveryMethod.ReliableOrdered)
            {
                var writer = PacketWriter.Take();

                NetworkSerializer.WriteHeader(in writer, data);

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

                Listener = new EventBasedNetListener();

                Manager = new NetManager(Listener);

                PacketWriter = PacketWriterProperty.Create(256);

                Dispatcher = new DispatcherProperty(this);
            }
        }

        public ClientsProperty Clients { get; }
        public class ClientsProperty
        {
            public LocalNetworkClient Local { get; private set; }

            AutoExpandArray<NetworkClient> Collection;

            TransportProperty Transport => Room.Transport;

            void ConnectResponseHandler(ref ClientConnectionResponse message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                //Read Clients
                for (int i = 0; i < message.Clients; i++)
                {
                    var id = NetworkClient.ReadID(reader);

                    var isLocal = id == message.ID;

                    NetworkClient client;

                    if (isLocal)
                        client = Local = new LocalNetworkClient(id, message.SpawnTokens);
                    else
                        client = new RemoteNetworkClient(id);

                    client.ReadState(reader);

                    Register(client);
                }

                //Check if we didn't recieve our local client
                if (Local is null)
                    throw new InvalidOperationException("No Local Client Received in Response");

                //Spawn Tokens
                {
                    for (int i = 0; i < message.SpawnTokens; i++)
                    {
                        var token = NetworkSerializer.ReadValue<NetworkEntityID>(in reader);
                        Local.AddSpawnToken(token);

                        Debug.Log($"Spawn Token: {token}");
                    }
                }

                //Entities
                {
                    for (int i = 0; i < message.Entities; i++)
                        Room.Entities.SpawnBuffered(reader);
                }

                Transport.Send(new NetworkPingRequest());
            }

            void ClientConnectHandler(ref ClientConnectMessage message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                var client = RemoteNetworkClient.ReadInstance(in reader);

                Register(client);
            }
            void ClientDisconnectHandler(ref ClientDisconnectMessage message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                Unregister(message.ID);
            }

            void Register(NetworkClient client)
            {
                Debug.Log($"Registerd Client {client}");

#if DEBUG
                if (Collection[client.ID.Value] is not null)
                {
                    NetworkLog.Error($"Client {client} Already Registered");
                    return;
                }
#endif

                Collection[client.ID.Value] = client;
            }
            void Unregister(NetworkClientID id)
            {
                var client = Collection[id.Value];
                if (client is null)
                {
                    NetworkLog.Error($"No Client with ID {id} Found");
                    return;
                }

                Debug.Log($"Unregisterd Client {client}");

                Collection[id.Value] = default;
            }

            readonly RoomInstance Room;
            public ClientsProperty(RoomInstance room)
            {
                this.Room = room;

                Collection = new AutoExpandArray<NetworkClient>(10, NetworkClientID.MaxValue, 10);

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

                public bool Finish()
                {
                    if (Validate() is false)
                        return false;

                    Token = Room.Clients.Local.RemoveSpawnToken();

                    Room.Entities.SpawnLocal(this);

                    var request = new SpawnEntityRequest(Token, Resource);
                    Room.Transport.Send(in request);

                    return true;
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

                instance.SetProperties(id, source, resource);

                instance.Spawn();
                instance.Replicate();

                Register(instance);
            }

            void SpawnLocal(SpawnOptions options)
            {
                var response = InstantiatePrefab(options.Resource);
                if (response.IsError)
                {
                    NetworkLog.Error(response.Error);
                    Room.Shutdown();
                    return;
                }

                var instance = response.Value;

                instance.SetProperties(options.Token, NetworkEntitySource.Prefab, options.Resource);

                instance.Spawn();

                Register(instance);
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

            Transport.Dispatcher.Register((ref NetworkPongResponse data, NetPacketReader reader, byte channel, DeliveryMethod delivery) =>
            {
                NetworkLog.Trace($"Pong from Server");

                Entities.Spawn()
                .SetResource(new NetworkEntityResource(0))
                .Finish();
            });
        }
    }

    public abstract class NetworkClient
    {
        public NetworkClientID ID { get; private set; }
        public string Username { get; private set; }

        public bool IsLocal => this is LocalNetworkClient;

        public static NetworkClientID ReadID(NetPacketReader reader)
        {
            return NetworkSerializer.ReadValue<NetworkClientID>(reader);
        }
        public void ReadState(NetPacketReader reader)
        {
            Username = NetworkSerializer.ReadValue<string>(reader);
        }

        public override string ToString() => $"(ID: {ID}, Username: {Username})";

        public NetworkClient(NetworkClientID id)
        {
            this.ID = id;
        }
    }

    public class RemoteNetworkClient : NetworkClient
    {
        public static RemoteNetworkClient ReadInstance(in NetPacketReader reader)
        {
            var id = ReadID(reader);

            var client = new RemoteNetworkClient(id);

            client.ReadState(reader);

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
        #endregion

        public LocalNetworkClient(NetworkClientID id, int spawnTokenCapacity) : base(id)
        {
            SpawnTokens = new Queue<NetworkEntityID>(spawnTokenCapacity);
        }
    }
}
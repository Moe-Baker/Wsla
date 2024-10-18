using System.Diagnostics.CodeAnalysis;
using System.Reflection.Emit;
using System.Security.Cryptography.X509Certificates;

using LiteNetLib;
using LiteNetLib.Utils;

using MemoryPack;

using Wsla.Shared.Global;

using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Wsla.Server
{
    public class Room
    {
        public string Name { get; }

        public TransportProperty Transport { get; }
        public class TransportProperty
        {
            public NetManager Manager { get; }
            public ushort Port => (ushort)Manager.LocalPort;

            public EventBasedNetListener Listener { get; }

            CancellationTokenSource? CancellationSource;

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

            public DispatcherProperty Dispatcher { get; }
            public class DispatcherProperty
            {
                ActionDelegate[] Handlers;
                public delegate void ActionDelegate(NetworkClient sender, NetPacketReader reader, byte channel, DeliveryMethod delivery);

                void ReceiveCallback(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod delivery)
                {
                    var client = peer.Tag as NetworkClient;
                    if (client is null)
                        throw new Exception("No Client Assigned to Peer");

                    var type = NetworkSerializer.ReadType(in reader);
                    var id = NetworkTypes.Get(type);

                    var handler = Handlers[id];
                    if (handler is null)
                    {
                        NetworkLog.Error($"No Dispatch Handler Provided for {type} Message");
                        return;
                    }

                    handler(client, reader, channel, delivery);
                }

                public delegate void TypeDelegate<T>(NetworkClient sender, ref T message, NetPacketReader reader, byte channel, DeliveryMethod delivery);
                public void Register<T>(TypeDelegate<T> handler)
                {
                    var id = NetworkTypes.Get<T>();

                    Handlers[id] = Surrogate;

                    void Surrogate(NetworkClient sender, NetPacketReader reader, byte channel, DeliveryMethod delivery)
                    {
                        var data = NetworkSerializer.ReadValue<T>(in reader);
                        handler(sender, ref data, reader, channel, delivery);
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

            public void Start()
            {
                if (Manager.Start(Constants.RelayManagementPort) is false)
                    throw new InvalidOperationException($"Can't Start Relay Server on Port {Constants.RelayManagementPort}");

                NetworkLog.Trace($"Starting Room {Room} on Port {Port}");

                CancellationSource = new CancellationTokenSource();

                Poll(CancellationSource.Token);
            }
            public void Stop()
            {
                NetworkLog.Trace($"Stopping Room {Room}");

                CancellationSource?.Cancel();
            }

            public void Send<T>(NetworkClient client, in T data, byte channel = 0, DeliveryMethod delivery = DeliveryMethod.ReliableOrdered)
            {
                var writer = PacketWriter.Take();

                NetworkSerializer.WriteHeader(in writer, data);

                Send(client, writer, channel, delivery);
            }
            public void Send(NetworkClient client, in NetDataWriter writer, byte channel = 0, DeliveryMethod delivery = DeliveryMethod.ReliableOrdered)
            {
                client.Peer.Send(writer, channel, delivery);
            }

            public void Broadcast<T>(in T data, byte channel = 0, DeliveryMethod delivery = DeliveryMethod.ReliableOrdered, NetworkClient? except = null)
            {
                var writer = PacketWriter.Take();

                NetworkSerializer.WriteHeader(in writer, data);

                if (except is null)
                    Manager.SendToAll(writer, channel, delivery);
                else
                    Manager.SendToAll(writer, channel, delivery, except.Peer);
            }
            public void Broadcast(in NetDataWriter writer, byte channel = 0, DeliveryMethod delivery = DeliveryMethod.ReliableOrdered, NetworkClient? except = null)
            {
                if (except is null)
                    Manager.SendToAll(writer, channel, delivery);
                else
                    Manager.SendToAll(writer, channel, delivery, except.Peer);
            }

            public void Kick(NetworkClient client, WslaError error)
            {
                var writer = PacketWriter.Take();

                MemoryPackSerializer.Serialize(in writer, in error);

                client.Peer.Disconnect(writer);
            }

            async void Poll(CancellationToken cancellation)
            {
                while (true)
                {
                    Manager.PollEvents();

                    await Task.Delay(5);

                    if (cancellation.IsCancellationRequested)
                        break;
                }
            }

            readonly Room Room;
            public TransportProperty(Room reference)
            {
                Room = reference;

                Listener = new EventBasedNetListener();

                Manager = new NetManager(Listener);

                PacketWriter = PacketWriterProperty.Create(256);

                Dispatcher = new DispatcherProperty(this);
            }
        }

        public ClientsProperty Clients { get; private set; }
        public class ClientsProperty
        {
            IncrementingKeyGenerator<NetworkClientID> IDGenerator;

            AutoExpandArray<NetworkClient?> Collection;

            public byte Count { get; private set; }

            public bool TryGet(NetworkClientID id, [MaybeNullWhen(returnValue: false)] out NetworkClient client)
            {
                client = Collection[id.Value];
                return client is not null;
            }

            TransportProperty Transport => Room.Transport;

            internal void Start()
            {
                Transport.Listener.ConnectionRequestEvent += RequestHandler;

                Transport.Listener.PeerConnectedEvent += ConnectHandler;
                Transport.Listener.PeerDisconnectedEvent += DisconnectHandler;
            }

            void RequestHandler(ConnectionRequest request)
            {
                NetworkLog.Info($"Connection Request from {request.RemoteEndPoint}");

                var segment = request.Data.GetRemainingBytesSegment();

                ClientConnectionRequest data;

                try
                {
                    data = MemoryPackSerializer.Deserialize<ClientConnectionRequest>(segment);
                }
                catch (Exception)
                {
                    NetworkLog.Warning($"Connection Request From {request.RemoteEndPoint} Couldn't be Deserialized");
                    RejectConnection(request, WslaErrorCode.RequestDeserializationFailure);
                    return;
                }

                NetworkLog.Trace($"Connection Request from {data}");

                //Reserve Client ID
                if (IDGenerator.TryReserve(out var id) is false)
                {
                    NetworkLog.Error($"Room {Room} Client ID Generatror Overloaded, Connection Request Rejected");
                    RejectConnection(request, WslaErrorCode.ClientIDGeneratorOverloaded);

                    return;
                }

                //Reserve Entitiy Spawn Tokens
                if (Room.Entities.IDGenerator.TryReserve(stackalloc NetworkEntityID[Room.Entities.ClientSpawnTokenAllowance], out var spawnTokens) is false)
                {
                    NetworkLog.Error($"Room {Room} Entitiy ID Generatror Overloaded, Connection Request Rejected");
                    RejectConnection(request, WslaErrorCode.EntityIDGeneratorOverloaded);

                    IDGenerator.Return(id);

                    return;
                }

                var peer = request.Accept();

                var client = new NetworkClient(Room, peer, id, data.Username, spawnTokens.Length);

                for (int i = 0; i < spawnTokens.Length; i++)
                    client.AddSpawnToken(spawnTokens[i]);

                peer.Tag = client;
            }
            void RejectConnection(ConnectionRequest request, WslaErrorCode code)
            {
                var writer = Transport.PacketWriter.Take();

                var error = WslaError.From(code);

                MemoryPackSerializer.Serialize(in writer, in error);

                request.Reject(writer);
            }

            void ConnectHandler(NetPeer peer)
            {
                var client = peer.Tag as NetworkClient;
                if (client is null)
                    throw new Exception("No Client Assigned to Peer");

                NetworkLog.Info($"Client {client} Connected");

                Count += 1;

                Collection[client.ID.Value] = client;

                //Broadcast To Others
                {
                    var writer = Transport.PacketWriter.Take();

                    NetworkSerializer.WriteHeader<ClientConnectMessage>(in writer);
                    client.WriteState(in writer);

                    Transport.Broadcast(in writer, except: client);
                }

                //Unicast to Client
                {
                    var writer = Transport.PacketWriter.Take();

                    var message = new ClientConnectionResponse(client.ID, Count, client.SpawnAllowance, Room.Entities.Count);
                    NetworkSerializer.WriteHeader(in writer, in message);

                    //Sync Clients
                    {
                        foreach (var other in Collection.AsSpan())
                        {
                            if (other is null)
                                continue;

                            other.WriteState(in writer);
                        }
                    }

                    //Sync Spawn Tokens
                    {
                        foreach (var token in client.SpawnTokens)
                            NetworkSerializer.WriteValue(in writer, token);
                    }

                    //Sync Entities
                    {
                        foreach (var (id, entity) in Room.Entities.Dictionary)
                            entity.WriteState(in writer);
                    }

                    Transport.Send(client, in writer);
                }
            }
            void DisconnectHandler(NetPeer peer, DisconnectInfo info)
            {
                var client = peer.Tag as NetworkClient;
                if (client is null)
                    throw new Exception("No Client Assigned to Peer");

                Count -= 1;

                NetworkLog.Info($"Client {client} Disconnected");

                Collection[client.ID.Value] = null;

                //Free Client ID
                IDGenerator.Return(client.ID);

                //Free Entity Spawn Tokens
                foreach (var token in client.SpawnTokens)
                    Room.Entities.IDGenerator.Return(token);

                //Broadcast To Others
                {
                    var message = new ClientDisconnectMessage(client.ID);
                    Transport.Broadcast(in message, except: client);
                }
            }

            readonly Room Room;
            public ClientsProperty(Room room)
            {
                this.Room = room;

                IDGenerator = new IncrementingKeyGenerator<NetworkClientID>(new NetworkClientID(1), 10, TimeSpan.FromSeconds(30), NetworkClientID.Increment);

                Collection = new AutoExpandArray<NetworkClient?>(10, NetworkClientID.MaxValue, 10);
            }
        }

        public EntitiesProperty Entities;
        public class EntitiesProperty
        {
            internal IncrementingKeyGenerator<NetworkEntityID> IDGenerator;

            internal Dictionary<NetworkEntityID, NetworkEntity> Dictionary;
            public ushort Count => (ushort)Dictionary.Count;

            public readonly ushort ClientSpawnTokenAllowance = 50;

            TransportProperty Transport => Room.Transport;

            void SpawnRequestHandler(NetworkClient sender, ref SpawnEntityRequest message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                var id = sender.RemoveSpawnToken();

                if (id != message.SpawnToken)
                {
                    NetworkLog.Warning($"Mismatched Spawn Tokens Received from {sender}, Excpected {id} Got {message.SpawnToken}");
                    Transport.Kick(sender, WslaError.From(WslaErrorCode.SpawnTokenContractBroken));
                    return;
                }

                if (IDGenerator.TryReserve(out var replacement) is false)
                {
                    NetworkLog.Error($"Room {Room} ran out of Entity Spawn Tokens");
                    Room.Shutdown();
                    return;
                }

                var entity = new NetworkEntity(id, message.Resource);

                entity.SetProperties(id, NetworkEntitySource.Prefab, message.Resource);

                Register(entity);

                //Respond to Sender
                {
                    var response = new SpawnEntityResponse(id, replacement);
                    Transport.Send(sender, response);
                }

                //Broadcast to Others
                {
                    var writer = Transport.PacketWriter.Take();

                    NetworkSerializer.WriteHeader<SpawnEntityCommand>(in writer);

                    entity.WriteState(in writer);

                    Transport.Broadcast(in writer, except: sender);
                }
            }

            void Register(NetworkEntity entity)
            {
                Dictionary.Add(entity.ID, entity);
            }
            void Unregister(NetworkEntityID id)
            {
                Dictionary.Remove(id);
            }

            readonly Room Room;
            public EntitiesProperty(Room room)
            {
                this.Room = room;

                IDGenerator = new(new NetworkEntityID(1), 40, TimeSpan.FromSeconds(10), NetworkEntityID.Increment);

                Dictionary = new Dictionary<NetworkEntityID, NetworkEntity>(40);

                Transport.Dispatcher.Register<SpawnEntityRequest>(SpawnRequestHandler);
            }
        }

        public void Start()
        {
            Transport.Start();
            Clients.Start();
        }
        public void Stop()
        {
            throw new NotImplementedException();
        }

        public void Shutdown()
        {

        }

        public override string ToString() => $"({Name})";

        public Room(string name)
        {
            this.Name = name;

            Transport = new TransportProperty(this);
            Clients = new ClientsProperty(this);
            Entities = new EntitiesProperty(this);

            Transport.Dispatcher.Register((NetworkClient sender, ref NetworkPingRequest data, NetPacketReader reader, byte channel, DeliveryMethod delivery) =>
            {
                NetworkLog.Trace($"Ping from {sender}");
                Transport.Send(sender, new NetworkPongResponse());
            });
        }
    }

    public class NetworkClient
    {
        public Room Room { get; }
        public NetPeer Peer { get; }

        public NetworkClientID ID { get; }

        public string Username { get; private set; }

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

        public void WriteState(in NetDataWriter writer)
        {
            NetworkSerializer.WriteValue(writer, ID);
            NetworkSerializer.WriteValue(writer, Username);
        }

        public override string ToString() => $"(ID: {ID}, Username: {Username})";

        public NetworkClient(Room room, NetPeer peer, NetworkClientID id, string username, int spawnTokenCapacity)
        {
            this.Room = room;
            this.Peer = peer;

            this.ID = id;
            this.Username = username;

            SpawnTokens = new Queue<NetworkEntityID>(spawnTokenCapacity);
        }
    }

    public class NetworkEntity
    {
        public NetworkEntityID ID { get; private set; }
        public NetworkEntitySource Source { get; private set; }
        public NetworkEntityResource Resource { get; private set; }

        internal void SetProperties(NetworkEntityID ID, NetworkEntitySource Source, NetworkEntityResource Resource)
        {
            this.ID = ID;
            this.Source = Source;
            this.Resource = Resource;
        }

        public void WriteState(in NetDataWriter writer)
        {
            NetworkSerializer.WriteValue(in writer, Source);
            NetworkSerializer.WriteValue(in writer, Resource);
            NetworkSerializer.WriteValue(in writer, ID);
        }

        public NetworkEntity(NetworkEntityID id, NetworkEntityResource resource)
        {
            this.ID = id;
            this.Resource = resource;
        }
    }
}
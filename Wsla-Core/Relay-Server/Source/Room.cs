using System.Diagnostics.CodeAnalysis;

using LiteNetLib;
using LiteNetLib.Utils;

using Wsla.Serialization;

namespace Wsla.Server
{
    public class Room : ThreadDispatcher.IJob
    {
        public string Name { get; }

        ThreadDispatcher.Processor? ThreadProcessor;
        ThreadDispatcher.IJob? ThreadDispatcher.IJob.Next { get; set; }
        ThreadDispatcher.IJob? ThreadDispatcher.IJob.Previous { get; set; }

        public TransportProperty Transport { get; }
        public class TransportProperty
        {
            public NetManager Manager { get; }
            public ushort Port => (ushort)Manager.LocalPort;

            public EventBasedNetListener Listener { get; }

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

                    var id = NetworkTypeSerializationResolver.ReadValue(ref reader);

                    var handler = Handlers[id];
                    if (handler is null)
                    {
                        NetworkLog.Error($"No Dispatch Handler Provided for {NetworkTypes.Get(id)} Message");
                        return;
                    }

                    handler(client, reader, channel, delivery);
                }

                public delegate void TypeDelegate<T>(NetworkClient sender, ref T message, NetPacketReader reader, byte channel, DeliveryMethod delivery);
                public void Register<[NetworkSerializationMarker] T>(TypeDelegate<T> handler)
                {
                    var id = NetworkTypes.Get<T>();

                    Handlers[id] = Surrogate;

                    void Surrogate(NetworkClient sender, NetPacketReader reader, byte channel, DeliveryMethod delivery)
                    {
                        NetworkSerializer.ReadValue(ref reader, out T data);
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
                if (Manager.StartInManualMode(Constants.RelayManagementPort) is false)
                    throw new InvalidOperationException($"Can't Start Relay Server on Port {Constants.RelayManagementPort}");

                NetworkLog.Info($"Starting Room {Room} on Port {Port}");
            }
            public void Stop() { }

            public void Send<[NetworkSerializationMarker] T>(NetworkClient client, in T data, byte channel = 0, DeliveryMethod delivery = DeliveryMethod.ReliableOrdered)
            {
                var writer = PacketWriter.Take();

                NetworkSerializer.WriteHeader(data, ref writer);

                Send(client, writer, channel, delivery);
            }
            public void Send(NetworkClient client, in NetDataWriter writer, byte channel = 0, DeliveryMethod delivery = DeliveryMethod.ReliableOrdered)
            {
                client.Peer.Send(writer, channel, delivery);
            }

            public void Broadcast<[NetworkSerializationMarker] T>(in T data, byte channel = 0, DeliveryMethod delivery = DeliveryMethod.ReliableOrdered, NetworkClient? except = null)
            {
                var writer = PacketWriter.Take();

                NetworkSerializer.WriteHeader(data, ref writer);

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

                NetworkSerializer.WriteValue(in error, ref writer);

                client.Peer.Disconnect(writer);
            }

            internal void Receive()
            {
                Manager.PollEvents();
            }

            internal void Send(TimeSpan elapsed)
            {
                var tick = Convert.ToInt32(elapsed.TotalMilliseconds);
                if (tick <= 0) tick = 1;

                Manager.ManualUpdate(tick);
            }

            readonly Room Room;
            public TransportProperty(Room reference)
            {
                Room = reference;

                Listener = new EventBasedNetListener();

                Manager = new NetManager(Listener);
                Manager.IPv6Enabled = false;

                PacketWriter = PacketWriterProperty.Create(256);

                Dispatcher = new DispatcherProperty(this);
            }
        }

        public ClientsProperty Clients { get; private set; }
        public class ClientsProperty
        {
            IncrementingKeyGenerator<NetworkClientID> IDGenerator;

            ExpandArray<NetworkClient> Collection;

            public byte Count { get; private set; }

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

                var reader = request.Data;

                ClientConnectionRequest data;

                try
                {
                    NetworkSerializer.ReadValue(ref reader, out data);
                }
                catch (Exception)
                {
                    NetworkLog.Warning($"Connection Request From {request.RemoteEndPoint} Couldn't be Deserialized");
                    RejectConnection(request, WslaErrorCode.RequestDeserializationFailure);
                    return;
                }

                NetworkLog.Info($"Connection Request from {data}");

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

                var client = new NetworkClient(Room, id, data.Username, spawnTokens.Length);

                for (int i = 0; i < spawnTokens.Length; i++)
                    client.AddSpawnToken(spawnTokens[i]);

                var peer = request.Accept(client);
            }
            void RejectConnection(ConnectionRequest request, WslaErrorCode code)
            {
                var writer = Transport.PacketWriter.Take();

                var error = WslaError.From(code);

                NetworkSerializer.WriteValue(in error, ref writer);

                request.Reject(writer);
            }

            void ConnectHandler(NetPeer peer)
            {
                var client = peer.Tag as NetworkClient;
                if (client is null)
                    throw new Exception("No Client Assigned to Peer");

                client.AssignPeer(peer);

                NetworkLog.Info($"Client {client} Connected");

                Count += 1;

                Collection.Add(client.ID.Value, client);

                //Broadcast To Others
                {
                    var writer = Transport.PacketWriter.Take();

                    var message = new ClientConnectMessage();

                    NetworkSerializer.WriteHeader(in message, ref writer);

                    client.WriteState(ref writer);

                    Transport.Broadcast(in writer, except: client);
                }

                //Unicast to Client
                {
                    var writer = Transport.PacketWriter.Take();

                    var message = new ClientConnectionResponse(client.ID, Count, client.SpawnAllowance, Room.Scenes.Count, Room.Entities.Count);
                    NetworkSerializer.WriteHeader(in message, ref writer);

                    //Sync Clients
                    WriteState(ref writer);

                    //Sync Spawn Tokens
                    client.WriteSpawnTokens(ref writer);

                    //Sync Scenes
                    Room.Scenes.WriteState(ref writer);

                    //Sync Entities
                    Room.Entities.WriteState(ref writer);

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

                Collection.Remove(client.ID.Value);

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

            void WriteState(ref NetDataWriter writer)
            {
                foreach (var other in Collection)
                    other.WriteState(ref writer);
            }

            readonly Room Room;
            public ClientsProperty(Room room)
            {
                this.Room = room;

                IDGenerator = new IncrementingKeyGenerator<NetworkClientID>(new NetworkClientID(1), 10, TimeSpan.FromSeconds(15), NetworkClientID.Increment);

                Collection = new ExpandArray<NetworkClient>(10, NetworkClientID.MaxValue, 10);
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

                    var command = new SpawnEntityCommand();

                    NetworkSerializer.WriteHeader(in command, ref writer);

                    entity.WriteState(ref writer);

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

            internal void WriteState(ref NetDataWriter writer)
            {
                foreach (var (id, entity) in Dictionary)
                    entity.WriteState(ref writer);
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

        public ScenesProperty Scenes { get; }

        public class ScenesProperty
        {
            public List<NetworkScene> Collection { get; }
            public bool TryFind(NetworkSceneID id, [MaybeNullWhen(returnValue: false)] out NetworkScene target)
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

            public byte Count => (byte)Collection.Count;

            public TransportProperty Transport => Room.Transport;

            void ChangeRequestHandler(NetworkClient sender, ref ChangeScenesRequest message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                ChangeProcedure(message.LoadMode, message.Scenes);

                //Broadcast to Others
                {
                    //TODO: investigate replicating the request as the command since they have the same fields
                    var command = ChangeScenesCommand.From(message);
                    Transport.Broadcast(in command, except: sender);
                }
            }

            void ChangeProcedure(NetworkSceneLoadMode mode, List<NetworkSceneID> ids)
            {
                if (mode is NetworkSceneLoadMode.Single)
                {
                    for (int i = 0; i < Collection.Count; i++)
                        Collection[i].Unload();

                    Collection.Clear();
                }

                for (int i = 0; i < ids.Count; i++)
                {
                    var instance = new NetworkScene(ids[i]);

                    Collection.Add(instance);

                    instance.Load();
                }
            }

            internal void WriteState(ref NetDataWriter writer)
            {
                foreach (var scene in Collection)
                    scene.WriteState(ref writer);
            }

            readonly Room Room;
            public ScenesProperty(Room Room)
            {
                this.Room = Room;

                Collection = new List<NetworkScene>(1);

                Transport.Dispatcher.Register<ChangeScenesRequest>(ChangeRequestHandler);
            }
        }

        public void Start(ThreadDispatcher Dispatcher)
        {
            Transport.Start();
            Clients.Start();

            ThreadProcessor = Dispatcher.Retrieve();
            ThreadProcessor.Register(this);
        }
        public void Stop()
        {
            NetworkLog.Info($"Stopping Room {this}");

            Transport.Stop();
        }

        public void Receive()
        {
            Transport.Receive();
        }
        public void Send(TimeSpan elapsed)
        {
            Transport.Send(elapsed);
        }

        public void Shutdown()
        {

        }

        public override string ToString() => $"({Name})";

        public Room(string Name)
        {
            this.Name = Name;

            Transport = new TransportProperty(this);
            Clients = new ClientsProperty(this);
            Entities = new EntitiesProperty(this);
            Scenes = new ScenesProperty(this);
        }
    }

    public class NetworkClient
    {
        public Room Room { get; }

        public NetPeer? Peer { get; private set; }
        internal void AssignPeer(NetPeer value)
        {
            this.Peer = value;
        }

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

        public void WriteSpawnTokens(ref NetDataWriter writer)
        {
            foreach (var token in SpawnTokens)
                NetworkSerializer.WriteValue(token, ref writer);
        }
        #endregion

        public void WriteState(ref NetDataWriter writer)
        {
            NetworkSerializer.WriteValue(ID, ref writer);
            NetworkSerializer.WriteValue(Username, ref writer);
        }

        public override string ToString() => $"(ID: {ID}, Username: {Username})";

        public NetworkClient(Room Room, NetworkClientID ID, string Username, int SpawnTokenCapacity)
        {
            this.Room = Room;

            this.ID = ID;
            this.Username = Username;

            SpawnTokens = new Queue<NetworkEntityID>(SpawnTokenCapacity);
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

        public void WriteState(ref NetDataWriter writer)
        {
            NetworkSerializer.WriteValue(Source, ref writer);
            NetworkSerializer.WriteValue(Resource, ref writer);
            NetworkSerializer.WriteValue(ID, ref writer);
        }

        public NetworkEntity(NetworkEntityID id, NetworkEntityResource resource)
        {
            this.ID = id;
            this.Resource = resource;
        }
    }

    public class NetworkScene
    {
        public NetworkSceneID ID { get; }

        public void Load()
        {

        }
        public void Unload()
        {

        }

        internal void WriteState(ref NetDataWriter writer)
        {
            NetworkSerializer.WriteValue(ID, ref writer);
        }

        public NetworkScene(NetworkSceneID ID)
        {
            this.ID = ID;
        }
    }
}
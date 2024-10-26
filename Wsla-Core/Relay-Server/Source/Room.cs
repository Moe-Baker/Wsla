using System.Diagnostics.CodeAnalysis;

using LiteNetLib;
using LiteNetLib.Utils;

using Wsla.Serialization;

namespace Wsla.Server
{
    public class Room
    {
        public string Name { get; }

        RoomThreadDispatcher.Processor? ThreadProcessor;
        public Room? Next { get; internal set; }
        public Room? Previous { get; internal set; }

        public GenericPool<NetDataWriter> PackerWriterPool => ThreadProcessor.PacketWritersPool;

        public TransportProperty Transport { get; }
        public class TransportProperty
        {
            public NetManager Manager { get; }
            public ushort Port => (ushort)Manager.LocalPort;

            public EventBasedNetListener Listener { get; }

            public readonly SinglePacketWriter PacketWriter;

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

                    var id = NetworkTypeSerializationResolver.ReadValue(reader);

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
                        NetworkSerializer.ReadValue(reader, out T data);
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

            public void SendData<[NetworkSerializationMarker] T>(NetworkClient client, in T data, byte channel = 0, DeliveryMethod delivery = DeliveryMethod.ReliableOrdered)
            {
                var writer = PacketWriter.Take();
                NetworkSerializer.WriteHeader(data, writer);

                SendWriter(client, writer, channel, delivery);
            }
            public void SendWriter(NetworkClient client, NetDataWriter writer, byte channel = 0, DeliveryMethod delivery = DeliveryMethod.ReliableOrdered)
            {
                client.Peer.Send(writer, channel, delivery);
            }

            public void BroadcastData<[NetworkSerializationMarker] T>(in T data, byte channel = 0, DeliveryMethod delivery = DeliveryMethod.ReliableOrdered, NetworkClient? except = null)
            {
                var writer = PacketWriter.Take();
                NetworkSerializer.WriteHeader(data, writer);

                BroadcastWriter(writer, channel, delivery, except: except);
            }
            public void BroadcastWriter(NetDataWriter writer, byte channel = 0, DeliveryMethod delivery = DeliveryMethod.ReliableOrdered, NetworkClient? except = null)
            {
                if (except is null)
                    Manager.SendToAll(writer, channel, delivery);
                else
                    Manager.SendToAll(writer, channel, delivery, except.Peer);
            }

            public void Kick(NetworkClient client, WslaError error)
            {
                var writer = PacketWriter.Take();

                NetworkSerializer.WriteValue(in error, writer);

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

                PacketWriter = SinglePacketWriter.Create(256);

                Dispatcher = new DispatcherProperty(this);
            }
        }

        public ClientsProperty Clients { get; private set; }
        public class ClientsProperty
        {
            IncrementingKeyGenerator<NetworkClientID> IDGenerator;

            public ExpandArray<NetworkClient> Collection { get; }
            public byte Count => (byte)Collection.Count;
            public bool TryGet(NetworkClientID id, out NetworkClient client) => Collection.TryGet(id.Value, out client);

            public NetworkClient? Master { get; private set; }

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
                    NetworkSerializer.ReadValue(reader, out data);
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

                NetworkSerializer.WriteValue(in error, writer);

                request.Reject(writer);
            }

            void ConnectHandler(NetPeer peer)
            {
                var client = peer.Tag as NetworkClient;
                if (client is null)
                    throw new Exception("No Client Assigned to Peer");

                client.AssignPeer(peer);

                NetworkLog.Info($"Client {client} Connected");

                Collection.Add(client.ID.Value, client);

                if (Master is null)
                    Master = client;

                //Broadcast To Others
                {
                    var writer = Transport.PacketWriter.Take();

                    var message = new ClientConnectMessage();

                    NetworkSerializer.WriteHeader(in message, writer);

                    client.WriteState(writer);

                    Transport.BroadcastWriter(writer, except: client);
                }

                //Unicast to Client
                {
                    var writer = Transport.PacketWriter.Take();

                    var message = new ClientConnectionResponse(client.ID, Master.ID, Count, client.SpawnAllowance, Room.Scenes.Count, Room.Entities.Count);
                    NetworkSerializer.WriteHeader(in message, writer);

                    //Sync Clients
                    WriteState(writer);

                    //Sync Spawn Tokens
                    client.WriteSpawnTokens(writer);

                    //Sync Scenes
                    Room.Scenes.WriteState(writer);

                    //Sync Entity Definitions
                    Room.Entities.WriteDefinitions(writer);

                    Transport.SendWriter(client, writer);
                }
            }
            void DisconnectHandler(NetPeer peer, DisconnectInfo info)
            {
                var client = peer.Tag as NetworkClient;
                if (client is null)
                    throw new Exception("No Client Assigned to Peer");

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
                    Transport.BroadcastData(in message, except: client);
                }
            }

            void WriteState(NetDataWriter writer)
            {
                foreach (var other in Collection)
                    other.WriteState(writer);
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

            public Dictionary<NetworkEntityID, NetworkEntity> Dictionary { get; }
            public ushort Count => (ushort)Dictionary.Count;

            public bool TryGet(NetworkEntityID id, [NotNullWhen(true)] out NetworkEntity entity) => Dictionary.TryGetValue(id, out entity);

            public readonly ushort ClientSpawnTokenAllowance = 50;

            TransportProperty Transport => Room.Transport;

            void SpawnRequestHandler(NetworkClient sender, ref SpawnEntityRequest message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                if (sender.ValdiateSpawnToken(message.SpawnToken) is false)
                {
                    NetworkLog.Warning($"Invalid Spawn Token {message.SpawnToken} Received from {sender}");
                    Transport.Kick(sender, WslaError.From(WslaErrorCode.SpawnTokenContractBroken));
                    return;
                }

                var owner = ChooseOwner(sender, message.Authority);
                if (ChooseScene(message.Lifetime, message.Scene, out var scene) is false)
                {
                    NetworkLog.Warning($"Client {sender} Trying to Spawn Entity on UnLoaded Scene, No Handing Implementation");
                    //TODO: Despawn entity on client side
                    throw new NotImplementedException();
                }

                if (IDGenerator.TryReserve(out var replacement) is false)
                {
                    NetworkLog.Error($"Room {Room} ran out of Entity Spawn Tokens");
                    Room.Shutdown();
                    return;
                }

                var entity = new NetworkEntity(Room, message.SpawnToken, NetworkEntityOrigin.Prefab, message.Resource, owner, scene, Authority: message.Authority, message.Lifetime);

                Register(entity);

                //Respond to Sender
                {
                    var response = new SpawnEntityResponse(entity.ID, replacement);
                    Transport.SendData(sender, response);
                }

                //Broadcast to Others
                {
                    var writer = Transport.PacketWriter.Take();

                    var command = new SpawnEntityCommand();

                    NetworkSerializer.WriteHeader(in command, writer);

                    entity.WriteDefinition(writer);

                    Transport.BroadcastWriter(writer, except: sender);
                }
            }

            NetworkClient ChooseOwner(NetworkClient sender, NetworkEntityAuthorityMode authority)
            {
                switch (authority)
                {
                    case NetworkEntityAuthorityMode.Distributable:
                        return ChooseDistributableOwner();

                    case NetworkEntityAuthorityMode.Authoritative:
                        return Room.Clients.Master;

                    case NetworkEntityAuthorityMode.Explicit:
                        return sender;

                    case NetworkEntityAuthorityMode.Transferable:
                        return sender;

                    case NetworkEntityAuthorityMode.Requestable:
                        return sender;

                    default: throw new NotImplementedException();
                }
            }

            bool ChooseScene(NetworkEntityLifetimeMode lifetime, NetworkSceneID id, out NetworkScene? scene)
            {
                if (lifetime is NetworkEntityLifetimeMode.Persistent)
                {
                    scene = default;
                    return true;
                }

                return Room.Scenes.TryFind(id, out scene);
            }

            /// <summary>
            /// Chooses the client with the least ammount of Entities
            /// </summary>
            /// <returns></returns>
            /// <exception cref="InvalidOperationException"></exception>
            NetworkClient ChooseDistributableOwner()
            {
                (NetworkClient? Client, int Entities) Marker = (default, int.MinValue);

                foreach (var client in Room.Clients.Collection)
                {
                    if (client.Entities.Count < Marker.Entities)
                        Marker = (client, Marker.Entities);
                }

                if (Marker.Client is null)
                    throw new InvalidOperationException($"No Network Client Found to Handle Distributable Ownership");

                return Marker.Client;
            }

            void Register(NetworkEntity entity)
            {
                Dictionary.Add(entity.ID, entity);

                entity.Owner.RegisterEntity(entity);

                if (entity.Scene is not null)
                    entity.Scene.RegisterEntity(entity);
            }
            void Unregister(NetworkEntityID id)
            {
                Dictionary.Remove(id);
            }

            void Transfer(NetworkEntity entity, NetworkClient to)
            {
                var from = entity.Owner;

                from.UnregisterEntity(entity);
                to.RegisterEntity(entity);

                entity.AssignOwner(to);
            }

            internal void WriteDefinitions(NetDataWriter writer)
            {
                foreach (var (id, entity) in Dictionary)
                    entity.WriteDefinition(writer);
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

        public RpcProperty RPCs { get; private set; }
        public class RpcProperty
        {
            TransportProperty Transport => Room.Transport;

            void BroadcastRequestHandler(NetworkClient sender, ref BroadcastNetworkRpcRequest message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                if (Room.Entities.TryGet(message.Parameters.Entity, out var entity) is false)
                {
                    NetworkLog.Warning($"Client {sender} Sent RPC {message.Parameters} for Non-Existing Entity");
                    return;
                }

                var writer = (message.Buffer is RemoteBufferMode.None) ? Transport.PacketWriter.Take() : Room.PackerWriterPool.Retrieve();

                Transport.BroadcastWriter(writer, channel: channel, delivery: delivery, except: sender);
            }

            void BufferRequestHandler(NetworkClient sender, ref BufferNetworkRpcRequest message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                if (Room.Entities.TryGet(message.Parameters.Entity, out var entity) is false)
                {
                    NetworkLog.Warning($"Client {sender} Sent RPC {message.Parameters} for Non-Existing Entity");
                    return;
                }

                var writer = Room.PackerWriterPool.Retrieve();

                Transport.BroadcastWriter(writer, channel: channel, delivery: delivery, except: sender);
            }

            void TargetRequestHandler(NetworkClient sender, ref TargetNetworkRpcRequest message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                if (Room.Entities.TryGet(message.Parameters.Entity, out var entity) is false)
                {
                    NetworkLog.Warning($"Client {sender} Sent RPC {message.Parameters} for Non-Existing Entity");
                    return;
                }

                var writer = Transport.PacketWriter.Take();

                WriteCommand(sender, message.Parameters, reader, writer);

                Transport.SendWriter(sender, writer);
            }

            void WriteCommand(NetworkClient sender, NetworkRpcParameters parameters, NetPacketReader arguments, NetDataWriter destination)
            {
                var command = new NetworkRpcCommand(sender.ID, parameters);

                NetworkSerializer.WriteHeader(in command, destination);

                //Write Arguments
                {
                    var source = arguments.GetRemainingBytesSpan();
                    var buffer = destination.Take(source.Length);

                    source.CopyTo(buffer);
                }
            }

            readonly Room Room;
            public RpcProperty(Room Room)
            {
                this.Room = Room;

                Transport.Dispatcher.Register<BroadcastNetworkRpcRequest>(BroadcastRequestHandler);
                Transport.Dispatcher.Register<BufferNetworkRpcRequest>(BufferRequestHandler);
                Transport.Dispatcher.Register<TargetNetworkRpcRequest>(TargetRequestHandler);
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
                    Transport.BroadcastData(in command, except: sender);
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

            internal void WriteState(NetDataWriter writer)
            {
                foreach (var scene in Collection)
                    scene.WriteState(writer);
            }

            readonly Room Room;
            public ScenesProperty(Room Room)
            {
                this.Room = Room;

                Collection = new List<NetworkScene>(1);

                Transport.Dispatcher.Register<ChangeScenesRequest>(ChangeRequestHandler);
            }
        }

        public void Start(RoomThreadDispatcher Dispatcher)
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
            RPCs = new RpcProperty(this);
        }
    }

    public class NetworkClient
    {
        public Room Room { get; }

        public NetworkClientID ID { get; }

        public string Username { get; private set; }

        public NetPeer? Peer { get; private set; }
        internal void AssignPeer(NetPeer value)
        {
            this.Peer = value;
        }

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

        public bool ValdiateSpawnToken(NetworkEntityID id)
        {
            if (SpawnTokens.TryPeek(out var registerd) is false)
                return false;

            if (registerd != id)
                return false;

            RemoveSpawnToken();
            return true;
        }

        public void WriteSpawnTokens(NetDataWriter writer)
        {
            foreach (var token in SpawnTokens)
                NetworkSerializer.WriteValue(token, writer);
        }
        #endregion

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

        public void WriteState(NetDataWriter writer)
        {
            NetworkSerializer.WriteValue(ID, writer);
            NetworkSerializer.WriteValue(Username, writer);
        }

        public override string ToString() => $"(ID: {ID}, Username: {Username})";

        public NetworkClient(Room Room, NetworkClientID ID, string Username, int SpawnTokenCapacity)
        {
            this.Room = Room;

            this.ID = ID;
            this.Username = Username;

            SpawnTokens = new Queue<NetworkEntityID>(SpawnTokenCapacity);

            Entities = new ExpandList<NetworkEntity>();
        }
    }

    public class NetworkEntity
    {
        public Room Room { get; }

        public NetworkEntityID ID { get; }
        public NetworkEntityOrigin Origin { get; }
        public NetworkEntityResource Resource { get; }

        public NetworkClient Owner { get; private set; }
        public int OwnerRegisteration;

        public NetworkEntityAuthorityMode Authority { get; private set; }
        public NetworkEntityLifetimeMode Lifetime { get; private set; }

        public NetworkScene Scene { get; }
        public int SceneRegisteration;

        public void AssignOwner(NetworkClient target)
        {
            Owner = target;
        }

        public void WriteDefinition(NetDataWriter writer)
        {
            var ownerID = Owner.ID;
            var sceneID = (Scene?.ID).GetValueOrDefault();
            var definition = new NetworkEntityDefinition(ID, Origin, Resource, Authority, Lifetime, ownerID, sceneID);

            NetworkSerializer.WriteValue(in definition, writer);
        }

        public NetworkEntity(Room Room, NetworkEntityID ID, NetworkEntityOrigin Origin, NetworkEntityResource Resource, NetworkClient Owner, NetworkScene Scene, NetworkEntityAuthorityMode Authority, NetworkEntityLifetimeMode Lifetime)
        {
            this.Room = Room;
            this.ID = ID;

            this.Origin = Origin;
            this.Resource = Resource;

            this.Owner = Owner;
            this.Scene = Scene;

            this.Authority = Authority;
            this.Lifetime = Lifetime;
        }
    }

    public class NetworkScene
    {
        public NetworkSceneID ID { get; }

        #region Entities
        public ExpandList<NetworkEntity> Entities { get; }

        public void RegisterEntity(NetworkEntity target)
        {
            target.SceneRegisteration = Entities.Add(target);
        }
        public void UnregisterEntity(NetworkEntity target)
        {
            Entities.RemoveAt(target.SceneRegisteration);
        }
        #endregion

        public void Load()
        {

        }
        public void Unload()
        {

        }

        internal void WriteState(NetDataWriter writer)
        {
            NetworkSerializer.WriteValue(ID, writer);
        }

        public NetworkScene(NetworkSceneID ID)
        {
            this.ID = ID;

            Entities = new ExpandList<NetworkEntity>(40);
        }
    }
}
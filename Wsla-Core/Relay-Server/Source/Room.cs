using System.Diagnostics.CodeAnalysis;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

using LiteNetLib;
using LiteNetLib.Utils;

using Wsla.Serialization;

namespace Wsla.Server
{
    public class Room : IDisposable
    {
        public string Name { get; }

        RoomThreadDispatcher.Processor? ThreadProcessor;
        public Room? Next { get; internal set; }
        public Room? Previous { get; internal set; }

        public RoomThreadDispatcher.Processor.PoolsProperty Pools => ThreadProcessor.Pools;

        public TransportProperty Transport { get; }
        public class TransportProperty
        {
            public NetManager Manager { get; }
            public ushort Port => (ushort)Manager.LocalPort;

            public EventBasedNetListener Listener { get; }

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
                var writer = Room.Pools.SinglePackerWriter.Take();
                NetworkSerializer.WriteHeader(data, writer);

                SendWriter(client, writer, channel, delivery);
            }
            public void SendWriter(NetworkClient client, NetDataWriter writer, byte channel = 0, DeliveryMethod delivery = DeliveryMethod.ReliableOrdered)
            {
                client.Peer.Send(writer, channel, delivery);
            }

            public void BroadcastData<[NetworkSerializationMarker] T>(in T data, byte channel = 0, DeliveryMethod delivery = DeliveryMethod.ReliableOrdered, NetworkClient? except = null)
            {
                var writer = Room.Pools.SinglePackerWriter.Take();
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
                var writer = Room.Pools.SinglePackerWriter.Take();

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
                Manager.DisconnectTimeout = 240 * 1000;
                Manager.IPv6Enabled = false;

                Dispatcher = new DispatcherProperty(this);
            }
        }

        public ClientsProperty Clients { get; private set; }
        public class ClientsProperty : IDisposable
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
                var writer = Room.Pools.SinglePackerWriter.Take();

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
                    var writer = Room.Pools.SinglePackerWriter.Take();

                    var message = new ClientConnectMessage();

                    NetworkSerializer.WriteHeader(in message, writer);

                    client.WriteState(writer);

                    Transport.BroadcastWriter(writer, except: client);
                }

                //Unicast to Client
                {
                    var writer = Room.Pools.SinglePackerWriter.Take();

                    var message = new ClientConnectionResponse(client.ID, Master.ID, Count, client.SpawnAllowance, Room.Entities.Count);
                    NetworkSerializer.WriteHeader(in message, writer);

                    Room.WriteState(client, writer);

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

                client.Dispose();
            }

            internal void WriteState(NetDataWriter writer)
            {
                foreach (var other in Collection)
                    other.WriteState(writer);
            }

            public void Dispose()
            {
                foreach (var client in Collection)
                    client.Dispose();
            }

            readonly Room Room;
            public ClientsProperty(Room room)
            {
                this.Room = room;

                IDGenerator = new IncrementingKeyGenerator<NetworkClientID>(NetworkClientID.Min, NetworkClientID.Max, 10, TimeSpan.FromSeconds(15), NetworkClientID.Increment);

                Collection = new ExpandArray<NetworkClient>(10, NetworkClientID.Max.Value, 10);
            }
        }

        public EntitiesProperty Entities;
        public class EntitiesProperty : IDisposable
        {
            internal IncrementingKeyGenerator<NetworkEntityID> IDGenerator;

            public Dictionary<NetworkEntityID, NetworkEntity> Dictionary { get; }
            public ushort Count => (ushort)Dictionary.Count;

            public bool TryGet(NetworkEntityID id, [NotNullWhen(true)] out NetworkEntity entity) => Dictionary.TryGetValue(id, out entity);

            public readonly ushort ClientSpawnTokenAllowance = 50;

            TransportProperty Transport => Room.Transport;

            void SpawnPrefabRequestHandler(NetworkClient sender, ref SpawnPrefabEntityRequest message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                if (message.Authority is NetworkEntityAuthorityMode.Authoritative && sender.IsMaster is false)
                {
                    NetworkLog.Warning($"Client {sender} isn't Master Client and Can't Spawn {NetworkEntityAuthorityMode.Authoritative} Entities");
                    Transport.Kick(sender, WslaError.From(WslaErrorCode.NoAuthority));
                    return;
                }

                if (message.Scene != Room.Scene.Version)
                {
                    NetworkLog.Warning($"Late Entity Spawn Request from {sender} for Scene Version {message.Scene}, Scene was Already Changed");
                    return;
                }

                if (sender.ValdiateSpawnToken(message.SpawnToken) is false)
                {
                    NetworkLog.Warning($"Invalid Spawn Token {message.SpawnToken} Received from {sender}");
                    Transport.Kick(sender, WslaError.From(WslaErrorCode.SpawnTokenContractBroken));
                    return;
                }

                if (IDGenerator.TryReserve(out var replacement) is false)
                {
                    NetworkLog.Error($"Room {Room} ran out of Entity Spawn Tokens");
                    Room.Shutdown();
                    return;
                }

                var entity = new NetworkEntity(Room, message.SpawnToken, NetworkEntityOrigin.Prefab, message.Resource, sender, message.Authority);

                Register(entity);

                //Respond to Sender
                {
                    var response = new SpawnPrefabEntityResponse(entity.ID, replacement);
                    Transport.SendData(sender, response);
                }

                //Broadcast to Others
                {
                    var writer = Room.Pools.SinglePackerWriter.Take();

                    var command = new SpawnPrefabEntityCommand(entity.ID, entity.Resource, entity.Authority, entity.Owner.ID);
                    NetworkSerializer.WriteHeader(in command, writer);

                    Transport.BroadcastWriter(writer, except: sender);
                }
            }

            /// <summary>
            /// Chooses the client with the least ammount of Entities
            /// </summary>
            /// <returns></returns>
            /// <exception cref="InvalidOperationException"></exception>
            internal NetworkClient ChooseDistributableOwner()
            {
                (NetworkClient? Client, int Entities) Marker = (default, int.MaxValue);

                foreach (var client in Room.Clients.Collection)
                {
                    if (client.Entities.Count < Marker.Entities)
                        Marker = (client, Marker.Entities);
                }

                if (Marker.Client is null)
                    throw new InvalidOperationException($"No Network Client Found to Handle Distributable Ownership");

                return Marker.Client;
            }

            internal void Register(NetworkEntity entity)
            {
                Dictionary.Add(entity.ID, entity);
                entity.Owner.RegisterEntity(entity);
            }
            internal void Unregister(NetworkEntityID id)
            {
                if (Dictionary.TryGetValue(id, out var entity) is false)
                    NetworkLog.Error($"No Entity with ID {id} Found");
            }
            internal void Unregister(NetworkEntity entity)
            {
                Dictionary.Remove(entity.ID);
                entity.Owner.UnregisterEntity(entity);

                entity.Dispose();
            }

            void Transfer(NetworkEntity entity, NetworkClient to)
            {
                var from = entity.Owner;

                from.UnregisterEntity(entity);
                to.RegisterEntity(entity);

                entity.AssignOwner(to);
            }

            internal void WriteDefinitions(NetDataWriter writer, out (ushort variables, ushort rpcs) buffer)
            {
                buffer = (0, 0);

                foreach (var (id, entity) in Dictionary)
                {
                    if (entity.RpcBuffer.Count > 0)
                        buffer.rpcs += 1;

                    if (entity.VariableBuffer.Count > 0)
                        buffer.variables += 1;

                    entity.WriteDefinition(writer);
                }
            }

            public void Dispose()
            {
                foreach (var (id, client) in Dictionary)
                    client.Dispose();
            }

            readonly Room Room;
            public EntitiesProperty(Room room)
            {
                this.Room = room;

                IDGenerator = new(NetworkEntityID.Min, NetworkEntityID.Max, 40, TimeSpan.FromSeconds(10), NetworkEntityID.Increment);

                Dictionary = new Dictionary<NetworkEntityID, NetworkEntity>(40);

                Transport.Dispatcher.Register<SpawnPrefabEntityRequest>(SpawnPrefabRequestHandler);
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

                if (message.Buffer is RemoteBufferMode.Buffer)
                    entity.RpcBuffer.Register(message.Parameters.Behaviour, message.Parameters.RPC, reader);

                //Send to Others
                {
                    var writer = Room.Pools.SinglePackerWriter.Take();
                    WriteCommand(sender, ref message.Parameters, reader, writer);
                    Transport.BroadcastWriter(writer, channel: channel, delivery: delivery, except: sender);
                }
            }

            void BufferRequestHandler(NetworkClient sender, ref BufferNetworkRpcRequest message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                if (Room.Entities.TryGet(message.Parameters.Entity, out var entity) is false)
                {
                    NetworkLog.Warning($"Client {sender} Sent RPC {message.Parameters} for Non-Existing Entity");
                    return;
                }

                entity.RpcBuffer.Register(message.Parameters.Behaviour, message.Parameters.RPC, reader);
            }

            void TargetRequestHandler(NetworkClient sender, ref TargetNetworkRpcRequest message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                if (Room.Entities.TryGet(message.Parameters.Entity, out var entity) is false)
                {
                    NetworkLog.Warning($"Client {sender} Sent RPC {message.Parameters} to Non-Existing Entity");
                    return;
                }

                if (Room.Clients.TryGet(message.Target, out var target) is false)
                {
                    NetworkLog.Warning($"Client {sender} Sent RPC {message.Parameters} to Non-Existing Client");
                    return;
                }

                //Send to Target
                {
                    var writer = Room.Pools.SinglePackerWriter.Take();
                    WriteCommand(sender, ref message.Parameters, reader, writer);
                    Transport.SendWriter(target, writer);
                }
            }

            void WriteCommand(NetworkClient sender, ref NetworkRpcParameters parameters, NetPacketReader input, NetDataWriter output)
            {
                var command = new NetworkRpcCommand(sender.ID, parameters);

                NetworkSerializer.WriteHeader(in command, output);

                //Write Arguments
                {
                    var source = input.PeekAvailableSpan();
                    var destination = output.PopSpan(source.Length);
                    source.CopyTo(destination);
                }
            }

            internal void WriteState(NetDataWriter writer, ushort count)
            {
                NetworkSerializer.WriteValue(in count, writer);

                if (count is 0)
                    return;

                foreach (var (id, entity) in Room.Entities.Dictionary)
                    entity.RpcBuffer.WriteState(id, writer);
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

        public VariablesProperty Variables { get; private set; }
        public class VariablesProperty
        {
            TransportProperty Transport => Room.Transport;

            void BroadcastRequestHandler(NetworkClient sender, ref BroadcastNetworkVariableRequest message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                if (Room.Entities.TryGet(message.Parameters.Entity, out var entity) is false)
                {
                    NetworkLog.Warning($"Client {sender} Sent Variable {message.Parameters} for Non-Existing Entity");
                    return;
                }

                entity.VariableBuffer.Register(message.Parameters.Behaviour, message.Parameters.Variable, reader);

                //Send to Others
                {
                    var writer = Room.Pools.SinglePackerWriter.Take();
                    WriteCommand(sender, ref message.Parameters, reader, writer);
                    Transport.BroadcastWriter(writer, channel: channel, delivery: delivery, except: sender);
                }
            }

            void BufferRequestHandler(NetworkClient sender, ref BufferNetworkVariableRequest message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                if (Room.Entities.TryGet(message.Parameters.Entity, out var entity) is false)
                {
                    NetworkLog.Warning($"Client {sender} Sent RPC {message.Parameters} for Non-Existing Entity");
                    return;
                }

                entity.VariableBuffer.Register(message.Parameters.Behaviour, message.Parameters.Variable, reader);
            }

            void WriteCommand(NetworkClient sender, ref NetworkVariableParameters parameters, NetPacketReader input, NetDataWriter output)
            {
                var command = new NetworkVariableCommand(sender.ID, parameters);

                NetworkSerializer.WriteHeader(in command, output);

                //Write Value
                {
                    var source = input.PeekAvailableSpan();
                    var destination = output.PopSpan(source.Length);
                    source.CopyTo(destination);
                }
            }

            internal void WriteState(NetDataWriter writer, ushort count)
            {
                NetworkSerializer.WriteValue(in count, writer);

                if (count is 0)
                    return;

                foreach (var (id, entity) in Room.Entities.Dictionary)
                    entity.VariableBuffer.WriteState(id, writer);
            }

            readonly Room Room;
            public VariablesProperty(Room Room)
            {
                this.Room = Room;

                Transport.Dispatcher.Register<BroadcastNetworkVariableRequest>(BroadcastRequestHandler);
                Transport.Dispatcher.Register<BufferNetworkVariableRequest>(BufferRequestHandler);
            }
        }

        public SceneProperty Scene { get; }
        public class SceneProperty
        {
            public NetworkSceneID ID { get; private set; }
            public NetworkSceneVersion Version { get; private set; }

            public TransportProperty Transport => Room.Transport;

            void ChangeRequestHandler(NetworkClient sender, ref ChangeSceneRequest message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                if (sender.IsMaster is false)
                {
                    NetworkLog.Warning($"Client {sender} Tried Changing Scenes without Being Master Client");
                    Transport.Kick(sender, WslaError.From(WslaErrorCode.NoAuthority));
                    return;
                }

                ChangeProcedure(message.Scene);

                //Broadcast to All
                {
                    var command = new ChangeSceneCommand(ID, Version);
                    Transport.BroadcastData(in command);
                }
            }
            void ChangeProcedure(NetworkSceneID target)
            {
                ID = target;
                Version = NetworkSceneVersion.Increment(Version);
            }

            void SpawnRequestHandler(NetworkClient sender, ref SpawnScenenRequest message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                if (sender.IsMaster is false)
                {
                    NetworkLog.Warning($"Client {sender} Trying to Spawn Scene Objects While not Being the Master Client");
                    Transport.Kick(sender, WslaError.From(WslaErrorCode.NoAuthority));
                    return;
                }

                var count = NetworkSerializer.ReadValue<byte>(reader);

                if (Room.Entities.IDGenerator.TryReserve(stackalloc NetworkEntityID[count], out var ids) is false)
                {
                    NetworkLog.Error($"Room {Room} Entitiy ID Generatror Overloaded");
                    Room.Shutdown();
                    return;
                }

                var entities = Room.Pools.EntityList.Take();

                for (byte i = 0; i < count; i++)
                {
                    var resource = new NetworkEntityResource(i);
                    var authority = NetworkSerializer.ReadValue<NetworkEntityAuthorityMode>(reader);
                    var owner = (authority is NetworkEntityAuthorityMode.Authoritative) ? Room.Clients.Master : Room.Entities.ChooseDistributableOwner();

                    var entity = new NetworkEntity(Room, ids[i], NetworkEntityOrigin.Scene, resource, owner, authority);

                    Room.Entities.Register(entity);

                    entities.Add(entity);
                }

                //Broadcast to Others
                {
                    var writer = Room.Pools.SinglePackerWriter.Take();

                    var command = new SpawnSceneCommand();
                    NetworkSerializer.WriteHeader(in command, writer);

                    foreach (var entity in entities)
                    {
                        NetworkSerializer.WriteValue(entity.ID, writer);

                        if (entity.Authority is not NetworkEntityAuthorityMode.Authoritative)
                            NetworkSerializer.WriteValue(entity.Owner.ID, writer);
                    }

                    Transport.BroadcastWriter(writer);
                }
            }

            internal void WriteState(NetDataWriter writer)
            {
                NetworkSerializer.WriteValue(ID, writer);
                NetworkSerializer.WriteValue(Version, writer);
            }

            readonly Room Room;
            public SceneProperty(Room Room)
            {
                this.Room = Room;

                ID = new NetworkSceneID(1);

                Transport.Dispatcher.Register<ChangeSceneRequest>(ChangeRequestHandler);
                Transport.Dispatcher.Register<SpawnScenenRequest>(SpawnRequestHandler);
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

        void WriteState(NetworkClient client, NetDataWriter writer)
        {
            //Sync Clients
            Clients.WriteState(writer);

            //Sync Spawn Tokens
            client.WriteSpawnTokens(writer);

            //Sync Scene
            Scene.WriteState(writer);

            //Sync Entity Definitions
            Entities.WriteDefinitions(writer, out var buffer);

            //Sync Variables
            Variables.WriteState(writer, buffer.variables);

            //Sync RPCs
            RPCs.WriteState(writer, buffer.rpcs);
        }

        public void Shutdown()
        {

        }

        public override string ToString() => $"({Name})";

        public void Dispose()
        {
            Clients.Dispose();
            Entities.Dispose();
        }

        public Room(string Name)
        {
            this.Name = Name;

            Transport = new TransportProperty(this);
            Clients = new ClientsProperty(this);
            Entities = new EntitiesProperty(this);
            Scene = new SceneProperty(this);
            RPCs = new RpcProperty(this);
            Variables = new VariablesProperty(this);
        }
    }

    public class NetworkClient : IDisposable
    {
        public NetworkClientID ID { get; }

        public string Username { get; private set; }

        public NetPeer? Peer { get; private set; }
        internal void AssignPeer(NetPeer value)
        {
            this.Peer = value;
        }

        public bool IsMaster => Room.Clients.Master == this;

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

        public void Dispose()
        {

        }

        readonly Room Room;
        public NetworkClient(Room Room, NetworkClientID ID, string Username, int SpawnTokenCapacity)
        {
            this.Room = Room;

            this.ID = ID;
            this.Username = Username;

            SpawnTokens = new Queue<NetworkEntityID>(SpawnTokenCapacity);

            Entities = new(0);
        }
    }

    public class NetworkEntity : IDisposable
    {
        public NetworkEntityID ID { get; }
        public NetworkEntityOrigin Origin { get; }
        public NetworkEntityResource Resource { get; }

        public NetworkClient Owner { get; private set; }
        public int OwnerRegisteration;

        public NetworkEntityAuthorityMode Authority { get; private set; }

        internal RemoteSyncBufferCollection<NetworkRpcID> RpcBuffer;
        internal RemoteSyncBufferCollection<NetworkVariableID> VariableBuffer;

        public void Dispose()
        {
            RpcBuffer.Dispose();
            VariableBuffer.Dispose();
        }

        public void AssignOwner(NetworkClient target)
        {
            Owner = target;
        }

        public void WriteDefinition(NetDataWriter writer)
        {
            var definition = new NetworkEntityDefinition(ID, Origin, Resource, Authority, Owner.ID);
            NetworkSerializer.WriteValue(in definition, writer);
        }

        readonly Room Room;
        public NetworkEntity(Room Room, NetworkEntityID ID, NetworkEntityOrigin Origin, NetworkEntityResource Resource, NetworkClient Owner, NetworkEntityAuthorityMode Authority)
        {
            this.Room = Room;
            this.ID = ID;

            this.Origin = Origin;
            this.Resource = Resource;

            this.Owner = Owner;

            this.Authority = Authority;

            RpcBuffer = new(Room);
            VariableBuffer = new(Room);
        }
    }

    public struct RemoteSyncBufferCollection<TMember> : IDisposable
        where TMember : unmanaged, IEquatable<TMember>, IRemoteSyncMemberID
    {
        Dictionary<Key, Payload>? Collection;
        public readonly struct Key : IEquatable<Key>
        {
            public NetworkBehaviourID Behaviour { get; }
            public TMember Member { get; }

            public override bool Equals([NotNullWhen(true)] object? obj)
            {
                if (obj is Key other)
                    return Equals(other);

                return false;
            }
            public bool Equals(Key other)
            {
                return Behaviour == other.Behaviour && Member.Equals(other.Member);
            }

            readonly int Hashcode;
            public override int GetHashCode() => Hashcode;

            public Key(NetworkBehaviourID Behaviour, TMember Member)
            {
                this.Behaviour = Behaviour;
                this.Member = Member;

                Hashcode = (Behaviour.Value << 4) | (Member.Value);
            }
        }
        public readonly struct Payload
        {
            public NetDataWriter? Stream { get; }

            public Payload(NetDataWriter? Stream)
            {
                this.Stream = Stream;
            }
        }

        public ushort Count => Collection is null ? (ushort)0 : (ushort)Collection.Count;

        public void Register(NetworkBehaviourID Behaviour, TMember Member, NetDataReader Input)
        {
            if (Collection is null)
                Collection = new(1);

            var key = new Key(Behaviour, Member);

            ref var payload = ref CollectionsMarshal.GetValueRefOrAddDefault(Collection, key, out var exists);

            if (exists is false)
            {
                if (Input.AvailableBytes is 0)
                {
                    payload = new Payload(default);
                }
                else
                {
                    var writer = Room.Pools.MultiPackerWriter.Retrieve();
                    payload = new Payload(writer);
                }
            }

            //Copy Buffer
            if (Input.AvailableBytes > 0 && payload.Stream is not null)
            {
                var source = Input.PeekAvailableSpan();

                payload.Stream.SetPosition(0);
                var destination = payload.Stream.PopSpan(source.Length);

                source.CopyTo(destination);
            }
        }

        public void WriteState(NetworkEntityID entity, NetDataWriter output)
        {
            if (Collection is null)
                return;

            NetworkSerializer.WriteValue(in entity, output);

            NetworkSerializer.WriteValue(Count, output);

            foreach (var (key, payload) in Collection)
            {
                //Write Key
                {
                    NetworkSerializer.WriteValue(key.Behaviour, output);
                    NetworkSerializer.WriteValue(key.Member, output);
                }

                //Write Payload
                if (payload.Stream is not null)
                {
                    var source = payload.Stream.PeekAllocatedSpan();
                    var destination = output.PopSpan(source.Length);
                    source.CopyTo(destination);
                }
            }
        }

        public void Dispose()
        {
            if (Collection is null)
                return;

            foreach (var (key, payload) in Collection)
            {
                if (payload.Stream is null)
                    continue;

                Room.Pools.MultiPackerWriter.Return(payload.Stream);
            }
        }

        readonly Room Room;
        public RemoteSyncBufferCollection(Room Room)
        {
            this.Room = Room;

            Collection = null;
        }
    }
}
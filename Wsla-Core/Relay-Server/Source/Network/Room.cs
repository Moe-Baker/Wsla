using System.Diagnostics.CodeAnalysis;

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
        internal RoomThreadDispatcher.Processor.PoolsProperty Pools => ThreadProcessor.Pools;

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
            }
            public void Stop()
            {
                Manager.Stop(false);
            }

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
                Manager.DisconnectTimeout = (int)Constants.Timeout.TotalMilliseconds;
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
                var client = RetrieveFromPeer(peer);

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
                var client = RetrieveFromPeer(peer);

                NetworkLog.Info($"Client {client} Disconnected");

                Collection.Remove(client.ID.Value);

                if (Collection.Count is 0) //Last Client, Shutdown Room
                {
                    Room.Stop();
                }
                else
                {
                    //Free Client ID
                    IDGenerator.Return(client.ID);

                    //Free Entity Spawn Tokens
                    foreach (var token in client.SpawnTokens)
                        Room.Entities.IDGenerator.Return(token);

                    //Check Master Client
                    if (client == Master)
                        ReplaceMaster();

                    //Replicate
                    {
                        var writer = Room.Pools.SinglePackerWriter.Take();

                        var message = new ClientDisconnectMessage(client.ID);
                        NetworkSerializer.WriteHeader(in message, writer);

                        foreach (var entity in client.Entities)
                        {
                            if (entity.Authority is NetworkEntityAuthorityMode.Transferable)
                                NetworkSerializer.WriteValue(entity.ID, writer);

                            Room.Entities.Despawn(entity);
                        }

                        Transport.BroadcastWriter(writer, except: client);
                    }

                    client.Dispose();
                }
            }

            NetworkClient RetrieveFromPeer(NetPeer peer)
            {
                var client = peer.Tag as NetworkClient;
                if (client is null)
                    throw new Exception($"No Client Assigned to Peer {peer}");

                return client;
            }

            void ReplaceMaster()
            {
                var previous = Master;
                Master = ChooseMaster();
                var current = Master;

                Room.Entities.SwapAuthortative(previous, current);

                //Broadcast to All
                {
                    var change = new ChangeMasterClientCommand(Master.ID);
                    Transport.BroadcastData(change);
                }
            }
            NetworkClient ChooseMaster()
            {
                foreach (var client in Collection)
                    return client;

                throw new InvalidOperationException($"No Registerd Clients to Choose From");
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

                Transport.Listener.ConnectionRequestEvent += RequestHandler;
                Transport.Listener.PeerConnectedEvent += ConnectHandler;
                Transport.Listener.PeerDisconnectedEvent += DisconnectHandler;
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

                    //Respond to Sender, Necessary to send them back their spawn token to reuse
                    {
                        var response = new SpawnPrefabEntityResponse(message.SpawnToken, message.SpawnToken);
                        Transport.SendData(sender, response);
                    }

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
                    Room.Stop();
                    return;
                }

                var entity = new NetworkEntity(Room, message.SpawnToken, NetworkEntityOrigin.Prefab, message.Resource, sender, message.Authority);

                //Read Trait if any
                entity.AssignTrait(reader, reader.Available);

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

                    entity.WriteTrait(writer);

                    Transport.BroadcastWriter(writer, except: sender);
                }
            }

            void DespawnRequestHandler(NetworkClient sender, ref DespawnEntityRequest message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                if (Dictionary.TryGetValue(message.ID, out var entity) is false)
                {
                    NetworkLog.Warning($"Late Entity Despawn Request from {sender} for Entity {message.ID}, Entity was Already Despawned");
                    return;
                }

                if (entity.Authority is NetworkEntityAuthorityMode.Authoritative && sender.IsMaster is false)
                {
                    NetworkLog.Warning($"Client {sender} isn't Master Client and Can't Despawn {NetworkEntityAuthorityMode.Authoritative} Entities");
                    Transport.Kick(sender, WslaError.From(WslaErrorCode.NoAuthority));
                    return;
                }

                //Broadcast to Others
                {
                    var command = new DespawnEntityCommand(entity.ID);
                    Transport.BroadcastData(in command, except: sender);
                }

                Despawn(entity);
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

            internal void Despawn(NetworkEntity entity) => Unregister(entity);

            internal void SwapAuthortative(NetworkClient from, NetworkClient to)
            {
                foreach (var entity in from.Entities)
                {
                    if (entity.Authority is not NetworkEntityAuthorityMode.Authoritative)
                        continue;

                    Transfer(entity, to);
                }
            }

            internal void Transfer(NetworkEntity entity, NetworkClient to)
            {
                var from = entity.Owner;

                from.UnregisterEntity(entity);
                to.RegisterEntity(entity);

                entity.TransferOwner(to);
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
                Transport.Dispatcher.Register<DespawnEntityRequest>(DespawnRequestHandler);
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

            public bool IsSpawned { get; private set; }

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
                IsSpawned = false;
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
                    Room.Stop();
                    return;
                }

                var entities = Room.Pools.EntityList.Take();

                for (byte i = 0; i < count; i++)
                {
                    var resource = new NetworkEntityResource(i);
                    var authority = NetworkSerializer.ReadValue<NetworkEntityAuthorityMode>(reader);
                    var entity = new NetworkEntity(Room, ids[i], NetworkEntityOrigin.Scene, resource, sender, authority);

                    Room.Entities.Register(entity);

                    entities.Add(entity);
                }

                IsSpawned = true;

                //Broadcast to Others
                {
                    var writer = Room.Pools.SinglePackerWriter.Take();

                    var command = new SpawnSceneCommand();
                    NetworkSerializer.WriteHeader(in command, writer);

                    foreach (var entity in entities)
                        NetworkSerializer.WriteValue(entity.ID, writer);

                    Transport.BroadcastWriter(writer);
                }
            }

            internal void WriteState(NetDataWriter writer)
            {
                NetworkSerializer.WriteValue(ID, writer);
                NetworkSerializer.WriteValue(Version, writer);
                NetworkSerializer.WriteValue(IsSpawned, writer);
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
            NetworkLog.Info($"Starting Room {this}");

            Transport.Start();

            ThreadProcessor = Dispatcher.Retrieve();
            ThreadProcessor.Register(this);
        }
        public void Stop()
        {
            NetworkLog.Info($"Stopping Room {this}");

            Transport.Stop();

            ThreadProcessor.Unregister(this);

            Dispose();
        }

        public void Receive() => Transport.Receive();
        public void Send(TimeSpan elapsed) => Transport.Send(elapsed);

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
}
using System.Diagnostics.CodeAnalysis;

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

        public ClientsPropert Clients { get; private set; }
        public class ClientsPropert
        {
            IncrementingKeyGenerator<NetworkClientID> IDGenerator;

            AutoExpandArray<NetworkClient?> Collection;

            public bool TryGet(NetworkClientID id, [MaybeNullWhen(returnValue: false)] out NetworkClient client)
            {
                client = Collection[id.Value];
                return client is not null;
            }

            TransportProperty Transport => Room.Transport;

            internal void Start()
            {
                Transport.Listener.ConnectionRequestEvent += ConnectionRequestHandler;

                Transport.Listener.PeerConnectedEvent += ConnectHandler;
                Transport.Listener.PeerDisconnectedEvent += DisconnectHandler;
            }

            void ConnectionRequestHandler(ConnectionRequest request)
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

                if (IDGenerator.TryReserve(out var id) is false)
                {
                    NetworkLog.Error($"Room {Room} Client ID Generatror Overloaded, Connection Request Rejected");
                    RejectConnection(request, WslaErrorCode.ClientIDGeneratorOverloaded);
                    return;
                }

                var peer = request.Accept();

                peer.Tag = new NetworkClient(Room, peer, id, data.Username);
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

                Collection[client.ID.Value] = client;

                //Broadcast To Others
                {
                    var data = client.GetData();
                    var message = new ClientConnectEvent(data);

                    Transport.Broadcast(in message, except: client);
                }

                //Unicast to Client
                {
                    var writer = Transport.PacketWriter.Take();

                    var message = new ClientConnectionResponse(client.ID);
                    NetworkSerializer.WriteHeader(in writer, in message);

                    foreach (var other in Collection.AsSpan())
                    {
                        if (other is null)
                            continue;

                        var data = other.GetData();

                        NetworkSerializer.WriteValue(in writer, in data);
                    }

                    Transport.Send(client, in writer);
                }
            }
            void DisconnectHandler(NetPeer peer, DisconnectInfo info)
            {
                var client = peer.Tag as NetworkClient;
                if (client is null)
                    throw new Exception("No Client Assigned to Peer");

                NetworkLog.Info($"Client {client} Disconnected");

                Collection[client.ID.Value] = null;

                IDGenerator.Return(client.ID);

                //Broadcast To Others
                {
                    var message = new ClientDisconnectEvent(client.ID);
                    Transport.Broadcast(in message, except: client);
                }
            }

            readonly Room Room;
            public ClientsPropert(Room room)
            {
                this.Room = room;

                IDGenerator = new IncrementingKeyGenerator<NetworkClientID>(10, TimeSpan.FromSeconds(30), NetworkClientID.Increment);

                Collection = new AutoExpandArray<NetworkClient?>(10, NetworkClientID.MaxValue, 10);
            }
        }

        public void Start()
        {
            Transport.Start();
            Clients.Start();
        }

        public override string ToString() => $"({Name})";

        public Room(string name)
        {
            this.Name = name;

            Transport = new TransportProperty(this);
            Clients = new ClientsPropert(this);

            Transport.Dispatcher.Register((NetworkClient sender, ref NetworkPingEvent data, NetPacketReader reader, byte channel, DeliveryMethod delivery) =>
            {
                NetworkLog.Trace($"Ping from {sender}");
                Transport.Send(sender, new NetworkPongEvent());
            });
        }
    }

    public class NetworkClient
    {
        public Room Room { get; }
        public NetPeer Peer { get; }

        public NetworkClientID ID { get; }

        public string Username { get; private set; }

        public NetworkClientData GetData() => new NetworkClientData(ID, Username);

        public override string ToString() => $"(ID: {ID}, Username: {Username})";

        public NetworkClient(Room room, NetPeer peer, NetworkClientID id, string username)
        {
            this.Room = room;
            this.Peer = peer;

            this.ID = id;
            this.Username = username;
        }
    }
}
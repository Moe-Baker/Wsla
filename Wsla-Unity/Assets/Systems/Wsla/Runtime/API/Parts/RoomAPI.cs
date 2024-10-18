using System;
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
            public NetworkClient Local { get; private set; }

            AutoExpandArray<NetworkClient> Collection;

            TransportProperty Transport => Room.Transport;

            void ConnectResponseHandler(ref ClientConnectionResponse message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                while (reader.EndOfData is false)
                {
                    var data = NetworkSerializer.ReadValue<NetworkClientData>(in reader);
                    var client = new NetworkClient(data, data.ID == message.LocalID);

                    if (client.IsLocal)
                        Local = client;

                    Register(client);
                }

                //Check if we didn't recieve our local client
                if (Local is null)
                    throw new InvalidOperationException("No Local Client Received in Response");

                Transport.Send(new NetworkPingEvent());
            }

            void ClientConnectHandler(ref ClientConnectEvent message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
            {
                var client = new NetworkClient(message.Data, false);
                Register(client);
            }
            void ClientDisconnectHandler(ref ClientDisconnectEvent message, NetPacketReader reader, byte channel, DeliveryMethod delivery)
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
                Transport.Dispatcher.Register<ClientConnectEvent>(ClientConnectHandler);
                Transport.Dispatcher.Register<ClientDisconnectEvent>(ClientDisconnectHandler);
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

        public RoomInstance(IPAddress address, ushort port)
        {
            Transport = new TransportProperty(this, address, port);
            Clients = new ClientsProperty(this);

            Transport.Dispatcher.Register((ref NetworkPongEvent data, NetPacketReader reader, byte channel, DeliveryMethod delivery) =>
            {
                NetworkLog.Trace($"Pong from Server");
            });
        }
    }

    public class NetworkClient
    {
        public NetworkClientID ID { get; }
        public string Username { get; }

        public bool IsLocal { get; }

        public override string ToString() => $"(ID: {ID}, Username: {Username})";

        public NetworkClient(NetworkClientData data, bool isLocal)
        {
            ID = data.ID;
            Username = data.Username;

            this.IsLocal = isLocal;
        }
    }
}
using LiteNetLib;

using MemoryPack;

using Wsla.Shared;

namespace Wsla.Server
{
    public class Room
    {
        public string Name { get; }

        public TransportProperty Transport { get; }
        public class TransportProperty
        {
            NetManager Manager;
            EventBasedNetListener Listener;

            CancellationTokenSource? CancellationSource;

            public ushort Port => (ushort)Manager.LocalPort;

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

            async void Poll(CancellationToken cancellation)
            {
                while (true)
                {
                    Manager.PollEvents();

                    await Task.Delay(20);

                    if (cancellation.IsCancellationRequested)
                        break;
                }
            }

            void PeerConnectionRequest(ConnectionRequest request)
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
                    request.Reject();
                    return;
                }

                NetworkLog.Trace($"Connection Request from {data}");

                var peer = request.Accept();
            }

            void PeerConnectedCallback(NetPeer peer)
            {
                NetworkLog.Info($"Peer {peer.Id} Connected");
            }
            void PeerDisconnectedCallback(NetPeer peer, DisconnectInfo info)
            {
                NetworkLog.Info($"Peer {peer.Id} Disconnected");
            }

            Room Room;
            public TransportProperty(Room reference)
            {
                Room = reference;

                Listener = new EventBasedNetListener();

                Listener.ConnectionRequestEvent += PeerConnectionRequest;
                Listener.PeerConnectedEvent += PeerConnectedCallback;
                Listener.PeerDisconnectedEvent += PeerDisconnectedCallback;

                Manager = new NetManager(Listener);
            }
        }

        public void Start()
        {
            Transport.Start();
        }

        public override string ToString() => Name;

        public Room(string name)
        {
            this.Name = name;

            Transport = new TransportProperty(this);
        }
    }
}
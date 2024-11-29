using LiteNetLib.Utils;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using Wsla.Serialization;

namespace Wsla
{
    public abstract class MessagingSocket
    {
        public Socket Socket { get; }

        public bool IsConnected { get; protected set; }

        public readonly CancellationTokenSource CancellationSource;
        public CancellationToken CancellationToken => CancellationSource.Token;

        public const int LengthHeaderSize = 2;

        protected virtual void Start()
        {
            IsConnected = true;
        }
        protected virtual async void Stop()
        {
            if (IsConnected is false)
                return;

            IsConnected = false;

            CancellationSource.Cancel();

            NetworkLog.Trace($"Stopping {this}");

            try
            {
                Socket.Shutdown(SocketShutdown.Both);

                await DisconnectAsync(Socket);
            }
            catch (SocketException ex)
            {
                NetworkLog.Error($"Socket Exception: {ex.ErrorCode} | {ex.SocketErrorCode} | {ex.NativeErrorCode}");
            }
            finally
            {
                Socket.Close();
            }
        }

        #region Send
        readonly SemaphoreSlim SendLock;

        readonly NetDataWriter SendBuffer;

        public async void Send<[NetworkSerializationMarker] T>(T data)
        {
            try
            {
                await SendLock.WaitAsync(CancellationToken);

                ArraySegment<byte> LengthBuffer;

                //Allocate Length
                {
                    LengthBuffer = new ArraySegment<byte>(SendBuffer.Data, SendBuffer.Position, LengthHeaderSize);
                    SendBuffer.Position += LengthHeaderSize;
                }

                NetworkSerializer.WriteHeader(in data, SendBuffer);

                //Write Length
                {
                    var length = (ushort)(SendBuffer.Position - LengthHeaderSize);

                    if (BitConverter.TryWriteBytes(LengthBuffer, length) is false)
                        throw new NotImplementedException();
                }

                var memory = SendBuffer.Data.AsMemory(0, SendBuffer.Position);
                await Socket.SendAsync(memory, SocketFlags.None, CancellationToken);
            }
            catch (SocketException ex)
            {
                NetworkLog.Error($"Socket Send Exception: {ex.ErrorCode} | {ex.SocketErrorCode} | {ex.NativeErrorCode}");
                Stop();
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            finally
            {
                SendLock.Release();
                SendBuffer.Reset();
            }
        }
        #endregion

        public override string ToString() => $"({Socket.LocalEndPoint} | {Socket.RemoteEndPoint})";

        protected MessagingSocket() : this(CreateSocket()) { }
        protected MessagingSocket(Socket Socket)
        {
            this.Socket = Socket;

            SendLock = new SemaphoreSlim(1);
            SendBuffer = new NetDataWriter(true, 128);

            CancellationSource = new CancellationTokenSource();
        }

        public static Socket CreateSocket() => new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true
        };

        public static async ValueTask DisconnectAsync(Socket socket)
        {
            var operation = new TaskCompletionSource<bool>();

            var args = new SocketAsyncEventArgs();

            args.DisconnectReuseSocket = false;
            args.Completed += (sender, args) => operation.TrySetResult(true);

            if (socket.DisconnectAsync(args) is false)
                return;

            await operation.Task;
        }
    }
    public abstract class MessagingConnection : MessagingSocket
    {
        protected override void Start()
        {
            base.Start();

            Receive();
        }

        #region Receive
        readonly NetDataWriter ReceivePacket;
        int ReceiveReadLength;

        protected async void Receive()
        {
            while (true)
            {
                try
                {
                    ReceivePacket.EnsureFit(100);

                    var memory = ReceivePacket.Data.AsMemory(ReceivePacket.Position);
                    var read = await Socket.ReceiveAsync(memory, SocketFlags.None, CancellationToken);

                    if (read == 0)
                    {
                        Stop();
                        break;
                    }

                    HandleReceive(read);
                }
                catch (SocketException ex)
                {
                    NetworkLog.Error($"Socket Receive Exception: {ex.ErrorCode} | {ex.SocketErrorCode} | {ex.NativeErrorCode}");
                    Stop();
                    return;
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        void HandleReceive(int read)
        {
            ReceiveReadLength += read;

            for (int counter = 1; /* No Condition */ ; counter++)
            {
                if (TryRead() is false)
                {
                    if (counter > 1)
                        AlignReceiveBuffer();

                    break;
                }
            }
        }

        bool TryRead()
        {
            ushort length;

            //Read Length
            {
                var buffer = ReceivePacket.PopSpan(LengthHeaderSize);
                length = BitConverter.ToUInt16(buffer);
            }

            if (length + LengthHeaderSize > ReceiveReadLength)
            {
                ReceivePacket.Position -= LengthHeaderSize;
                return false;
            }

            ReceiveReadLength -= LengthHeaderSize + length;

            DispatchMessage(ReceivePacket);

            return true;
        }

        protected abstract void DispatchMessage(NetDataWriter writer);

        unsafe void AlignReceiveBuffer()
        {
            var remaining = ReceivePacket.GetSpan(ReceivePacket.Position, ReceiveReadLength);

            fixed (byte* ptr = ReceivePacket.Data)
            {
                Buffer.MemoryCopy((ptr + ReceivePacket.Position), ptr, ReceivePacket.Data.Length, ReceiveReadLength);
            }

            ReceivePacket.Position = 0;
        }
        #endregion

        public MessagingConnection(Socket socket) : base(socket)
        {
            ReceivePacket = new NetDataWriter(true, 128);
        }
    }

    public class MessagingQuery : MessagingSocket, IDisposable
    {
        public async Task<WslaResponse<WslaError>> Connect(IPAddress address, ushort port)
        {
            try
            {
                await Socket.ConnectAsync(address, port);
            }
            catch (Exception)
            {
                return WslaError.From(WslaErrorCode.TransportFailure);
            }

            Start();

            return WslaResponse<WslaError>.Success;
        }
        public void Disconnect() => Stop();

        void IDisposable.Dispose() => Disconnect();

        #region Receive
        readonly NetDataWriter ReceivePacket;
        int ReceiveReadLength;

        public async Task<WslaResponse<T, WslaError>> Receive<[NetworkSerializationMarker] T>()
        {
            try
            {
                while (true)
                {
                    if (TryRead<T>(out var data))
                    {
                        AlignReceiveBuffer();
                        return data;
                    }

                    ReceivePacket.EnsureFit(100);

                    var memory = ReceivePacket.Data.AsMemory(ReceivePacket.Position);
                    var read = await Socket.ReceiveAsync(memory, SocketFlags.None, CancellationToken);

                    ReceiveReadLength += read;
                }
            }
            catch (SocketException ex)
            {
                NetworkLog.Error($"Socket Receive Exception: {ex.ErrorCode} | {ex.SocketErrorCode} | {ex.NativeErrorCode}");
                Stop();
                throw;
            }
        }

        bool TryRead<T>(out WslaResponse<T, WslaError> response)
        {
            ushort length;

            //Read Length
            {
                var buffer = ReceivePacket.PopSpan(LengthHeaderSize);
                length = BitConverter.ToUInt16(buffer);
            }

            if (length + LengthHeaderSize > ReceiveReadLength)
            {
                ReceivePacket.Position -= LengthHeaderSize;
                response = default;
                return false;
            }

            ReceiveReadLength -= LengthHeaderSize + length;

            var type = NetworkSerializer.ReadValue<Type>(ReceivePacket);

            if (type == typeof(T))
                response = NetworkSerializer.ReadValue<T>(ReceivePacket);
            else if (type == typeof(WslaError))
                response = NetworkSerializer.ReadValue<WslaError>(ReceivePacket);
            else
                throw new InvalidCastException($"Cannot Read Packet for Type {type} as {typeof(T)}");

            return true;
        }

        unsafe void AlignReceiveBuffer()
        {
            var remaining = ReceivePacket.GetSpan(ReceivePacket.Position, ReceiveReadLength);

            fixed (byte* ptr = ReceivePacket.Data)
            {
                Buffer.MemoryCopy((ptr + ReceivePacket.Position), ptr, ReceivePacket.Data.Length, ReceiveReadLength);
            }

            ReceivePacket.Position = 0;
        }
        #endregion

        public MessagingQuery()
        {
            ReceivePacket = new NetDataWriter(true, 128);
        }
    }

    public class MessagingClient : MessagingConnection
    {
        public DispatcherProperty Dispatcher { get; }
        public class DispatcherProperty
        {
            ActionDelegate[] Handlers;
            public delegate void ActionDelegate(NetDataWriter reader);

            public void Dispatch(NetDataWriter reader)
            {
                var id = NetworkTypeSerializationResolver.ReadValue(reader);

                var handler = Handlers[id];
                if (handler is null)
                {
                    NetworkLog.Error($"No Dispatch Handler Provided for {NetworkTypes.Get(id)} Message");
                    return;
                }

                handler(reader);
            }

            public delegate void TypeDelegate<T>(ref T message);
            public void Register<[NetworkSerializationMarker] T>(TypeDelegate<T> handler)
            {
                var id = NetworkTypes.Get<T>();

                Handlers[id] = Surrogate;

                void Surrogate(NetDataWriter reader)
                {
                    var data = NetworkSerializer.ReadValue<T>(reader);
                    handler(ref data);
                }
            }

            public DispatcherProperty()
            {
                Handlers = new ActionDelegate[NetworkTypes.Capacity];
            }
        }

        public async Task Connect(IPAddress address, ushort port)
        {
            await Socket.ConnectAsync(address, port);

            Start();
        }
        public void Disconnect() => Stop();

        protected override void DispatchMessage(NetDataWriter writer) => Dispatcher.Dispatch(writer);

        public MessagingClient() : base(CreateSocket())
        {
            Dispatcher = new DispatcherProperty();
        }
    }

    public class MessagingServer : MessagingSocket
    {
        List<MessagingPeer> Peers;

        public DispatcherProperty Dispatcher { get; }
        public class DispatcherProperty
        {
            ActionDelegate[] Handlers;
            public delegate void ActionDelegate(MessagingPeer peer, NetDataWriter packet);

            public void Dispatch(MessagingPeer peer, NetDataWriter packet)
            {
                var id = NetworkTypeSerializationResolver.ReadValue(packet);

                var handler = Handlers[id];
                if (handler is null)
                {
                    NetworkLog.Error($"No Dispatch Handler Provided for {NetworkTypes.Get(id)} Message");
                    return;
                }

                handler(peer, packet);
            }

            public delegate void TypeDelegate<T>(MessagingPeer peer, ref T message);
            public void Register<[NetworkSerializationMarker] T>(TypeDelegate<T> handler)
            {
                var id = NetworkTypes.Get<T>();

                Handlers[id] = Surrogate;

                void Surrogate(MessagingPeer peer, NetDataWriter reader)
                {
                    var data = NetworkSerializer.ReadValue<T>(reader);
                    handler(peer, ref data);
                }
            }

            public DispatcherProperty()
            {
                Handlers = new ActionDelegate[NetworkTypes.Capacity];
            }
        }

        public void Start(int port) => Start(IPAddress.Any, port);
        public void Start(IPAddress address, int port)
        {
            var endpoint = new IPEndPoint(address, port);
            Socket.Bind(endpoint);

            Socket.Listen(100);

            Poll();
        }

        public void End() => Stop();

        public async void Poll()
        {
            while (true)
            {
                var socket = await Socket.AcceptAsync();
                var peer = new MessagingPeer(socket, this);

                lock (Peers)
                {
                    Peers.Add(peer);
                }
            }
        }

        internal void Remove(MessagingPeer peer)
        {
            lock (Peers)
            {
                Peers.Remove(peer);
            }
        }

        public MessagingServer()
        {
            Peers = new List<MessagingPeer>();

            Dispatcher = new DispatcherProperty();
        }
    }
    public class MessagingPeer : MessagingConnection
    {
        public void Disconnect()
        {
            if (IsConnected is false)
                return;

            Stop();
        }

        protected override void Stop()
        {
            base.Stop();

            Server.Remove(this);
        }

        protected override void DispatchMessage(NetDataWriter packet) => Server.Dispatcher.Dispatch(this, packet);

        readonly MessagingServer Server;
        internal MessagingPeer(Socket Socket, MessagingServer Server) : base(Socket)
        {
            this.Server = Server;

            Start();
        }
    }
}
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
        public Socket Socket { get; private set; }

        public bool IsConnected { get; protected set; }

        public CancellationTokenSource CancellationSource { get; private set; }
        public CancellationToken GetCancellationToken() => CancellationSource.Token;

        public const int LengthHeaderSize = 2;

        protected void CreateSocket()
        {
            var Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true,
            };

            AssignSocket(Socket);
        }
        protected void AssignSocket(Socket Socket)
        {
            this.Socket = Socket;
        }

        protected virtual void Start()
        {
            IsConnected = true;

            CancellationSource = new CancellationTokenSource();
        }
        protected virtual async ValueTask Stop()
        {
            if (IsConnected is false)
                return;

            IsConnected = false;

            CancellationSource.Cancel();

            NetworkLog.Trace($"Stopping {this}");

            try
            {
                Socket.Shutdown(SocketShutdown.Both);

                await DisconnectAsync(Socket, false);
            }
            catch (SocketException ex)
            {
                NetworkLog.Error($"Socket Exception: {ex.ErrorCode} | {ex.SocketErrorCode} | {ex.NativeErrorCode}");
            }
            finally
            {
                Reset();
            }
        }

        protected virtual void Reset()
        {
            Socket.Close();

            Socket = default;
            CancellationSource = default;

            SendBuffer.Reset();
        }

        #region Send
        readonly SemaphoreSlim SendLock;

        readonly NetDataWriter SendBuffer;

        public async void Send<[NetworkSerializationMarker] T>(T data)
        {
            var cancellation = GetCancellationToken();

            try
            {
                await SendLock.WaitAsync(cancellation);

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
                await Socket.SendAsync(memory, SocketFlags.None, cancellation);
            }
            catch (SocketException ex)
            {
                NetworkLog.Error($"Socket Send Exception: {ex.ErrorCode} | {ex.SocketErrorCode} | {ex.NativeErrorCode}");
                await Stop();
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

        protected MessagingSocket()
        {
            SendLock = new SemaphoreSlim(1);
            SendBuffer = new NetDataWriter(true, 128);
        }

        public static async ValueTask DisconnectAsync(Socket socket, bool reuse)
        {
            var operation = new TaskCompletionSource<bool>();

            var args = new SocketAsyncEventArgs();

            args.DisconnectReuseSocket = reuse;
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
        readonly NetDataWriter ReceiveBuffer;
        int ReceiveReadLength;

        protected async void Receive()
        {
            var cancellation = GetCancellationToken();

            while (true)
            {
                try
                {
                    ReceiveBuffer.EnsureFit(100);

                    var memory = ReceiveBuffer.Data.AsMemory(ReceiveBuffer.Position);
                    var read = await Socket.ReceiveAsync(memory, SocketFlags.None, cancellation);

                    if (read == 0)
                    {
                        await Stop();
                        return;
                    }

                    HandleReceive(read);
                }
                catch (SocketException ex)
                {
                    NetworkLog.Error($"Socket Receive Exception: {ex.ErrorCode} | {ex.SocketErrorCode} | {ex.NativeErrorCode}");
                    await Stop();
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
                var buffer = ReceiveBuffer.PopSpan(LengthHeaderSize);
                length = BitConverter.ToUInt16(buffer);
            }

            if (length + LengthHeaderSize > ReceiveReadLength)
            {
                ReceiveBuffer.Position -= LengthHeaderSize;
                return false;
            }

            ReceiveReadLength -= LengthHeaderSize + length;

            DispatchMessage(ReceiveBuffer);

            return true;
        }

        protected abstract void DispatchMessage(NetDataWriter writer);

        unsafe void AlignReceiveBuffer()
        {
            var remaining = ReceiveBuffer.GetSpan(ReceiveBuffer.Position, ReceiveReadLength);

            fixed (byte* ptr = ReceiveBuffer.Data)
            {
                Buffer.MemoryCopy((ptr + ReceiveBuffer.Position), ptr, ReceiveBuffer.Data.Length, ReceiveReadLength);
            }

            ReceiveBuffer.Position = 0;
        }
        #endregion

        protected override void Reset()
        {
            base.Reset();

            ReceiveBuffer.Reset();
        }


        protected MessagingConnection() : base()
        {
            ReceiveBuffer = new NetDataWriter(true, 128);
        }
    }

    public class MessagingQuery : MessagingSocket, IDisposable
    {
        public async Task<WslaResponse<WslaError>> Connect(IPAddress address, ushort port)
        {
            CreateSocket();

            try
            {
                await Socket.ConnectAsync(address, port);
            }
            catch (Exception ex)
            {
                NetworkLog.Error($"Exception: {ex}");

                return WslaError.From(WslaErrorCode.TransportFailure);
            }

            Start();

            return WslaResponse<WslaError>.Success;
        }
        public ValueTask Disconnect() => Stop();

        void IDisposable.Dispose() => Disconnect();

        #region Receive
        readonly NetDataWriter ReceiveBuffer;
        int ReceiveReadLength;

        public async Task<WslaResponse<T, WslaError>> Receive<[NetworkSerializationMarker] T>()
        {
            var cancellation = GetCancellationToken();

            try
            {
                while (true)
                {
                    if (TryRead<T>(out var data))
                    {
                        AlignReceiveBuffer();
                        return data;
                    }

                    ReceiveBuffer.EnsureFit(100);

                    var memory = ReceiveBuffer.Data.AsMemory(ReceiveBuffer.Position);
                    var read = await Socket.ReceiveAsync(memory, SocketFlags.None, cancellation);

                    ReceiveReadLength += read;
                }
            }
            catch (SocketException ex)
            {
                NetworkLog.Error($"Socket Receive Exception: {ex.ErrorCode} | {ex.SocketErrorCode} | {ex.NativeErrorCode}");
                await Stop();
                throw;
            }
        }

        bool TryRead<T>(out WslaResponse<T, WslaError> response)
        {
            ushort length;

            //Read Length
            {
                var buffer = ReceiveBuffer.PopSpan(LengthHeaderSize);
                length = BitConverter.ToUInt16(buffer);
            }

            if (length + LengthHeaderSize > ReceiveReadLength)
            {
                ReceiveBuffer.Position -= LengthHeaderSize;
                response = default;
                return false;
            }

            ReceiveReadLength -= LengthHeaderSize + length;

            var type = NetworkSerializer.ReadValue<Type>(ReceiveBuffer);

            if (type == typeof(T))
                response = NetworkSerializer.ReadValue<T>(ReceiveBuffer);
            else if (type == typeof(WslaError))
                response = NetworkSerializer.ReadValue<WslaError>(ReceiveBuffer);
            else
                throw new InvalidCastException($"Cannot Read Packet for Type {type} as {typeof(T)}");

            return true;
        }

        unsafe void AlignReceiveBuffer()
        {
            var remaining = ReceiveBuffer.GetSpan(ReceiveBuffer.Position, ReceiveReadLength);

            fixed (byte* ptr = ReceiveBuffer.Data)
            {
                Buffer.MemoryCopy((ptr + ReceiveBuffer.Position), ptr, ReceiveBuffer.Data.Length, ReceiveReadLength);
            }

            ReceiveBuffer.Position = 0;
        }
        #endregion

        public async Task<WslaResponse<TResponse, WslaError>> Transport<[NetworkSerializationMarker] TRequest, [NetworkSerializationMarker] TResponse>(IPAddress address, ushort port, TRequest request)
        {
            //Connect
            {
                var response = await Connect(address, port);

                if (response.IsError)
                    return response.Error;
            }

            //Send, Receive & Disconnect
            {
                Send(request);

                var response = await Receive<TResponse>();

                await Disconnect();

                return response;
            }
        }

        protected override void Reset()
        {
            base.Reset();

            ReceiveBuffer.Reset();
        }

        public MessagingQuery()
        {
            ReceiveBuffer = new NetDataWriter(true, 128);
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

        public async Task<WslaResponse<WslaError>> Connect(IPAddress address, ushort port)
        {
            CreateSocket();

            try
            {
                await Socket.ConnectAsync(address, port);
            }
            catch (Exception ex)
            {
                NetworkLog.Error($"Exception: {ex}");

                return WslaError.From(WslaErrorCode.TransportFailure);
            }

            Start();

            return WslaResponse<WslaError>.Success;
        }
        public ValueTask Disconnect() => Stop();

        protected override void DispatchMessage(NetDataWriter writer) => Dispatcher.Dispatch(writer);

        public MessagingClient() : base()
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
            CreateSocket();

            var endpoint = new IPEndPoint(address, port);
            Socket.Bind(endpoint);

            Socket.Listen(100);

            Poll();
        }

        public async void Poll()
        {
            while (true)
            {
                var socket = await Socket.AcceptAsync();
                var peer = new MessagingPeer(this, socket);

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

        public ValueTask End() => Stop();

        protected override void Reset()
        {
            base.Reset();

            lock (Peers)
            {
                Peers.Clear();
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

        protected override ValueTask Stop()
        {
            Server.Remove(this);

            return base.Stop();
        }

        protected override void DispatchMessage(NetDataWriter packet) => Server.Dispatcher.Dispatch(this, packet);

        readonly MessagingServer Server;
        internal MessagingPeer(MessagingServer Server, Socket Socket) : base()
        {
            this.Server = Server;

            AssignSocket(Socket);

            Start();
        }
    }
}
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
    public abstract class MessagingConnection
    {
        public Socket Socket { get; private set; }

        public bool IsConnected { get; protected set; }

        public CancellationTokenSource CancellationSource { get; private set; }
        public CancellationToken GetCancellationToken() => CancellationSource.Token;

        public const int LengthHeaderSize = 2;

        protected virtual void Run()
        {
            IsConnected = true;

            Receive();
        }

        #region Receive
        readonly NetDataWriter ReceiveBuffer;

        protected async void Receive()
        {
            var cancellation = GetCancellationToken();

            while (cancellation.IsCancellationRequested is false)
            {
                try
                {
                    ReceiveBuffer.EnsureFit(100);

                    var memory = ReceiveBuffer.Data.AsMemory(ReceiveBuffer.Position);

                    var read = await Socket.ReceiveAsync(memory, SocketFlags.None, cancellation);
                    if (cancellation.IsCancellationRequested)
                        return;

                    if (read is 0)
                    {
                        Stop();
                        return;
                    }

                    var cursor = ReceiveBuffer.Position + read;
                    ReceiveBuffer.Position = 0;

                    while (CheckReceivedMessage(cursor, out ushort length))
                        DispatchMessage(ReceiveBuffer, length);

                    AlignReceiveBuffer(cursor);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    NetworkLog.Error($"Exception on Socket Receive: {ex}");
                    Stop();

                    return;
                }
            }
        }

        bool CheckReceivedMessage(int cursor, out ushort length)
        {
            var available = cursor - ReceiveBuffer.Position;

            if (available < LengthHeaderSize)
            {
                length = default;
                return false; //No length header read
            }

            //Read Length
            {
                var buffer = ReceiveBuffer.PopSpan(LengthHeaderSize);
                available -= LengthHeaderSize;

                length = BitConverter.ToUInt16(buffer);
            }

            if (available < length)
            {
                ReceiveBuffer.Position -= LengthHeaderSize;
                return false; //Only length header read, rest of message in transit
            }

            return true;
        }

        unsafe void AlignReceiveBuffer(int cursor)
        {
            if (ReceiveBuffer.Position == 0)
            {
                //No data read at all
                //We should never be able to reach this point because
                //When the socket reads 0 we terminate connection
                //And when the message is not yet completely received we re-run the receive loop
                //But I don't throw an exception, just a warning

                NetworkLog.Warning($"Messaging Receive Buffer Completely not Read");

                ReceiveBuffer.Position = cursor;
                return;
            }

            if (ReceiveBuffer.Position == cursor)
            {
                //Happy case scenario, receive buffer read as far as the cursor
                //Means that the data inside the buffer was a complete full message
                //Nothing more, nothing less

                ReceiveBuffer.Position = 0;
                return;
            }

            if (ReceiveBuffer.Position < cursor)
            {
                //Still some data remaining in buffer
                //Most likely a fragment from the next message
                //TCP is a stream protocol after all

                fixed (byte* destination = ReceiveBuffer.Data)
                {
                    var source = destination + ReceiveBuffer.Length; //Where to copy from
                    var capacity = ReceiveBuffer.Capacity; //Length of the destination
                    var count = cursor - ReceiveBuffer.Position; //Bytes to copy

                    Buffer.MemoryCopy(source, destination, capacity, count);

                    ReceiveBuffer.Position = count;
                }
            }
            else
            {
                //A serialization error happened, the receive buffer was read for more data than was received

                throw new Exception($"Messaging Receive Buffer Mis-Alignment, Read for {ReceiveBuffer.Position} when Max was {cursor}");
            }
        }

        protected abstract void DispatchMessage(INetworkStream stream, ushort length);
        #endregion

        #region Send
        readonly SemaphoreSlim SendLock;

        readonly NetDataWriter SendBuffer;

        public async void Send<[NetworkSerializationMarker] T>(T data) => await SendAsync(data);
        public async Task SendAsync<[NetworkSerializationMarker] T>(T data)
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

                return;
            }
            catch (SocketException ex)
            {
                var text = $"Socket Send Exception: {ex.ErrorCode} | {ex.SocketErrorCode} | {ex.NativeErrorCode}";

                NetworkLog.Error(text);
                Stop();
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            finally
            {
                SendBuffer.Reset();

                SendLock.Release();
            }
        }
        #endregion

        #region Stop
        protected async void Stop()
        {
            if (IsConnected is false)
                return;

            IsConnected = false;

            CancellationSource.Cancel();

            NetworkLog.Trace($"Stopping {this}");

            OnStop?.Invoke();

            try
            {
                Socket.Shutdown(SocketShutdown.Both);

                var operation = new TaskCompletionSource<bool>();

                var args = new SocketAsyncEventArgs();

                args.Completed += (sender, args) => operation.TrySetResult(true);

                if (Socket.DisconnectAsync(args) is false)
                    return;

                await operation.Task;
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
        public event Action OnStop;

        protected virtual void Reset()
        {
            Socket.Close();

            ReceiveBuffer.Reset();
            SendBuffer.Reset();
        }
        #endregion

        public override string ToString() => $"[Socket {Socket.RemoteEndPoint} -> {Socket.LocalEndPoint}]";

        protected MessagingConnection()
        {
            CancellationSource = new CancellationTokenSource();

            SendLock = new SemaphoreSlim(1);
            SendBuffer = new NetDataWriter(true, 128);

            ReceiveBuffer = new NetDataWriter(true, 128);
        }
        protected MessagingConnection(Socket Socket) : this()
        {
            this.Socket = Socket;
        }

        protected static Socket CreateSocket() => new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    }

    public class MessagingClient : MessagingConnection
    {
        public DispatcherProperty Dispatcher { get; }
        public class DispatcherProperty
        {
            ActionDelegate[] Handlers;
            public delegate void ActionDelegate(INetworkStream reader);

            public void Dispatch(INetworkStream reader)
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

                void Surrogate(INetworkStream reader)
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
            try
            {
                await Socket.ConnectAsync(address, port);
            }
            catch (Exception ex)
            {
                NetworkLog.Error($"Exception: {ex}");

                return WslaError.From(WslaErrorCode.TransportFailure);
            }

            Run();

            return WslaResponse<WslaError>.Success;
        }
        public void Disconnect() => Stop();

        protected override void DispatchMessage(INetworkStream stream, ushort length) => Dispatcher.Dispatch(stream);

        public MessagingClient() : base(CreateSocket())
        {
            Dispatcher = new DispatcherProperty();
        }
    }

    public class MessagingPeer : MessagingConnection
    {
        public MessagingServer Server { get; }

        public void Start() => Run();

        protected override void DispatchMessage(INetworkStream stream, ushort length) => Server.Dispatcher.Dispatch(this, stream);

        public MessagingPeer(MessagingServer Server, Socket Socket) : base(Socket)
        {
            this.Server = Server;
        }
    }

    public class MessagingServer
    {
        Socket Socket;

        List<MessagingPeer> Peers;

        public DispatcherProperty Dispatcher { get; }
        public class DispatcherProperty
        {
            ActionDelegate[] Handlers;
            public delegate void ActionDelegate(MessagingPeer peer, INetworkStream packet);

            public void Dispatch(MessagingPeer peer, INetworkStream packet)
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

            public delegate void SyncTypeDelegate<T>(MessagingPeer peer, ref T message);
            public void RegisterSync<[NetworkSerializationMarker] T>(SyncTypeDelegate<T> handler)
            {
                var id = NetworkTypes.Get<T>();

                Handlers[id] = Surrogate;

                void Surrogate(MessagingPeer peer, INetworkStream reader)
                {
                    var data = NetworkSerializer.ReadValue<T>(reader);
                    handler(peer, ref data);
                }
            }

            public delegate Task AsyncTypeDelegate<T>(MessagingPeer peer, T message);
            public void RegisterAsync<[NetworkSerializationMarker] T>(AsyncTypeDelegate<T> handler)
            {
                var id = NetworkTypes.Get<T>();

                Handlers[id] = Surrogate;

                async void Surrogate(MessagingPeer peer, INetworkStream reader)
                {
                    var data = NetworkSerializer.ReadValue<T>(reader);
                    await handler(peer, data);
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
            Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true,
            };

            var endpoint = new IPEndPoint(address, port);
            Socket.Bind(endpoint);

            Socket.Listen(100);

            Poll();
        }

        async void Poll()
        {
            while (true)
            {
                var socket = await Socket.AcceptAsync();
                var peer = new MessagingPeer(this, socket);

                lock (Peers)
                {
                    Peers.Add(peer);
                }

                peer.Start();
            }
        }

        public MessagingServer()
        {
            Dispatcher = new DispatcherProperty();

            Peers = new List<MessagingPeer>();
        }
    }
}
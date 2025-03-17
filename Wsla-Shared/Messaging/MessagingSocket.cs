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
        }

        #region Receive
        readonly NetDataWriter ReceiveBuffer;
        int ReceiveReadLength;

        protected async void RunDispatcher(Action<INetworkStream> sink)
        {
            while (true)
            {
                var response = await ReceivePacket();

                if (response.IsError)
                    return;

                sink(response.Value);
            }
        }

        protected async Task<WslaResponse<INetworkStream, WslaError>> ReceivePacket()
        {
            var cancellation = GetCancellationToken();

            while (true)
            {
                try
                {
                    if (TryReadPacket())
                        return ReceiveBuffer;

                    AlignReceiveBuffer();

                    ReceiveBuffer.EnsureFit(100);

                    var memory = ReceiveBuffer.Data.AsMemory(ReceiveBuffer.Position);

                    var read = await Socket.ReceiveAsync(memory, SocketFlags.None, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    if (read == 0)
                    {
                        Stop();

                        return WslaError.From(WslaErrorCode.SocketClosed);
                    }

                    ReceiveReadLength += read;

                    if (TryReadPacket())
                        return ReceiveBuffer;
                }
                catch (SocketException ex)
                {
                    NetworkLog.Error($"Socket Receive Exception: {ex.ErrorCode} | {ex.SocketErrorCode} | {ex.NativeErrorCode}");
                    Stop();

                    return WslaError.From(WslaErrorCode.SocketClosed);
                }
                catch (OperationCanceledException)
                {
                    return WslaError.From(WslaErrorCode.SocketClosed);
                }
            }
        }

        bool TryReadPacket()
        {
            if (ReceiveReadLength < LengthHeaderSize)
                return false;

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

            return true;
        }

        unsafe void AlignReceiveBuffer()
        {
            if (ReceiveBuffer.Position is 0)
                return; //Already Aligned

            var remaining = ReceiveBuffer.GetSpan(ReceiveBuffer.Position, ReceiveReadLength);

            fixed (byte* ptr = ReceiveBuffer.Data)
            {
                Buffer.MemoryCopy((ptr + ReceiveBuffer.Position), ptr, ReceiveBuffer.Data.Length, ReceiveReadLength);
            }

            ReceiveBuffer.Position = 0;
        }
        #endregion

        #region Send
        readonly SemaphoreSlim SendLock;

        readonly NetDataWriter SendBuffer;

        public async Task<WslaResponse<WslaError>> Send<[NetworkSerializationMarker] T>(T data)
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

                return WslaResponse<WslaError>.Success;
            }
            catch (SocketException ex)
            {
                var text = $"Socket Send Exception: {ex.ErrorCode} | {ex.SocketErrorCode} | {ex.NativeErrorCode}";

                NetworkLog.Error(text);
                Stop();

                return new WslaError(WslaErrorCode.TransportFailure, text);
            }
            catch (OperationCanceledException)
            {
                return WslaError.From(WslaErrorCode.OperationCanceled);
            }
            finally
            {
                SendLock.Release();
                SendBuffer.Reset();
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

        protected static Socket CreateSocket()
        {
            return new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true,
            };
        }
    }

    public class MessagingQuery : MessagingConnection, IDisposable
    {
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

        void IDisposable.Dispose() => Disconnect();

        public async Task<WslaResponse<T, WslaError>> Receive<[NetworkSerializationMarker] T>()
        {
            var response = await ReceivePacket();

            if (response.IsError)
                return response.Error;

            var stream = response.Value;

            var type = NetworkSerializer.ReadValue<Type>(stream);

            if (type == typeof(T))
                return NetworkSerializer.ReadValue<T>(stream);
            else if (type == typeof(WslaError))
                return NetworkSerializer.ReadValue<WslaError>(stream);
            else
                throw new InvalidCastException($"Cannot Read Packet for Type {type} as {typeof(T)}");
        }

        public async Task<WslaResponse<TResponse, WslaError>> Transport<[NetworkSerializationMarker] TRequest, [NetworkSerializationMarker] TResponse>(IPAddress address, ushort port, TRequest request)
        {
            //Connect
            {
                var response = await Connect(address, port);

                if (response.IsError)
                    return response.Error;
            }

            //Send
            {
                var response = await Send(request);

                if (response.IsError)
                    return response.Error;
            }

            //Receive
            {
                var response = await Receive<TResponse>();

                return response;
            }
        }

        public MessagingQuery() : base(CreateSocket()) { }
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

            RunDispatcher(DispatchMessage);

            return WslaResponse<WslaError>.Success;
        }
        public void Disconnect() => Stop();

        void DispatchMessage(INetworkStream stream) => Dispatcher.Dispatch(stream);

        public MessagingClient() : base(CreateSocket())
        {
            Dispatcher = new DispatcherProperty();
        }
    }

    public class MessagingPeer : MessagingConnection
    {
        public MessagingServer Server { get; }

        public void Start() => Run();
        protected override void Run()
        {
            base.Run();

            RunDispatcher(DispatchMessage);
        }

        void DispatchMessage(INetworkStream packet) => Server.Dispatcher.Dispatch(this, packet);

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

public struct MessagingData<TResponse> : IAutoNetworkSerialization
{
    public uint Index;
    public TResponse Response;

    public void Select(ref AutoSerializationContext context)
    {
        context.Select(ref Index);
        context.Select(ref Response);
    }
}
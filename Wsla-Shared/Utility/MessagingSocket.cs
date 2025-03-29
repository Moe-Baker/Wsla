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

        volatile MessagingSocketState State;
        public MessagingSocketState GetState() => State;

        MessagingSocketDisconnectReason DisconnectReason;

        CancellationTokenSource CancellationSource;
        public CancellationToken DisconnectCancellationToken => CancellationSource.Token;

        public const int LengthHeaderSize = 2;

        public object Tag;

        protected virtual void Run()
        {
            State = MessagingSocketState.Connected;

            SendLock = new SemaphoreSlim(1);
            SendBuffer = new NetDataWriter(true, 128);

            ReceiveBuffer = new NetDataWriter(true, 128);

            LastSendTime = AtomicTime.Create();
            LastReceiveTime = AtomicTime.Create();

            Receive(CancellationSource.Token).Forget();
            KeepAlive(CancellationSource.Token).Forget();
        }

        #region Send
        SemaphoreSlim SendLock;

        NetDataWriter SendBuffer;

        public void SendMessage<[NetworkSerializationMarker] T>(T data) => SendMessageAsync(data).Forget();
        public async Task SendMessageAsync<[NetworkSerializationMarker] T>(T data)
        {
            try
            {
                await SendLock.WaitAsync(DisconnectCancellationToken);

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
                await Socket.SendAsync(memory, SocketFlags.None, DisconnectCancellationToken);
                if (DisconnectCancellationToken.IsCancellationRequested)
                    return;

                //Update Keep Alive Send Time
                LastSendTime.UpdateTime();

                return;
            }
            catch (SocketException ex)
            {
                var text = $"Socket Send Exception: {ex.ErrorCode} | {ex.SocketErrorCode} | {ex.NativeErrorCode}";

                NetworkLog.Error(text);
                Stop(MessagingSocketDisconnectReason.SendError);
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

        async Task SendKeepAlive()
        {
            try
            {
                await SendLock.WaitAsync(DisconnectCancellationToken);

                await Socket.SendAsync(KeepAlivePayload, SocketFlags.None, DisconnectCancellationToken);
                if (DisconnectCancellationToken.IsCancellationRequested)
                    return;

                //Update Keep Alive Send Time
                LastSendTime.UpdateTime();

                return;
            }
            catch (SocketException ex)
            {
                var text = $"Socket Send Exception: {ex.ErrorCode} | {ex.SocketErrorCode} | {ex.NativeErrorCode}";

                NetworkLog.Error(text);
                Stop(MessagingSocketDisconnectReason.SendError);
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            finally
            {
                SendLock.Release();
            }
        }
        #endregion

        #region Receive
        NetDataWriter ReceiveBuffer;

        protected async Task Receive(CancellationToken cancellation)
        {
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
                        Stop(MessagingSocketDisconnectReason.RemoteClose);
                        return;
                    }

                    LastReceiveTime.UpdateTime();

                    var cursor = ReceiveBuffer.Position + read;
                    ReceiveBuffer.Position = 0;

                    while (CheckReceivedMessage(cursor, out ushort length))
                    {
                        if (length is 0) continue; //Keep alive message

                        var destination = ReceiveBuffer.Position + length;

                        DispatchMessage(ReceiveBuffer, length);

                        if (ReceiveBuffer.Position != destination)
                        {
                            NetworkLog.Warning($"Misaligned Read on Messaging Socket, Expected Read: {destination}, Actual Read: {ReceiveBuffer.Position}");
                            Stop(MessagingSocketDisconnectReason.ReceiveError);
                            return;
                        }
                    }

                    AlignReceiveBuffer(cursor);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    NetworkLog.Error($"Exception on Socket Receive: {ex.Message}");
                    Stop(MessagingSocketDisconnectReason.ReceiveError);

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
                available -= LengthHeaderSize;
                var buffer = ReceiveBuffer.PopSpan(LengthHeaderSize);
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
                //An incomplete message

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

        #region Keep Alive
        AtomicTime LastSendTime;
        AtomicTime LastReceiveTime;

        TimeSpan TimeoutDuration = TimeSpan.FromSeconds(20);
        TimeSpan KeepAliveSendInterval => TimeoutDuration / 3;

        static byte[] KeepAlivePayload = new byte[] { 0, 0 };

        async Task KeepAlive(CancellationToken cancellation)
        {
            while (cancellation.IsCancellationRequested is false)
            {
                await Task.Delay(KeepAliveSendInterval);
                if (cancellation.IsCancellationRequested)
                    return;

                //Check Send
                {
                    var duration = LastSendTime.ReadSpan();

                    if (duration >= KeepAliveSendInterval)
                        SendKeepAlive().Forget();
                }

                //Check Receive
                {
                    var duration = LastReceiveTime.ReadSpan();

                    if (duration >= TimeoutDuration)
                    {
                        NetworkLog.Error($"Socket {this} Timed-Out after {duration.TotalSeconds}s");
                        Stop(MessagingSocketDisconnectReason.Timeout);
                        return;
                    }
                }
            }
        }
        #endregion

        #region Stop
        object StopLock;

        protected void Stop(MessagingSocketDisconnectReason reason) => StopAsync(reason).Forget();
        protected async ValueTask StopAsync(MessagingSocketDisconnectReason reason)
        {
            CancellationSource.Cancel();

            lock (StopLock)
            {
                if (State is MessagingSocketState.Disconnected)
                    return;

                StopAction(reason);
            }

            NetworkLog.Trace($"Stopping Messaging Socket {this}");

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
                Socket.Close();
            }
        }
        protected virtual void StopAction(MessagingSocketDisconnectReason reason)
        {
            State = MessagingSocketState.Disconnected;
            DisconnectReason = reason;

            OnStop?.Invoke(this, reason);
        }

        public event StopDelegate OnStop;
        public delegate void StopDelegate(MessagingConnection connection, MessagingSocketDisconnectReason reason);

        public void RegisterStopCallback(StopDelegate callback)
        {
            lock (StopLock)
            {
                if (State is MessagingSocketState.Disconnected)
                    callback?.Invoke(this, DisconnectReason);
                else
                    OnStop += callback;
            }
        }

        public void UnregisterStopCallback(StopDelegate callback)
        {
            lock (StopLock)
            {
                OnStop -= callback;
            }
        }
        #endregion

        public override string ToString() => $"[Socket {Socket.RemoteEndPoint} -> {Socket.LocalEndPoint}]";

        protected MessagingConnection()
        {
            CancellationSource = new CancellationTokenSource();

            StopLock = new object();

            State = MessagingSocketState.Idle;
        }
        protected MessagingConnection(Socket Socket) : this()
        {
            this.Socket = Socket;
        }

        protected static Socket CreateSocket() => new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    }

    public enum MessagingSocketState
    {
        Idle, Connected, Disconnected
    }
    public enum MessagingSocketDisconnectReason
    {
        /// <summary>
        /// ¯\_(ツ)_/¯
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Connection timed-out
        /// </summary>
        Timeout = 1,

        /// <summary>
        /// Remote end (not you) closed the connection
        /// </summary>
        RemoteClose = 2,

        /// <summary>
        /// Local end (you) closed the connection
        /// </summary>
        LocalClose = 3,

        /// <summary>
        /// Error when sending a message
        /// </summary>
        SendError = 4,

        /// <summary>
        /// Error when receiving a message
        /// </summary>
        ReceiveError = 4,
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
                NetworkLog.Error($"Exception: {ex.Message}");

                return WslaError.From(WslaErrorCode.TransportFailure);
            }

            Run();

            return true;
        }
        public void Disconnect() => Stop(MessagingSocketDisconnectReason.LocalClose);

        protected override void DispatchMessage(INetworkStream stream, ushort length) => Dispatcher.Dispatch(stream);

        public MessagingClient() : base(CreateSocket())
        {
            Dispatcher = new DispatcherProperty();
        }
    }

    public class MessagingPeer : MessagingConnection
    {
        public MessagingServer Server { get; }

        internal void Start() => Run();

        public void Disconnect() => Stop(MessagingSocketDisconnectReason.LocalClose);
        protected override void StopAction(MessagingSocketDisconnectReason reason)
        {
            base.StopAction(reason);

            Server.RemovePeer(this);
        }

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

                void Surrogate(MessagingPeer peer, INetworkStream reader)
                {
                    var data = NetworkSerializer.ReadValue<T>(reader);
                    handler(peer, data).Forget();
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

            Poll().Forget();
        }

        async Task Poll()
        {
            while (true)
            {
                var socket = await Socket.AcceptAsync();
                var peer = new MessagingPeer(this, socket);

                AddPeer(peer);

                peer.Start();
            }
        }

        void AddPeer(MessagingPeer peer)
        {
            lock (Peers)
            {
                Peers.Add(peer);
            }
        }
        internal void RemovePeer(MessagingPeer peer)
        {
            lock (Peers)
            {
                Peers.Remove(peer);
            }
        }

        public MessagingServer()
        {
            Dispatcher = new DispatcherProperty();

            Peers = new List<MessagingPeer>();
        }
    }
}
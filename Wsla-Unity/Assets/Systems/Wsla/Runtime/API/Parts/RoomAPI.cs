using System;
using System.Buffers;
using System.Net;
using System.Threading;

using Cysharp.Threading.Tasks;

using LiteNetLib;
using LiteNetLib.Utils;

using MemoryPack;

using Toolbox;

using UnityEngine;

using Wsla.Shared;

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
}

[Serializable]
public class RoomInstance
{
    public TransportProperty Transport { get; }
    public class TransportProperty
    {
        public IPAddress Address { get; }
        public ushort Port { get; }

        NetManager Manager;
        EventBasedNetListener Listener;
        NetPeer Peer;

        CancellationTokenSource CancellationSource;

        internal async UniTask<Response> Start(ClientConnectionRequest request)
        {
            //Connect
            {
                Manager.Start();

                var operation = new UniTaskCompletionSource<Response<DisconnectInfo>>();

                Listener.PeerConnectedEvent += Connected;
                void Connected(NetPeer peer) => operation.TrySetResult(true);

                Listener.PeerDisconnectedEvent += Disconnect;
                void Disconnect(NetPeer peer, DisconnectInfo info) => operation.TrySetResult(info);

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
                    NetworkLog.Error($"Room Connection Failed, Reason: {response.Error.Reason}");
                    Stop();
                    return false;
                }
            }

            //Prepare State
            {
                Listener.PeerConnectedEvent += ConnectedCallback;
                Listener.NetworkReceiveEvent += ReceiveCallback;
                Listener.PeerDisconnectedEvent += DisconnectedCallback;
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

        void ConnectedCallback(NetPeer peer)
        {
            NetworkLog.Info($"Peer {peer.Id} Connected");
        }
        void DisconnectedCallback(NetPeer peer, DisconnectInfo info)
        {
            NetworkLog.Info($"Peer {peer.Id} Disconnected");
        }

        void ReceiveCallback(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod delivery)
        {
            reader.Recycle();
        }

        RoomInstance Room;
        public TransportProperty(RoomInstance room, IPAddress address, ushort port)
        {
            this.Room = room;

            this.Address = address;
            this.Port = port;

            Listener = new EventBasedNetListener();

            Manager = new NetManager(Listener);
        }
    }

    public async UniTask<Response<WslaError>> Start(ClientConnectionRequest request)
    {
        var response = await Transport.Start(request);
        if (response.IsError)
            return WslaError.From(WslaErrorCode.TransportFailure);

        return true;
    }

    public RoomInstance(IPAddress address, ushort port)
    {
        Transport = new TransportProperty(this, address, port);
    }
}

public struct WslaError
{
    public WslaErrorCode Code { get; }
    public string Description { get; }

    public override string ToString()
    {
        if (string.IsNullOrEmpty(Description))
            return Code.ToString();
        else
            return $"{Code} | {Description}";
    }

    public WslaError(WslaErrorCode code, string description)
    {
        this.Code = code;
        this.Description = description;
    }

    public static WslaError From(WslaErrorCode code) => new(code, string.Empty);

    public static implicit operator WslaError(WslaErrorCode code) => From(code);
}
public enum WslaErrorCode
{
    TransportFailure,
}
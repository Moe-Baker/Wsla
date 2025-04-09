using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Wsla.Unity
{
    [Serializable]
    public class MatchMakingAPI : NetworkAPI.Property
    {
        public List<ServerRegion> Regions { get; private set; }

        public async Task<WslaResponse<WslaError>> UpdateRegions()
        {
            var response = await API.REST.GET<List<ServerRegion>>(Constants.RestRoutes.ListRegions);

            if (response.IsError)
                return WslaError.From(response.Error);

            Regions = response.Value;

            NetworkLog.Trace($"Region Servers {Regions.FormatString()}");

            return WslaResponse<WslaError>.Success;
        }

        public Task<WslaResponse<RoomConnectionInfo, WslaError>> CreateRoom(SparseArray<ServerRegion> regions, CreateRoomParameters parameters)
        {
            NetworkLog.Info(API.ApplicationID.Value);

            var request = new CreateRoomRequest(API.ApplicationID.Value, regions, parameters);
            return CreateRoom(request);
        }
        public async Task<WslaResponse<RoomConnectionInfo, WslaError>> CreateRoom(CreateRoomRequest request)
        {
            var response = await API.REST.POST<CreateRoomRequest, RoomConnectionInfo>(Constants.RestRoutes.CreateRoom, request);

            if (response.IsError)
                return WslaError.From(response.Error);

            var info = response.Value;

            return new RoomConnectionInfo(info.Address, info.Port);
        }

        public Task<WslaResponse<RoomConnectionInfo, WslaError>> FindRoom(SparseArray<ServerRegion> regions, CreateRoomParameters? create = default)
        {
            var request = new FindRoomRequest(API.ApplicationID.Value, regions, create);
            return FindRoom(request);
        }
        public async Task<WslaResponse<RoomConnectionInfo, WslaError>> FindRoom(FindRoomRequest request)
        {
            var response = await API.REST.POST<FindRoomRequest, RoomConnectionInfo>(Constants.RestRoutes.FindRoom, request);

            if (response.IsError)
                return WslaError.From(response.Error);

            var info = response.Value;

            return info;
        }

        public async Task<WslaResponse<List<RoomListEntryInfo>, WslaError>> ListRooms(SparseArray<ServerRegion> regions)
        {
            var request = new ListRoomsRequest(API.ApplicationID.Value, regions);

            var response = await API.REST.POST<ListRoomsRequest, List<RoomListEntryInfo>>(Constants.RestRoutes.ListRooms, request);

            if (response.IsError)
                return WslaError.From(response.Error);

            return response.Value;
        }

        public MatchMakingTicket FindMatch(FixedString<FS20> pool, NetworkSceneID scene, SparseArray<ServerRegion> regions, CancellationToken cancellation = default)
        {
            return new MatchMakingTicket(pool, regions, scene, cancellation);
        }
    }

    public class MatchMakingTicket
    {
        readonly SparseArray<ServerRegion> Regions;
        readonly FixedString<FS20> PoolName;
        readonly NetworkSceneID Scene;

        MessagingClient Client;

        readonly CancellationToken CancellationToken;
        bool IsCancelled => CancellationToken.IsCancellationRequested;

        CancellationTokenRegistration CancellationRegistration;

        TaskCompletionSource<WslaResponse<RoomConnectionInfo, WslaError>> Operation;

        NetworkAPI API => NetworkAPI.Instance;

        public async Task<WslaResponse<RoomConnectionInfo, WslaError>> Operate()
        {
            Client = new MessagingClient();

            //Setup Operation
            {
                Operation = new();

                if (CancellationToken.CanBeCanceled)
                    CancellationRegistration = CancellationToken.Register(CancelHandler);
            }

            Client.RegisterStopCallback(ClientStopCallback);

            //Connect
            {
                var response = await Client.Connect(API.CoordinatorAddress.IP, Constants.CoordinatorMessagingPort);

                if (response.IsError)
                    return response.Error;
            }

            if (IsCancelled) return WslaError.From(WslaErrorCode.OperationCanceled);

            //Register Dispatchers
            {
                Client.Dispatcher.Register<MatchmakingSuccessResponse>(SuccessHandler);
                Client.Dispatcher.Register<MatchmakingFailResponse>(FailHandler);
            }

            //Send Request
            {
                var request = new StartMatchMakingRequest(API.ApplicationID.Value, PoolName, Regions, Scene);
                await Client.SendMessageAsync(request);
            }

            var result = await Operation.Task;

            return result;
        }

        void SuccessHandler(ref MatchmakingSuccessResponse message)
        {
            Operation.TrySetResult(message.Info);

            Disconnect();
        }
        void FailHandler(ref MatchmakingFailResponse message)
        {
            Operation.TrySetResult(message.Error);

            Disconnect();
        }
        void CancelHandler()
        {
            Operation.TrySetResult(WslaError.From(WslaErrorCode.OperationCanceled));

            Disconnect();
        }

        void Disconnect()
        {
            CancellationRegistration.Dispose();

            Client.UnregisterStopCallback(ClientStopCallback);
            Client.Disconnect();
        }

        void ClientStopCallback(MessagingConnection connection, MessagingSocketDisconnectReason reason)
        {
            Operation.TrySetResult(WslaError.From(WslaErrorCode.TransportFailure));
        }

        public MatchMakingTicket(FixedString<FS20> PoolName, SparseArray<ServerRegion> Regions, NetworkSceneID Scene, CancellationToken CancellationToken)
        {
            this.Regions = Regions;
            this.PoolName = PoolName;
            this.Scene = Scene;

            this.CancellationToken = CancellationToken;
            CancellationRegistration = default;
        }
    }
}
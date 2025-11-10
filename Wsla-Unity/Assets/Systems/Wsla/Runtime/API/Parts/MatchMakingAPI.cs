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
            var response = await API.REST.GET<List<ServerRegion>>(Constants.RestRoutes.Service.ListRegions);

            if (response.IsError)
                return WslaError.From(response.Error);

            Regions = response.Value;

            NetworkLog.Trace($"Region Servers {Regions.FormatString()}");

            return WslaResponse<WslaError>.Success;
        }

        public Task<WslaResponse<RoomConnectionInfo, WslaError>> CreateRoom(SparseArray<ServerRegion> regions, CreateRoomParameters parameters)
        {
            var request = new CreateRoomRequest(API.GameVersion.Value, API.ApplicationID.Value, regions, parameters);
            return CreateRoom(request);
        }
        public async Task<WslaResponse<RoomConnectionInfo, WslaError>> CreateRoom(CreateRoomRequest request)
        {
            var response = await API.REST.POST<CreateRoomRequest, RoomConnectionInfo>(Constants.RestRoutes.Service.CreateRoom, request);

            if (response.IsError)
                return WslaError.From(response.Error);

            var info = response.Value;

            return new RoomConnectionInfo(info.Address, info.Port);
        }

        public Task<WslaResponse<RoomConnectionInfo, WslaError>> FindRoom(SparseArray<ServerRegion> regions, CreateRoomParameters? create = default)
        {
            var request = new FindRoomRequest(API.GameVersion.Value, API.ApplicationID.Value, regions, create);
            return FindRoom(request);
        }
        public async Task<WslaResponse<RoomConnectionInfo, WslaError>> FindRoom(FindRoomRequest request)
        {
            var response = await API.REST.POST<FindRoomRequest, RoomConnectionInfo>(Constants.RestRoutes.Service.FindRoom, request);

            if (response.IsError)
                return WslaError.From(response.Error);

            var info = response.Value;

            return info;
        }

        public async Task<WslaResponse<List<RoomListEntryInfo>, WslaError>> ListRooms(SparseArray<ServerRegion> regions)
        {
            var request = new QueryRoomsRequest(API.GameVersion.Value, API.ApplicationID.Value, regions);

            var response = await API.REST.POST<QueryRoomsRequest, List<RoomListEntryInfo>>(Constants.RestRoutes.Service.QueryRooms, request);

            if (response.IsError)
                return WslaError.From(response.Error);

            return response.Value;
        }

        public MatchMakingTicket FindMatch(FixedString<FS20> pool, SparseArray<NetworkSceneID> scene, SparseArray<ServerRegion> regions, MatchMakingParameters parameters = default, CancellationToken cancellation = default)
        {
            return new MatchMakingTicket(pool, regions, scene, parameters, cancellation);
        }
    }

    public class MatchMakingTicket
    {
        readonly SparseArray<ServerRegion> Regions;
        readonly FixedString<FS20> PoolName;
        readonly SparseArray<NetworkSceneID> Scene;
        readonly MatchMakingParameters Parameters;

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
                var request = new StartMatchMakingRequest(API.GameVersion.Value, API.ApplicationID.Value, PoolName, Regions, Scene, Parameters);
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

        public MatchMakingTicket(FixedString<FS20> PoolName, SparseArray<ServerRegion> Regions, SparseArray<NetworkSceneID> Scene, MatchMakingParameters Parameters, CancellationToken CancellationToken)
        {
            this.Regions = Regions;
            this.PoolName = PoolName;
            this.Scene = Scene;
            this.Parameters = Parameters;

            this.CancellationToken = CancellationToken;
            CancellationRegistration = default;
        }
    }
}
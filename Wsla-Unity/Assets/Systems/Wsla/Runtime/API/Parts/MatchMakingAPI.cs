using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using UnityEngine;

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

            NetworkLog.Trace($"Region Servers ({Regions.Count})");

            foreach (var region in Regions)
                NetworkLog.Trace($"Region: {region}");

            return WslaResponse<WslaError>.Success;
        }

        public Task<WslaResponse<RoomConnectionInfo, WslaError>> CreateRoom(ServerRegion? region, CreateRoomParameters parameters)
        {
            var request = new CreateRoomRequest(region, parameters);
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

        public Task<WslaResponse<RoomConnectionInfo, WslaError>> FindRoom(ServerRegion? region, CreateRoomParameters? create = default)
        {
            var request = new FindRoomRequest(region, create);
            return FindRoom(request);
        }
        public async Task<WslaResponse<RoomConnectionInfo, WslaError>> FindRoom(FindRoomRequest request)
        {
            var response = await API.REST.POST<FindRoomRequest, RoomConnectionInfo?>(Constants.RestRoutes.FindRoom, request);

            if (response.IsError)
                return WslaError.From(response.Error);

            var info = response.Value;

            if (info.HasValue is false)
                return WslaError.From(WslaErrorCode.NoRoomFound);

            return info.Value;
        }

        public async Task<WslaResponse<List<RoomListEntryInfo>, WslaError>> ListRooms(ServerRegion region)
        {
            var request = new ListRoomsRequest(region);

            var response = await API.REST.POST<ListRoomsRequest, List<RoomListEntryInfo>>(Constants.RestRoutes.ListRooms, request);

            if (response.IsError)
                return WslaError.From(response.Error);

            return response.Value;
        }

        public MatchMakingTicket FindMatch(ServerRegion region) => new MatchMakingTicket(API);
    }

    public class MatchMakingTicket
    {
        readonly NetworkAPI API;

        readonly Guid ID;

        readonly CancellationToken ApplicationQuitToken;

        readonly CancellationTokenSource CancellationSource;
        public bool IsCanceled => CancellationSource.IsCancellationRequested;

        static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(1f);

        public async Task<WslaResponse<RoomConnectionInfo, WslaError>> Operate()
        {
            var token = CancellationSource.Token;

            try
            {
                await Request(token);
                var info = await Poll(token);

                if (info.HasValue is false)
                    return WslaError.From(WslaErrorCode.NoRoomFound);

                return info.Value;
            }
            catch (OperationCanceledException)
            {
                Cancel().Forget();
                return WslaError.From(WslaErrorCode.OperationCanceled);
            }
            catch (Exception ex)
            {
                NetworkLog.Error(ex);
                return WslaError.From(ex);
            }
        }

        async Task<bool> Request(CancellationToken cancellation)
        {
            var request = new MatchMakingRequest(ID);

            var response = await API.REST.POST(Constants.RestRoutes.RequestMatch, request, cancellation: cancellation);
            cancellation.ThrowIfCancellationRequested();

            if (response.IsError)
                throw response.Error.ToException();

            return true;
        }

        async Task<RoomConnectionInfo?> Poll(CancellationToken cancellation)
        {
            while (true)
            {
                var response = await API.REST.POST<Guid, MatchMakingUpdate>(Constants.RestRoutes.UpdateMatch, ID, cancellation: cancellation);
                cancellation.ThrowIfCancellationRequested();

                if (response.IsError)
                    throw response.Error.ToException();

                var update = response.Value;

                switch (update.Progress)
                {
                    case MatchMakingProgress.Searching:
                    {
                        await Task.Delay(PollingInterval, cancellation);
                        cancellation.ThrowIfCancellationRequested();
                    }
                    continue;

                    case MatchMakingProgress.Found:
                        return update.Info;

                    case MatchMakingProgress.NotFound:
                        return null;
                }
            }
        }

        async Task Cancel()
        {
            if (ApplicationQuitToken.IsCancellationRequested)
                return;

            await API.REST.POST<Guid, MatchMakingUpdate>(Constants.RestRoutes.CancelMatch, ID);
        }

        internal MatchMakingTicket(NetworkAPI API)
        {
            this.API = API;

            ID = Guid.NewGuid();

            ApplicationQuitToken = Application.exitCancellationToken;
            CancellationSource = CancellationTokenSource.CreateLinkedTokenSource(ApplicationQuitToken);
        }
    }
}
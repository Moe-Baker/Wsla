using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Wsla.Unity
{
    [Serializable]
    public class MatchMakingAPI : NetworkAPI.Property
    {
        public List<ServerRegion> Regions { get; private set; }

        public async Task<WslaResponse<WslaError>> UpdateRegions()
        {
            var response = await API.REST.GET<List<ServerRegion>>(API.CoordinatorAddress.IP, Constants.CoordinatorHttpPort, Constants.RestRoutes.ListRegions);

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
            var response = await API.REST.POST<CreateRoomRequest, RoomConnectionInfo>(API.CoordinatorAddress.IP, Constants.CoordinatorHttpPort, Constants.RestRoutes.CreateRoom, request);

            if (response.IsError)
                return WslaError.From(response.Error);

            var info = response.Value;

            return new RoomConnectionInfo(info.Address, info.Port);
        }

        public Task<WslaResponse<RoomConnectionInfo?, WslaError>> FindRoom(ServerRegion? region, CreateRoomParameters? create = default)
        {
            var request = new FindRoomRequest(region, create);
            return FindRoom(request);
        }
        public async Task<WslaResponse<RoomConnectionInfo?, WslaError>> FindRoom(FindRoomRequest request)
        {
            var response = await API.REST.POST<FindRoomRequest, RoomConnectionInfo?>(API.CoordinatorAddress.IP, Constants.CoordinatorHttpPort, Constants.RestRoutes.FindRoom, request);

            if (response.IsError)
                return WslaError.From(response.Error);

            return response.Value;
        }

        public async Task<WslaResponse<List<RoomListEntryInfo>, WslaError>> ListRooms(ServerRegion region)
        {
            var request = new ListRoomsRequest(region);

            var response = await API.REST.POST<ListRoomsRequest, List<RoomListEntryInfo>>(API.CoordinatorAddress.IP, Constants.CoordinatorHttpPort, Constants.RestRoutes.ListRooms, request);

            if (response.IsError)
                return WslaError.From(response.Error);

            return response.Value;
        }
    }
}
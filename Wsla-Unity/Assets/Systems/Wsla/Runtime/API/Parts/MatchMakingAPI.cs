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
            var response = await API.REST.GET<ListRegionsResponse>(API.CoordinatorAddress.IP, Constants.CoordinatorMessagingPort, Constants.RestRoutes.ListRegions);

            if (response.IsError)
                return WslaError.From(response.Error);

            Regions = response.Value.Regions;

            NetworkLog.Trace($"Region Servers ({Regions.Count})");

            foreach (var region in Regions)
                NetworkLog.Trace($"Region: {region}");

            return WslaResponse<WslaError>.Success;
        }

        public async Task<WslaResponse<RoomConnectionInfo, WslaError>> CreateRoom(ServerRegion region, CreateRoomCommand command)
        {
            var request = new CreateRoomRequest(region, command);

            var response = await API.REST.POST<CreateRoomRequest, CreateRoomResponse>(API.CoordinatorAddress.IP, Constants.CoordinatorMessagingPort, Constants.RestRoutes.CreateRoom, request);

            if (response.IsError)
                return WslaError.From(response.Error);

            var info = response.Value;

            return new RoomConnectionInfo(info.Address, info.Port);
        }

        public async Task<WslaResponse<List<RoomListEntryInfo>, WslaError>> ListRooms(ServerRegion region)
        {
            var request = new ListRoomsRequest(region);

            var response = await API.REST.POST<ListRoomsRequest, List<RoomListEntryInfo>>(API.CoordinatorAddress.IP, Constants.CoordinatorMessagingPort, Constants.RestRoutes.ListRooms, request);

            if (response.IsError)
                return WslaError.From(response.Error);

            return response.Value;
        }
    }
}
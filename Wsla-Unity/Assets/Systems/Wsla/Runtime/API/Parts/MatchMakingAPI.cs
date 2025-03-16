using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace Wsla.Unity
{
    [Serializable]
    public class MatchMakingAPI : NetworkAPI.Property
    {
        public List<ServerRegion> Regions { get; private set; }

        public async Task<WslaResponse<WslaError>> UpdateRegions()
        {
            using (var query = new MessagingQuery())
            {
                var request = new ListRegionsRequest();

                var response = await query.Transport<ListRegionsRequest, ListRegionsResponse>(API.CoordinatorAddress.IP, Constants.CoordinatorMessagingPort, request);

                if (response.IsError)
                    return response.Error;

                Regions = response.Value.Regions;

                NetworkLog.Trace($"Region Servers ({Regions.Count})");

                foreach (var region in Regions)
                    NetworkLog.Trace($"Region: {region}");

                return WslaResponse<WslaError>.Success;
            }
        }

        public async Task<WslaResponse<RoomConnectionInfo, WslaError>> CreateRoom(ServerRegion region, CreateRoomCommand command)
        {
            var request = new CreateRoomRequest(command, region);

            using (var query = new MessagingQuery())
            {
                var response = await query.Transport<CreateRoomRequest, CreateRoomResponse>(API.CoordinatorAddress.IP, Constants.CoordinatorMessagingPort, request);

                if (response.IsError)
                    return response.Error;

                var info = response.Value;

                return new RoomConnectionInfo(info.Address, info.Port);
            }
        }
    }
}
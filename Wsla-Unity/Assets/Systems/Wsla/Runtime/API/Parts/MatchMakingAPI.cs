using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace Wsla.Unity
{
    [Serializable]
    public class MatchMakingAPI : NetworkAPI.Property
    {
        public Dictionary<ServerRegion, IPAddress> Regions { get; private set; }

        public bool TryGet(ServerRegion region, out IPAddress address) => Regions.TryGetValue(region, out address);

        public async Task<WslaResponse<WslaError>> UpdateRegions()
        {
            using (var query = new MessagingQuery())
            {
                var request = new ListRelaysRequest();

                var response = await query.Transport<ListRelaysRequest, ListRelaysResponse>(API.CoordinatorAddress.IP, Constants.CoordinatorMessagingPort, request);

                if (response.IsError)
                    return response.Error;

                Regions = response.Value.Regions;

                NetworkLog.Trace($"Region Servers ({Regions.Count})");

                foreach (var (region, address) in Regions)
                    NetworkLog.Trace($"Region: {region}, Address: {address}");

                return WslaResponse<WslaError>.Success;
            }
        }

        public async Task<WslaResponse<RoomConnectionInfo, WslaError>> CreateRoom(ServerRegion region, CreateRoomRequest request)
        {
            if (TryGet(region, out var address) is false)
                return WslaError.From(WslaErrorCode.NoRegion);

            using (var query = new MessagingQuery())
            {
                var response = await query.Transport<CreateRoomRequest, CreateRoomResponse>(address, Constants.RelayMessagingPort, request);

                if (response.IsError)
                    return response.Error;

                return new RoomConnectionInfo(address, response.Value.Port);
            }
        }
    }
}
using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using System.Net.Http.Json;
using System.Diagnostics;
using UnityEngine;

namespace Wsla.Unity
{
    [Serializable]
    public class RestAPI : NetworkAPI.Property
    {
        readonly UrlStringCache UrlCache;

        readonly AutoCyclingValue<HttpClient> ClientCycle;

        public async Task<WslaResponse<T, RestResponse>> GET<T>(IPAddress address, ushort port, string path)
        {
            var url = UrlCache.Get(address, port, path);
            var client = ClientCycle.Fetch();

            var response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode is false)
                return RestResponse.From(response);

            T value;

            try
            {
                value = await response.Content.ReadFromJsonAsync<T>(options: SharedAPI.JsonOptions);
            }
            catch (Exception ex)
            {
                return RestResponse.From(ex);
            }

            return value;
        }

        public async Task<WslaResponse<RestResponse>> PUT<TRequest>(IPAddress address, ushort port, string path, TRequest request)
        {
            var url = UrlCache.Get(address, port, path);
            var client = ClientCycle.Fetch();

            var response = await client.PutAsJsonAsync(url, request, options: SharedAPI.JsonOptions);

            if (response.IsSuccessStatusCode is false)
                return RestResponse.From(response);

            return true;
        }
        public async Task<WslaResponse<TResponse, RestResponse>> PUT<TRequest, TResponse>(IPAddress address, ushort port, string path, TRequest request)
        {
            var url = UrlCache.Get(address, port, path);
            var client = ClientCycle.Fetch();

            var response = await client.PutAsJsonAsync(url, request, options: SharedAPI.JsonOptions);

            if (response.IsSuccessStatusCode is false)
                return RestResponse.From(response);

            TResponse value;

            try
            {
                value = await response.Content.ReadFromJsonAsync<TResponse>();
            }
            catch (Exception ex)
            {
                return RestResponse.From(ex);
            }

            return value;
        }

        public async Task<WslaResponse<RestResponse>> POST<TRequest>(IPAddress address, ushort port, string path, TRequest request)
        {
            var url = UrlCache.Get(address, port, path);
            var client = ClientCycle.Fetch();

            var response = await client.PostAsJsonAsync(url, request, options: SharedAPI.JsonOptions);

            if (response.IsSuccessStatusCode is false)
                return RestResponse.From(response);

            return true;
        }
        public async Task<WslaResponse<TResponse, RestResponse>> POST<TRequest, TResponse>(IPAddress address, ushort port, string path, TRequest request)
        {
            var url = UrlCache.Get(address, port, path);
            var client = ClientCycle.Fetch();

            var response = await client.PostAsJsonAsync(url, request, options: SharedAPI.JsonOptions);

            if (response.IsSuccessStatusCode is false)
                return RestResponse.From(response);

            TResponse value;

            try
            {
                value = await response.Content.ReadFromJsonAsync<TResponse>(options: SharedAPI.JsonOptions);
            }
            catch (Exception ex)
            {
                return RestResponse.From(ex);
            }

            return value;
        }

        public RestAPI()
        {
            ClientCycle = new AutoCyclingValue<HttpClient>(TimeSpan.FromMinutes(15), () => new HttpClient());
            UrlCache = new UrlStringCache();
        }
    }
}
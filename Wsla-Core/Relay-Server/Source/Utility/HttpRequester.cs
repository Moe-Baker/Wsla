using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using System;
using System.Net.Http;

namespace Wsla.Server
{
    public class HttpRequester
    {
        readonly HttpClient Client;
        readonly UrlStringCache UrlCache;

        readonly JsonSerializerOptions JsonOptions;

        public async Task<WslaResponse<T, RestResponse>> GET<T>(IPAddress address, ushort port, string path)
        {
            var url = UrlCache.Get(address, port, path);

            var response = await Client.GetAsync(url);

            if (response.IsSuccessStatusCode is false)
                return RestResponse.From(response);

            T value;

            try
            {
                value = await response.Content.ReadFromJsonAsync<T>(options: JsonOptions);
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

            var response = await Client.PutAsJsonAsync(url, request, options: JsonOptions);

            if (response.IsSuccessStatusCode is false)
                return RestResponse.From(response);

            return true;
        }
        public async Task<WslaResponse<TResponse, RestResponse>> PUT<TRequest, TResponse>(IPAddress address, ushort port, string path, TRequest request)
        {
            var url = UrlCache.Get(address, port, path);

            var response = await Client.PutAsJsonAsync(url, request, options: JsonOptions);

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

            var response = await Client.PostAsJsonAsync(url, request, options: JsonOptions);

            if (response.IsSuccessStatusCode is false)
                return RestResponse.From(response);

            return true;
        }
        public async Task<WslaResponse<TResponse, RestResponse>> POST<TRequest, TResponse>(IPAddress address, ushort port, string path, TRequest request)
        {
            var url = UrlCache.Get(address, port, path);

            var response = await Client.PostAsJsonAsync(url, request, options: JsonOptions);

            if (response.IsSuccessStatusCode is false)
                return RestResponse.From(response);

            TResponse value;

            try
            {
                value = await response.Content.ReadFromJsonAsync<TResponse>(options: JsonOptions);
            }
            catch (Exception ex)
            {
                return RestResponse.From(ex);
            }

            return value;
        }

        public HttpRequester(JsonSerializerOptions JsonOptions)
        {
            Client = new HttpClient(new SocketsHttpHandler()
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(15)
            });

            UrlCache = new UrlStringCache();

            this.JsonOptions = JsonOptions;
        }
    }
}
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using System.Net.Http.Json;
using System.Threading;

namespace Wsla.Unity
{
    [Serializable]
    public class RestAPI : NetworkAPI.Property
    {
        UrlStringCache UrlCache;

        AutoCyclingValue<HttpClient> ClientCycle;

        IPAddress Address => API.CoordinatorAddress.IP;
        ushort Port => Constants.CoordinatorHttpPort;

        public override void Set(NetworkAPI value)
        {
            base.Set(value);

            ClientCycle = new AutoCyclingValue<HttpClient>(TimeSpan.FromMinutes(15), () => new HttpClient());
            UrlCache = new UrlStringCache();

            API.OnDispose += DisposeCallback;
        }

        void DisposeCallback()
        {
            ClientCycle.Dispose();
            ClientCycle = default;
        }

        public async Task<WslaResponse<T, RestResponse>> GET<T>(string path, CancellationToken cancellation = default)
        {
            var url = UrlCache.Get(Address, Port, path);
            var client = ClientCycle.Fetch();

            try
            {
                var response = await client.GetAsync(url, cancellationToken: cancellation);

                if (response.IsSuccessStatusCode is false)
                    return RestResponse.From(response);

                if (response.StatusCode is HttpStatusCode.NoContent)
                    return WslaResponse<T, RestResponse>.FromResult(default);

                return await response.Content.ReadFromJsonAsync<T>(options: SharedAPI.JsonOptions, cancellationToken: cancellation);
            }
            catch (Exception ex)
            {
                return RestResponse.From(ex);
            }
        }

        public async Task<WslaResponse<RestResponse>> PUT<TRequest>(string path, TRequest request, CancellationToken cancellation = default)
        {
            var url = UrlCache.Get(Address, Port, path);
            var client = ClientCycle.Fetch();

            try
            {
                var response = await client.PutAsJsonAsync(url, request, options: SharedAPI.JsonOptions, cancellationToken: cancellation);

                if (response.IsSuccessStatusCode is false)
                    return RestResponse.From(response);

                return true;
            }
            catch (Exception ex)
            {
                return RestResponse.From(ex);
            }
        }
        public async Task<WslaResponse<TResponse, RestResponse>> PUT<TRequest, TResponse>(string path, TRequest request, CancellationToken cancellation = default)
        {
            var url = UrlCache.Get(Address, Port, path);
            var client = ClientCycle.Fetch();

            try
            {
                var response = await client.PutAsJsonAsync(url, request, options: SharedAPI.JsonOptions, cancellationToken: cancellation);

                if (response.IsSuccessStatusCode is false)
                    return RestResponse.From(response);

                if (response.StatusCode is HttpStatusCode.NoContent)
                    return WslaResponse<TResponse, RestResponse>.FromResult(default);

                return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: cancellation);
            }
            catch (Exception ex)
            {
                return RestResponse.From(ex);
            }
        }

        public async Task<WslaResponse<RestResponse>> POST<TRequest>(string path, TRequest request, CancellationToken cancellation = default)
        {
            var url = UrlCache.Get(Address, Port, path);
            var client = ClientCycle.Fetch();

            try
            {
                var response = await client.PostAsJsonAsync(url, request, options: SharedAPI.JsonOptions, cancellationToken: cancellation);

                if (response.IsSuccessStatusCode is false)
                    return RestResponse.From(response);

                return true;
            }
            catch (Exception ex)
            {
                return RestResponse.From(ex);
            }
        }
        public async Task<WslaResponse<TResponse, RestResponse>> POST<TRequest, TResponse>(string path, TRequest request, CancellationToken cancellation = default)
        {
            var url = UrlCache.Get(Address, Port, path);
            var client = ClientCycle.Fetch();

            try
            {
                var response = await client.PostAsJsonAsync(url, request, options: SharedAPI.JsonOptions, cancellationToken: cancellation);

                if (response.IsSuccessStatusCode is false)
                    return RestResponse.From(response);

                if (response.StatusCode is HttpStatusCode.NoContent)
                    return WslaResponse<TResponse, RestResponse>.FromResult(default);

                return await response.Content.ReadFromJsonAsync<TResponse>(options: SharedAPI.JsonOptions, cancellationToken: cancellation);
            }
            catch (Exception ex)
            {
                return RestResponse.From(ex);
            }
        }
    }
}
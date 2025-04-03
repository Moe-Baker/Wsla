using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using System.Threading;
using System.IO;
using LiteNetLib.Utils;
using System.Collections.Generic;
using Wsla.Serialization;
using UnityEngine;

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

        MemoryContent WriteContent<T>(in T value, INetworkStream stream)
        {
            NetworkSerializer.WriteValue(in value, stream);

            var memory = stream.PeekAllocatedMemory();

            return new MemoryContent(memory);
        }
        async ValueTask<T> ReadContent<T>(HttpContent content, CancellationToken cancellation)
        {
            var stream = await content.ReadAsStreamAsync();

            var destination = NetworkStreamPool.Rent();

            try
            {
                while (true)
                {
                    destination.EnsureFit(100);
                    var memory = destination.PeekAvailableMemory();

                    cancellation.ThrowIfCancellationRequested();
                    var read = await stream.ReadAsync(memory);

                    if (read is 0)
                        break;

                    destination.Position += read;
                }

                destination.Position = 0;

                return NetworkSerializer.ReadValue<T>(destination);
            }
            finally
            {
                NetworkStreamPool.Return(destination);
            }
        }

        async Task<HttpResponseMessage> SendRequest<TRequest>(string url, HttpMethod method, TRequest request, CancellationToken cancellation)
        {
            var medium = NetworkStreamPool.Rent();

            try
            {
                var message = new HttpRequestMessage(method, url)
                {
                    Content = WriteContent(in request, medium),
                };

                var client = ClientCycle.Fetch();

                return await client.SendAsync(message, cancellation);
            }
            finally
            {
                NetworkStreamPool.Return(medium);
            }
        }

        public async Task<WslaResponse<T, RestResponse>> GET<[NetworkSerializationMarker] T>(string path, CancellationToken cancellation = default)
        {
            var url = UrlCache.Get(Address, Port, path);
            var client = ClientCycle.Fetch();

            var medium = NetworkStreamPool.Rent();

            try
            {
                var response = await client.GetAsync(url, cancellationToken: cancellation);

                if (response.IsSuccessStatusCode is false)
                    return RestResponse.From(response);

                if (response.StatusCode is HttpStatusCode.NoContent)
                    return WslaResponse<T, RestResponse>.FromResult(default);

                return await ReadContent<T>(response.Content, cancellation);
            }
            catch (Exception ex)
            {
                return RestResponse.From(ex);
            }
            finally
            {
                NetworkStreamPool.Return(medium);
            }
        }

        public async Task<WslaResponse<TResponse, RestResponse>> POST<[NetworkSerializationMarker] TRequest, [NetworkSerializationMarker] TResponse>(string path, TRequest request, CancellationToken cancellation = default)
        {
            var url = UrlCache.Get(Address, Port, path);
            var client = ClientCycle.Fetch();

            try
            {
                var response = await SendRequest(url, HttpMethod.Post, request, cancellation);

                if (response.IsSuccessStatusCode is false)
                    return RestResponse.From(response);

                if (response.StatusCode is HttpStatusCode.NoContent)
                    return WslaResponse<TResponse, RestResponse>.FromResult(default);

                return await ReadContent<TResponse>(response.Content, cancellation);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return RestResponse.From(ex);
            }
        }

        static class NetworkStreamPool
        {
            static Stack<INetworkStream> Stack;

            public static INetworkStream Rent()
            {
                lock (Stack)
                {
                    if (Stack.TryPop(out var stream) is false)
                        stream = new NetDataWriter(true, 128);

                    return stream;
                }
            }

            public static void Return(INetworkStream stream)
            {
                stream.Position = 0;

                lock (Stack)
                {
                    Stack.Push(stream);
                }
            }

            static NetworkStreamPool()
            {
                Stack = new(10);
            }
        }
    }

    public class MemoryContent : HttpContent
    {
        ReadOnlyMemory<byte> Content;

        protected override bool TryComputeLength(out long length)
        {
            length = Content.Length;
            return true;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            return stream.WriteAsync(Content).AsTask();
        }

        public MemoryContent(ReadOnlyMemory<byte> Content)
        {
            this.Content = Content;
        }
    }
}
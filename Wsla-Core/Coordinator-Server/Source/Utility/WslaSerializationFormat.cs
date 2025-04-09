using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Basics;
using GenHTTP.Modules.Conversion.Serializers;

using LiteNetLib.Utils;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;

using Wsla.Serialization;

namespace Wsla.Server
{
    class WslaSerializationFormat : ISerializationFormat
    {
        public ValueTask<IResponseBuilder> SerializeAsync(IRequest request, object response)
        {
            var result = request.Respond()
                .Content(new WslaContent(response))
                .Type(ContentType.ApplicationOctetStream);

            return new ValueTask<IResponseBuilder>(result);
        }
        public async ValueTask<object> DeserializeAsync(Stream stream, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type)
        {
            var destination = NetworkStreamPool.Rent();

            try
            {
                while (true)
                {
                    destination.EnsureFit(100);
                    var memory = destination.PeekAvailableMemory();

                    var read = await stream.ReadAsync(memory);

                    if (read is 0)
                        break;

                    destination.Position += read;
                }

                destination.Position = 0;
                return NetworkSerializer.Implicit.ReadValue(type, destination);
            }
            finally
            {
                NetworkStreamPool.Return(destination);
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

        class WslaContent : IResponseContent
        {
            object Response;

            public ulong? Length => null;

            public ValueTask<ulong?> CalculateChecksumAsync() => new((ulong)Response.GetHashCode());

            public async ValueTask WriteAsync(Stream target, uint bufferSize)
            {
                var source = NetworkStreamPool.Rent();

                try
                {
                    var type = Response.GetType();
                    NetworkSerializer.Implicit.WriteValue(type, Response, source);

                    var memory = source.PeekAllocatedMemory();
                    await target.WriteAsync(memory);
                }
                finally
                {
                    NetworkStreamPool.Return(source);
                }
            }

            public WslaContent(object Response)
            {
                this.Response = Response;
            }
        }
    }
}
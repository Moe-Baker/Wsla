using LiteNetLib.Utils;

using Microsoft.AspNetCore.Mvc.Formatters;

using System;
using System.Threading.Tasks;

using Wsla.Serialization;

namespace Wsla.Server
{
    public static class WslaSerializationFormatters
    {
        public const string ContentType = Constants.WslaContentType;

        public class Input : InputFormatter
        {
            ObjectPool<INetworkStream> StreamPool;

            public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context)
            {
                var request = context.HttpContext.Request;
                var cancellation = context.HttpContext.RequestAborted;

                using var handle = StreamPool.Lease(out var stream);

                while (true)
                {
                    var length = (int)request.ContentLength.GetValueOrDefault(100);

                    stream.EnsureFit(length);

                    var memory = stream.PeekAvailableMemory();
                    var read = await request.Body.ReadAsync(memory, cancellation);

                    if (read is 0)
                        break;

                    stream.Position += read;
                }

                var cursor = stream.Position;

                if (cursor is 0)
                    return InputFormatterResult.NoValue();

                stream.Position = 0;

                try
                {
                    var model = NetworkSerializer.Implicit.ReadValue(context.ModelType, stream);

                    if (stream.Position != cursor)
                    {
                        NetworkLog.Error($"Read Mismatch, Total: {cursor}, Read {stream.Position}");
                        return InputFormatterResult.Failure();
                    }

                    return InputFormatterResult.Success(model);
                }
                catch (Exception ex)
                {
                    NetworkLog.Error("Failure on Wsla Binary Input Formatter");
                    NetworkLog.Error(ex);

                    return InputFormatterResult.Failure();
                }
            }

            public Input()
            {
                SupportedMediaTypes.Add(ContentType);

                StreamPool = new(() => new NetDataWriter(true))
                {
                    Reset = (x) => x.Position = 0
                };
            }
        }

        public class Output : OutputFormatter
        {
            ObjectPool<INetworkStream> StreamPool;

            public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context)
            {
                var response = context.HttpContext.Response;
                var cancellation = context.HttpContext.RequestAborted;

                context.ContentType = ContentType;

                using var handle = StreamPool.Lease(out var stream);

                NetworkSerializer.Implicit.WriteValue(context.ObjectType, context.Object, stream);

                var memory = stream.PeekAllocatedMemory();

                await response.BodyWriter.WriteAsync(memory, cancellation);
            }

            public Output()
            {
                SupportedMediaTypes.Add(ContentType);

                StreamPool = new(() => new NetDataWriter(true))
                {
                    Reset = (x) => x.Position = 0
                };
            }
        }
    }
}
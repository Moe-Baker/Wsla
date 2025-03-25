using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wsla
{
    public abstract class JsonWrapperConverter<TWrapper, TData> : JsonConverter<TWrapper>
    {
        public override TWrapper Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var data = JsonSerializer.Deserialize<TData>(ref reader, options: options);
            return CreateWrapper(data);
        }

        public override void Write(Utf8JsonWriter writer, TWrapper wrapper, JsonSerializerOptions options)
        {
            var data = ReadWrapper(wrapper);
            JsonSerializer.Serialize(writer, data, options: options);
        }

        public abstract TWrapper CreateWrapper(TData data);
        public abstract TData ReadWrapper(TWrapper wrapper);
    }
}
using System;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace Wsla
{
    public class MatchMakingValueJsonConverter : JsonConverter<MatchMakingValue>
    {
        public override MatchMakingValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Number:
                {
                    var number = reader.GetSingle();
                    return new MatchMakingValue(number);
                }

                case JsonTokenType.String:
                {
                    var text = reader.GetString();
                    return new MatchMakingValue(text);
                }

                default: throw new JsonException($"Can't Deserialize {reader.TokenType} Token as Match Making Value");
            }
        }

        public override void Write(Utf8JsonWriter writer, MatchMakingValue value, JsonSerializerOptions options)
        {
            switch (value.Type)
            {
                case MatchMakingValue.ValueType.Null:
                    writer.WriteNullValue();
                    break;

                case MatchMakingValue.ValueType.Number:
                    writer.WriteNumberValue(value.Number);
                    break;

                case MatchMakingValue.ValueType.Text:
                    writer.WriteStringValue(value.Text.AsSpan());
                    break;
            }
        }
    }
}
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Buffers;
using System.Text;
using System;

namespace Wsla
{
    public unsafe class FixedStringJsonConverter<TString> : JsonConverter<TString>
        where TString : IFixedString, new()
    {
        public override TString ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return ReadValue(ref reader);
        }
        public override void WriteAsPropertyName(Utf8JsonWriter writer, TString value, JsonSerializerOptions options)
        {
            var characters = value.AsSpan();
            writer.WritePropertyName(characters);
        }

        TString ReadValue(ref Utf8JsonReader reader)
        {
            if (reader.ValueIsEscaped)
                throw new JsonException($"Cannot Convert Escaped String to Fixed String");

            if (reader.HasValueSequence)
            {
                CheckBinarySize((int)reader.ValueSequence.Length);

                Span<byte> binary = stackalloc byte[(int)reader.ValueSequence.Length];
                reader.ValueSequence.CopyTo(binary);

                return ReadBinary(binary);
            }
            else
            {
                CheckBinarySize(reader.ValueSpan.Length);

                var binary = reader.ValueSpan;

                return ReadBinary(binary);
            }

            static void CheckBinarySize(int binary)
            {
                var max = Encoding.UTF8.GetMaxByteCount(FixedString.MaxCharacters);

                if (binary > max)
                    throw new JsonException($"Json Fixed String Bytes Longer than Possible Max of {max}");
            }
            static TString ReadBinary(ReadOnlySpan<byte> binary)
            {
                var value = new TString();

                var characters = value.GetTotalSpan();
                var length = Encoding.UTF8.GetChars(binary, characters);
                value.SetLength(length);

                return value;
            }
        }

        public override TString Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType is JsonTokenType.Null)
                return default;

            if (reader.TokenType is not JsonTokenType.String)
                throw new JsonException($"Cannot Convert {reader.TokenType} to Fixed String");

            return ReadValue(ref reader);
        }
        public override void Write(Utf8JsonWriter writer, TString value, JsonSerializerOptions options)
        {
            var characters = value.AsSpan();
            writer.WriteStringValue(characters);
        }
    }
}
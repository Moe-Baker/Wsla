using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wsla
{
    public class NetworkVersionJsonConverter : JsonConverter<NetworkVersion>
    {
        public static readonly int MaxCharactersLength = NetworkVersion.MaxVersionCharacterLength;
        public static readonly int MaxBinaryLength = Encoding.UTF8.GetMaxByteCount(MaxCharactersLength);

        public override NetworkVersion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType is JsonTokenType.Null)
                return default;

            if (reader.TokenType is not JsonTokenType.String)
                throw new JsonException($"Cannot Convert {reader.TokenType} to Network Version");

            if (reader.ValueIsEscaped)
                throw new JsonException($"Cannot Convert Escaped String to Network Version");

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

            static void CheckBinarySize(int length)
            {
                if (length > MaxBinaryLength)
                    throw new JsonException($"Binary Length of {length} Is Bigger than Max Binary Length of {MaxBinaryLength}");
            }
            static NetworkVersion ReadBinary(ReadOnlySpan<byte> binary)
            {
                Span<char> characters = stackalloc char[Encoding.UTF8.GetMaxCharCount(binary.Length)];

                var written = Encoding.UTF8.GetChars(binary, characters);

                characters = characters.Slice(0, written);

                if (NetworkVersion.TryParse(characters, out var address) is false)
                    throw new InvalidOperationException($"Can't Convert {characters.ToString()} to Network Version");

                return address;
            }
        }

        public override void Write(Utf8JsonWriter writer, NetworkVersion value, JsonSerializerOptions options)
        {
            Span<char> buffer = stackalloc char[NetworkVersion.MaxVersionCharacterLength];

            if (value.TryFormat(buffer, out var written) is false)
                throw new NotImplementedException();

            buffer = buffer.Slice(0, written);

            writer.WriteStringValue(buffer);
        }
    }
}
using System.Net;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Buffers;
using System.Text;
using System;

namespace Wsla
{
    public class IPAddressJsonConverter : JsonConverter<IPAddress>
    {
        public const int MaxCharactersLength = 50;
        public const int MaxBinaryLength = MaxCharactersLength * 4;

        public override IPAddress Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType is JsonTokenType.Null)
                return null;

            if (reader.TokenType is not JsonTokenType.String)
                throw new JsonException($"Cannot Convert {reader.TokenType} to IP Address");

            if (reader.ValueIsEscaped)
                throw new JsonException($"Cannot Convert Escaped String to IP Address");

            if (reader.HasValueSequence)
            {
                var length = (int)reader.ValueSequence.Length;

                if (length > MaxBinaryLength)
                    throw new JsonException($"Binary Length of {length} Is Bigger than Max Binary Length of {MaxBinaryLength}");

                Span<byte> binary = stackalloc byte[length];
                reader.ValueSequence.CopyTo(binary);

                return ReadBinary(binary);
            }
            else
            {
                var length = reader.ValueSpan.Length;

                if (length > MaxBinaryLength)
                    throw new JsonException($"Binary Length of {length} Is Bigger than Max Binary Length of {MaxBinaryLength}");

                var binary = reader.ValueSpan;

                return ReadBinary(binary);
            }

            static IPAddress ReadBinary(ReadOnlySpan<byte> binary)
            {
                var length = Encoding.UTF8.GetMaxCharCount(binary.Length);

                Span<char> characters = stackalloc char[length];

                var written = Encoding.UTF8.GetChars(binary, characters);

                characters = characters.Slice(0, written);

                if (IPAddress.TryParse(characters, out var address) is false)
                    throw new InvalidOperationException($"Can't Convert {characters.ToString()} to IP Address");

                return address;
            }
        }

        public override void Write(Utf8JsonWriter writer, IPAddress value, JsonSerializerOptions options)
        {
            Span<char> characters = stackalloc char[MaxCharactersLength];

            if (value.TryFormat(characters, out var written) is false)
                throw new NotImplementedException();

            var slice = characters.Slice(0, written);

            writer.WriteStringValue(slice);
        }
    }
}
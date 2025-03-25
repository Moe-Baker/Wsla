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
        public static readonly int MaxCharactersLength = 50; //An educated guess
        public static readonly int MaxBinaryLength = Encoding.UTF8.GetMaxByteCount(MaxCharactersLength);

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
            static IPAddress ReadBinary(ReadOnlySpan<byte> binary)
            {
                Span<char> characters = stackalloc char[Encoding.UTF8.GetMaxCharCount(binary.Length)];

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
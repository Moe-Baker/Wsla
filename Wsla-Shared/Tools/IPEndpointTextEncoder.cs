using System;
using System.Net;

namespace Wsla
{
    public static class IPEndpointTextEncoder
    {
        /// <summary>
        /// Table of Characters to Use
        /// </summary>
        static char[] CharacterTable { get; }
        static bool TryIndex(char character, out byte index)
        {
            character = char.ToUpper(character);

            for (index = 0; index < CharacterTable.Length; index++)
                if (character == CharacterTable[index])
                    return true;

            return false;
        }

        /// <summary>
        /// Size of Binary for an IPv4 Address (4) + Port (2)
        /// </summary>
        static readonly int BinarySize = 4 + 2;

        /// <summary>
        /// Number of Characters for an Input, (<see cref="BinarySize"/> * 2) Because Each Binary Takes 2 Characters to Encode
        /// </summary>
        static readonly int CharacterCount = BinarySize * 2;

        /// <summary>
        /// An Offset to Include with the Index of the Octet of the Binary to Reduce Repetition
        /// </summary>
        static readonly byte BinaryScrambleOffset = 1;

        public static string Encode(IPAddress address, ushort port)
        {
            if (address == null)
                throw new ArgumentException($"Can't Encode Null IP Address");

            if (address.AddressFamily is not System.Net.Sockets.AddressFamily.InterNetwork)
                throw new ArgumentException($"Only Supports IPv4");

            Span<byte> binary = stackalloc byte[BinarySize];

            if (address.TryWriteBytes(binary, out _) is false)
                throw new NotImplementedException();

            if (BitConverter.TryWriteBytes(binary.Slice(4), port) is false)
                throw new NotImplementedException();

            Span<char> characters = stackalloc char[CharacterCount];

            for (int i = 0; i < binary.Length; i++)
            {
                var octet = binary[i] + (BinaryScrambleOffset * i);

                var div = octet / CharacterTable.Length;
                var rem = octet % CharacterTable.Length;

                characters[(i * 2)] = CharacterTable[div];
                characters[(i * 2) + 1] = CharacterTable[rem];
            }

            return new string(characters);
        }

        public static bool TryDecode(ReadOnlySpan<char> characters, out IPAddress address, out ushort port)
        {
            characters = characters.Trim();

            if (characters.Length != CharacterCount)
            {
                address = default;
                port = default;
                return false;
            }

            Span<byte> binary = stackalloc byte[BinarySize];

            for (byte i = 0; i < characters.Length / 2; i++)
            {
                if (TryIndex(characters[(i * 2)], out var div) is false || TryIndex(characters[(i * 2) + 1], out var rem) is false)
                {
                    address = default;
                    port = default;
                    return false;
                }

                binary[i] = (byte)((div * CharacterTable.Length) + rem - (BinaryScrambleOffset * i));
            }

            address = new IPAddress(binary.Slice(0, 4));
            port = BitConverter.ToUInt16(binary.Slice(4, 2));
            return true;
        }

        static IPEndpointTextEncoder()
        {
            CharacterTable = new char[]
            {
                'A', 'C', 'D', 'E', 'F', 'G', 'H', 'J', 'K', 'L', 'M', 'N', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', //Alphabet Except [O, I, B, P]
                '2', '3', '4', '5', '6', '7', '8', '9', //Numbers Except [1]
            };

            for (int i = 0; i < CharacterTable.Length / 2; i++)
            {
                if (i % 2 is 0)
                {
                    ref var original = ref CharacterTable[i];
                    ref var replacement = ref CharacterTable[^(i + 1)];

                    (original, replacement) = (replacement, original);
                }
            }
        }
    }
}
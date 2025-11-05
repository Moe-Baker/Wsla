using System;

using Wsla.Serialization;

namespace Wsla
{
    [NetworkBlittable]
    public struct NetworkVersion : IEquatable<NetworkVersion>, IComparable<NetworkVersion>
    {
        public byte Major { get; }
        public byte Minor { get; }
        public byte Patch { get; }

        /// <summary>
        /// Numerical representation of the version, used for comparisons
        /// </summary>
        public uint Numerical => (uint)Binary;

        /// <summary>
        /// Binary representation of the version
        /// </summary>
        int Binary => (Major << 16) | (Minor << 8) | (Patch);

        public const char SplitCharacter = '.';
        public const int MaxOctetCharacterLength = 3;
        public const int MaxVersionCharacterLength = (MaxOctetCharacterLength * 3) + (2);

        public override bool Equals(object obj)
        {
            if (obj is NetworkVersion other)
                return Equals(other);

            return false;
        }
        public bool Equals(NetworkVersion other)
        {
            return (Major == other.Major) && (Minor == other.Minor) && (Patch == other.Patch);
        }

        public override string ToString() => $"{Major}{SplitCharacter}{Minor}{SplitCharacter}{Patch}";
        public override int GetHashCode() => Binary;

        public int CompareTo(NetworkVersion other) => Numerical.CompareTo(other.Numerical);

        public NetworkVersion(byte major, byte minor, byte patch)
        {
            this.Major = major;
            this.Minor = minor;
            this.Patch = patch;
        }

        public static bool operator ==(NetworkVersion left, NetworkVersion right) => left.Equals(right);
        public static bool operator !=(NetworkVersion left, NetworkVersion right) => !left.Equals(right);

        public static bool operator >(NetworkVersion left, NetworkVersion right) => left.Numerical > right.Numerical;
        public static bool operator <(NetworkVersion left, NetworkVersion right) => left.Numerical < right.Numerical;

        public static bool TryParse(string text, out NetworkVersion version)
        {
            var span = text.AsSpan();
            return TryParse(span, out version);
        }
        public static bool TryParse(ReadOnlySpan<char> characters, out NetworkVersion version)
        {
            if (TrySplit(ref characters, out var major) is false)
            {
                version = default;
                return false;
            }
            if (TrySplit(ref characters, out var minor) is false)
            {
                version = default;
                return false;
            }
            if (TrySplit(ref characters, out var patch) is false)
            {
                version = default;
                return false;
            }
            static bool TrySplit(ref ReadOnlySpan<char> characters, out byte number)
            {
                var index = characters.IndexOf(SplitCharacter);

                ReadOnlySpan<char> octet;

                if (index < 0)
                {
                    octet = characters;
                    characters = default;
                }
                else
                {
                    octet = characters.Slice(0, index);
                    characters = characters.Slice(index + 1);
                }

                if (octet.Length == 0)
                {
                    number = 0;
                    return true;
                }

                return byte.TryParse(octet, out number);
            }

            version = new NetworkVersion(major, minor, patch);
            return true;
        }
    }
}
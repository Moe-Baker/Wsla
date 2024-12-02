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
            var parts = text.Split('.');

            if (TryParseSegment(parts, 0, out var major) is false)
            {
                version = default;
                return false;
            }

            if (TryParseSegment(parts, 1, out var minor) is false)
            {
                version = default;
                return false;
            }

            if (TryParseSegment(parts, 2, out var patch) is false)
            {
                version = default;
                return false;
            }

            static bool TryParseSegment(string[] parts, int index, out byte destination)
            {
                if (index >= parts.Length)
                {
                    destination = 0;
                    return false;
                }

                return byte.TryParse(parts[index], out destination);
            }

            version = new NetworkVersion(major, minor, patch);
            return true;
        }
    }
}
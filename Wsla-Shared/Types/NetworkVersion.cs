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
    }
}
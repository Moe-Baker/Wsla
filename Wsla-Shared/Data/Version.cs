using System;

namespace Wsla.Shared
{
    public struct Version : IEquatable<Version>
    {
        public byte Major { get; }
        public byte Minor { get; }
        public byte Patch { get; }

        public int Numerical
        {
            get
            {
                int value = 0;

                value |= Major;
                value <<= 8;

                value |= Minor;
                value <<= 8;

                value |= Patch;
                value <<= 8;

                return value;
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is Version other)
                return Equals(other);

            return false;
        }
        public bool Equals(Version other)
        {
            return Major == other.Major && Minor == other.Minor && Patch == other.Patch;
        }

        public override int GetHashCode() => Numerical;

        public Version(byte major, byte minor, byte patch)
        {
            this.Major = major;
            this.Minor = minor;
            this.Patch = patch;
        }

        public static bool operator ==(Version right, Version left) => right.Equals(left);
        public static bool operator !=(Version right, Version left) => !right.Equals(left);
    }
}
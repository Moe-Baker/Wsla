using System;

using Wsla.Serialization;

namespace Wsla
{
    [NetworkBlittable]
    public partial struct ApplicationID : IEquatable<ApplicationID>
    {
        public byte Value { get; }

        public override bool Equals(object obj)
        {
            if (obj is ApplicationID other)
                return Equals(other);

            return false;
        }
        public bool Equals(ApplicationID other)
        {
            return Value == other.Value;
        }

        public override int GetHashCode() => Value;

        public override string ToString() => Value.ToString();

        public ApplicationID(byte value)
        {
            this.Value = value;
        }

        public static ApplicationID Min { get; } = new(byte.MinValue);
        public static ApplicationID Max { get; } = new(byte.MaxValue - 1);
        public static ApplicationID None { get; } = new(byte.MaxValue);

        public static bool operator ==(ApplicationID left, ApplicationID right) => left.Equals(right);
        public static bool operator !=(ApplicationID left, ApplicationID right) => !left.Equals(right);

        public static bool Increment(ref ApplicationID index, out ApplicationID key)
        {
            if (index.Value >= Max.Value)
            {
                key = default;
                return false;
            }

            key = index;
            index = new ApplicationID((byte)(index.Value + 1));

            return true;
        }
    }
}
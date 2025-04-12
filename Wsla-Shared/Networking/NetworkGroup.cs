using System;
using System.Text;

using Wsla.Serialization;

namespace Wsla
{
    [NetworkBlittable]
    public partial struct NetworkGroupID : IEquatable<NetworkGroupID>
    {
        public byte Value { get; }

        public override bool Equals(object obj)
        {
            if (obj is NetworkGroupID other)
                return Equals(other);

            return false;
        }
        public bool Equals(NetworkGroupID other)
        {
            return Value == other.Value;
        }

        public override int GetHashCode() => Value;

        public override string ToString() => Value.ToString();

        public NetworkGroupID(byte value)
        {
            if (value >= NetworkGroupCollection.Capacity)
                throw new ArgumentOutOfRangeException(nameof(value), value, $"Max Group Number is {NetworkGroupCollection.Capacity - 1}");

            this.Value = value;
        }

        public static NetworkGroupID Min { get; } = new(0);
        public static NetworkGroupID Max { get; } = new(NetworkGroupCollection.Capacity - 1);

        public static bool operator ==(NetworkGroupID left, NetworkGroupID right) => left.Equals(right);
        public static bool operator !=(NetworkGroupID left, NetworkGroupID right) => !left.Equals(right);

        public static NetworkGroupCollection operator +(NetworkGroupID left, NetworkGroupID right)
        {
            return NetworkGroupCollection.From(left) + NetworkGroupCollection.From(right);
        }
        public static NetworkGroupCollection operator -(NetworkGroupID left, NetworkGroupID right)
        {
            return NetworkGroupCollection.From(left) - NetworkGroupCollection.From(right);
        }
    }

    [NetworkBlittable]
    public partial struct NetworkGroupCollection : IEquatable<NetworkGroupCollection>
    {
        public byte Value { get; }

        public const int Capacity = 8;

        public bool IsEmpty => Value is 0;
        public bool IsEveryone => Value is byte.MaxValue;

        public bool Contains(NetworkGroupID group)
        {
            var mask = 1 << group.Value;
            return (Value & mask) != 0;
        }

        public bool Intersects(NetworkGroupCollection collection)
        {
            return (Value & collection.Value) != 0;
        }

        public override bool Equals(object obj)
        {
            if (obj is NetworkGroupCollection other)
                return Equals(other);

            return false;
        }
        public bool Equals(NetworkGroupCollection other)
        {
            return Value == other.Value;
        }

        public override int GetHashCode() => Value;

        public override string ToString()
        {
            if (IsEveryone)
                return "[Everyone]";

            if (IsEmpty)
                return "[Empty]";

            var builder = new StringBuilder();

            builder.Append('[');

            var first = true;
            for (byte i = 0; i < Capacity; i++)
            {
                var group = new NetworkGroupID(i);

                if (Contains(group) is false)
                    continue;

                if (first)
                    first = false;
                else
                    builder.Append(", ");

                builder.Append(group);
            }

            builder.Append(']');

            return builder.ToString();
        }

        public NetworkGroupCollection(byte value)
        {
            this.Value = value;
        }
        public NetworkGroupCollection(NetworkGroupID group)
        {
            Value = (byte)(1 << group.Value);
        }

        public static NetworkGroupCollection Empty => new NetworkGroupCollection(byte.MinValue);
        public static NetworkGroupCollection Everyone => new NetworkGroupCollection(byte.MaxValue);

        public static bool operator ==(NetworkGroupCollection left, NetworkGroupCollection right) => left.Equals(right);
        public static bool operator !=(NetworkGroupCollection left, NetworkGroupCollection right) => !left.Equals(right);

        public static NetworkGroupCollection From(NetworkGroupID group) => new NetworkGroupCollection(group);

        public static implicit operator NetworkGroupCollection(NetworkGroupID group) => new NetworkGroupCollection(group);

        public static NetworkGroupCollection Combine(NetworkGroupCollection collectionA, NetworkGroupCollection collectionB)
        {
            var value = (byte)(collectionA.Value | collectionB.Value);
            return new NetworkGroupCollection(value);
        }
        public static NetworkGroupCollection Subtract(NetworkGroupCollection collectionA, NetworkGroupCollection collectionB)
        {
            var value = (byte)(collectionA.Value & ~collectionB.Value);
            return new NetworkGroupCollection(value);
        }

        public static NetworkGroupCollection operator +(NetworkGroupCollection left, NetworkGroupCollection right)
        {
            return Combine(left, right);
        }
        public static NetworkGroupCollection operator -(NetworkGroupCollection left, NetworkGroupCollection right)
        {
            return Subtract(left, right);
        }

        public static NetworkGroupCollection operator +(NetworkGroupCollection left, NetworkGroupID right)
        {
            return Combine(left, right);
        }
        public static NetworkGroupCollection operator -(NetworkGroupCollection left, NetworkGroupID right)
        {
            return Subtract(left, right);
        }

        public static NetworkGroupCollection operator +(NetworkGroupID left, NetworkGroupCollection right)
        {
            return Combine(left, right);
        }
        public static NetworkGroupCollection operator -(NetworkGroupID left, NetworkGroupCollection right)
        {
            return Subtract(left, right);
        }
    }
}
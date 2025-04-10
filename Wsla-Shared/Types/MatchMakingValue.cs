using System;
using System.Collections.Generic;

using Wsla.Serialization;

namespace Wsla
{
    public struct MatchMakingValue : IEquatable<MatchMakingValue>, IAutoNetworkSerialization
    {
        public ValueType Type;
        public enum ValueType : byte
        {
            Null, Number, Text
        }

        public bool IsNull => Type is ValueType.Null;

        public float Number;
        public FixedString<FS20> Text;

        public const float Epsilon = 0.01f;

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref Type);

            switch (Type)
            {
                case ValueType.Null: break;

                case ValueType.Number:
                    context.Select(ref Number);
                    break;

                case ValueType.Text:
                    context.Select(ref Text);
                    break;

                default: throw new NotImplementedException();
            }
        }

        public MatchMakingValue(float Number)
        {
            Type = ValueType.Number;
            this.Number = Number;

            Text = default;
        }
        public MatchMakingValue(FixedString<FS20> Text)
        {
            Type = ValueType.Text;
            this.Text = Text;

            Number = default;
        }

        public override bool Equals(object obj)
        {
            if (obj is MatchMakingValue other)
                return Equals(other);

            return false;
        }
        public bool Equals(MatchMakingValue other)
        {
            if (Type != other.Type)
                return false;

            switch (Type)
            {
                case ValueType.Number:
                    return MathF.Abs(Number - other.Number) <= Epsilon;

                case ValueType.Text:
                    return Text.Equals(other.Text);

                default: throw new NotImplementedException();
            }
        }

        public override int GetHashCode()
        {
            return Type switch
            {
                ValueType.Null => 0,

                ValueType.Number => HashCode.Combine(Type, Number),
                ValueType.Text => HashCode.Combine(Type, Text),

                _ => throw new NotImplementedException(),
            };
        }

        public override string ToString()
        {
            return Type switch
            {
                ValueType.Null => "Null",

                ValueType.Number => Number.ToString(),
                ValueType.Text => Text.ToString(),

                _ => throw new NotImplementedException(),
            };
        }

        public static bool operator ==(in MatchMakingValue left, in MatchMakingValue right) => left.Equals(right);
        public static bool operator !=(in MatchMakingValue left, in MatchMakingValue right) => !left.Equals(right);
    }

    public struct MatchMakingParameters : IAutoNetworkSerialization
    {
        public List<Entry> Entries;
        public struct Entry : IAutoNetworkSerialization
        {
            public FixedString<FS20> Name;
            public MatchMakingValue Value;

            public void Select(ref AutoSerializationContext context)
            {
                context.Select(ref Name);
                context.Select(ref Value);
            }

            public Entry(in FixedString<FS20> Name, in MatchMakingValue Value)
            {
                this.Name = Name;
                this.Value = Value;
            }
        }

        public int Count
        {
            get
            {
                if (Entries == null)
                    return 0;

                return Entries.Count;
            }
        }

        public bool TryGet(in FixedString<FS20> name, out MatchMakingValue value)
        {
            if (Entries == null)
            {
                value = default;
                return false;
            }

            foreach (var entry in Entries)
            {
                if (entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    value = entry.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        public MatchMakingParameters Add(in FixedString<FS20> name, in float value) => Add(in name, new MatchMakingValue(value));
        public MatchMakingParameters Add(in FixedString<FS20> name, in FixedString<FS20> value) => Add(in name, new MatchMakingValue(value));
        public MatchMakingParameters Add(in FixedString<FS20> name, in MatchMakingValue value)
        {
            Entries ??= new(1);

            var entry = new Entry(in name, in value);
            Entries.Add(entry);

            return this;
        }

        public void Select(ref AutoSerializationContext context)
        {
            context.Select(ref Entries);
        }

        public MatchMakingParameters(List<Entry> Entries)
        {
            this.Entries = Entries;
        }

        public static MatchMakingParameters Empty => default;

        public static MatchMakingParameters New()
        {
            var list = new List<Entry>();
            return new(list);
        }
    }
}
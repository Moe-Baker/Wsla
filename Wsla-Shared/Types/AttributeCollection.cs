using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

using Wsla.Serialization;

[assembly: InternalsVisibleTo("Wsla-Unit-Tests")]

namespace Wsla
{
    [JsonConverter(typeof(WrapperJsonConverter))]
    public class AttributeCollection : IManualNetworkSerialization
    {
        public Dictionary<FixedString<FS20>, FixedString<FS40>> Dictionary;

        public bool TryGetValue(FixedString<FS20> key, out FixedString<FS40> value) => Dictionary.TryGetValue(key, out value);

        public bool TryParseValue<T>(FixedString<FS20> key, out T value)
        {
            if (Dictionary.TryGetValue(key, out var text) is false)
            {
                value = default;
                return false;
            }

            return TextValueConverter.TryParse(text, out value);
        }

        public void SetValue(FixedString<FS20> key, FixedString<FS40> value)
        {
            Dictionary[key] = value;
        }
        public void SetValue(FixedString<FS20> key, string value)
        {
            Dictionary[key] = new FixedString<FS20>(value);
        }
        public void SetValue(FixedString<FS20> key, ReadOnlySpan<char> value)
        {
            Dictionary[key] = new FixedString<FS20>(value);
        }
        public void SetValue<T>(FixedString<FS20> key, T value)
        {
            var text = new FixedString<FS40>();

            var span = text.GetTotalSpan();

            if (TextValueConverter.TryFormat(value, span, out var written) is false)
                throw new ArgumentOutOfRangeException($"Argument ({value}) Can't Fit into {text.Max} Characters");

            text.SetLength(written);

            Dictionary[key] = text;
        }

        public void Write(INetworkStream stream)
        {
            NetworkSerializer.WriteValue((byte)Dictionary.Count, stream);

            foreach (var (key, value) in Dictionary)
            {
                NetworkSerializer.WriteValue(in key, stream);
                NetworkSerializer.WriteValue(in value, stream);
            }
        }
        public void Read(INetworkStream stream)
        {
            var length = NetworkSerializer.ReadValue<byte>(stream);

            if (Dictionary is null)
            {
                Dictionary = CreateDictionary(length);
            }
            else
            {
                Dictionary.Clear();
                Dictionary.EnsureCapacity(length);
            }

            for (int i = 0; i < length; i++)
            {
                var key = NetworkSerializer.ReadValue<FixedString<FS20>>(stream);
                var value = NetworkSerializer.ReadValue<FixedString<FS40>>(stream);

                Dictionary.Add(key, value);
            }
        }

        public Dictionary<FixedString<FS20>, FixedString<FS40>>.Enumerator GetEnumerator() => Dictionary.GetEnumerator();

        public AttributeCollection() : this(0) { }
        public AttributeCollection(int capacity) : this(CreateDictionary(capacity)) { }
        public AttributeCollection(Dictionary<FixedString<FS20>, FixedString<FS40>> Dictionary)
        {
            this.Dictionary = Dictionary;
        }

        static Dictionary<FixedString<FS20>, FixedString<FS40>> CreateDictionary(int capacity)
        {
            return new Dictionary<FixedString<FS20>, FixedString<FS40>>(capacity);
        }

        public class WrapperJsonConverter : JsonWrapperConverter<AttributeCollection, Dictionary<FixedString<FS20>, FixedString<FS40>>>
        {
            public override AttributeCollection CreateWrapper(Dictionary<FixedString<FS20>, FixedString<FS40>> data)
            {
                return new AttributeCollection(data);
            }
            public override Dictionary<FixedString<FS20>, FixedString<FS40>> ReadWrapper(AttributeCollection wrapper)
            {
                return wrapper.Dictionary;
            }
        }
    }
}
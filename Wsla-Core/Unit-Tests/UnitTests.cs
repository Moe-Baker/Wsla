using LiteNetLib.Utils;

using System.Net;
using System.Text;
using System.Text.Json;

using Wsla;
using Wsla.Serialization;

namespace NetworkSerializationTests
{
    public class StringTests
    {
        [Fact]
        public void NullStringTest()
        {
            string? source = null;
            var clone = Utility.Duplicate(source);

            Assert.Equal(source, clone);
        }

        [Fact]
        public void ShortStringTest()
        {
            var source = "Hello World";
            var clone = Utility.Duplicate(source);

            Assert.Equal(source, clone);
        }

        [Fact]
        public void LongStringTest()
        {
            var size = StringNetworkSerializationResolver.StackOptimizationLimit * 2;

            var builder = new StringBuilder(size);

            for (int i = 0; i < size; i++)
                builder.Append((char)i);

            var source = builder.ToString();
            var clone = Utility.Duplicate(source);

            Assert.Equal(source, clone);
        }
    }

    public class FixedStringTests
    {
        [Fact]
        void Fixed20Test()
        {
            var source = new FixedString<FS20>("Hello World");
            var clone = Utility.Duplicate(source);

            Assert.Equal(source.ToString(), clone.ToString());
        }
        [Fact]
        void Fixed40Test()
        {
            var source = new FixedString<FS40>("Hello World");
            var clone = Utility.Duplicate(source);

            Assert.Equal(source.ToString(), clone.ToString());
        }
        [Fact]
        void Fixed60Test()
        {
            var source = new FixedString<FS60>("Hello World");
            var clone = Utility.Duplicate(source);

            Assert.Equal(source.ToString(), clone.ToString());
        }
        [Fact]
        void Fixed80Test()
        {
            var source = new FixedString<FS80>("Hello World");
            var clone = Utility.Duplicate(source);

            Assert.Equal(source.ToString(), clone.ToString());
        }

        [Fact]
        void MinTestTest()
        {
            var source = new FixedString<FS20>("");
            var clone = Utility.Duplicate(source);

            Assert.Equal(source.ToString(), clone.ToString());
        }

        [Fact]
        void MaxTest()
        {
            var source = new FixedString<FS80>(new string(char.MaxValue, 80));
            var clone = Utility.Duplicate(source);

            Assert.Equal(source.ToString(), clone.ToString());
        }

        [Fact]
        void ImplicitConversion()
        {
            FixedString<FS20> f20 = new FixedString<FS20>("Hello World");

            FixedString<FS40> f40 = f20;
            Assert.True(f20 == f40);

            FixedString<FS60> f60 = f20;
            Assert.True(f20 == f60);

            FixedString<FS80> f80 = f20;
            Assert.True(f20 == f80);
        }

        [Fact]
        void BinaryLengthTest()
        {
            var instance = new FixedString<FS20>("Hello World");

            var writer = new NetDataWriter();

            NetworkSerializer.WriteValue(in instance, writer);

            Assert.Equal(writer.Length, instance.Length + 1);
        }
    }

    public class TupleTests
    {
        [Fact]
        public void EmptyTest()
        {
            var original = new ValueTuple();
            var clone = Utility.Duplicate(original);

            Assert.Equal(original, clone);
        }

        [Fact]
        public void Item2Test()
        {
            var original = ValueTuple.Create(1, "Hello World");
            var clone = Utility.Duplicate(original);

            Assert.Equal(original.Item1, clone.Item1);
            Assert.Equal(original.Item2, clone.Item2);
        }

        [Fact]
        public void Item4Test()
        {
            var original = ValueTuple.Create(1, "Hello World", 2.5f, long.MaxValue);
            var clone = Utility.Duplicate(original);

            Assert.Equal(original.Item1, clone.Item1);
            Assert.Equal(original.Item2, clone.Item2);
            Assert.Equal(original.Item3, clone.Item3);
            Assert.Equal(original.Item4, clone.Item4);
        }

        [Fact]
        public void Item6Test()
        {
            var original = ValueTuple.Create(1, "Hello World", 2.5f, long.MaxValue, ushort.MinValue, new int[] { 1, 2, 3 });
            var clone = Utility.Duplicate(original);

            Assert.Equal(original.Item1, clone.Item1);
            Assert.Equal(original.Item2, clone.Item2);
            Assert.Equal(original.Item3, clone.Item3);
            Assert.Equal(original.Item4, clone.Item4);
            Assert.Equal(original.Item5, clone.Item5);
            Assert.Equal(original.Item6, clone.Item6);
        }

        [Fact]
        public void Item8Test()
        {
            var original = ValueTuple.Create(1, "Hello World", 2.5f, long.MaxValue, ushort.MinValue, new int[] { 1, 2, 3 }, "Bye World", 12);
            var clone = Utility.Duplicate(original);

            Assert.Equal(original.Item1, clone.Item1);
            Assert.Equal(original.Item2, clone.Item2);
            Assert.Equal(original.Item3, clone.Item3);
            Assert.Equal(original.Item4, clone.Item4);
            Assert.Equal(original.Item5, clone.Item5);
            Assert.Equal(original.Item6, clone.Item6);
            Assert.Equal(original.Item7, clone.Item7);
            Assert.Equal(original.Item8, clone.Item8);
        }
    }

    public class IpAddressTests
    {
        [Fact]
        public void V4Test()
        {
            var source = IPAddress.Loopback;
            var clone = Utility.Duplicate(source);

            Assert.True(source.Equals(clone));
        }

        [Fact]
        public void V6Test()
        {
            var source = IPAddress.IPv6Loopback;
            var clone = Utility.Duplicate(source);

            Assert.True(source.Equals(clone));
        }
    }

    public class NullableTests
    {
        [Fact]
        void ValueTest()
        {
            var original = new Nullable<int>(42);
            var clone = Utility.Duplicate(original);

            Assert.Equal(original, clone);
        }

        [Fact]
        void NullTest()
        {
            var original = new Nullable<int>();
            var clone = Utility.Duplicate(original);

            Assert.Equal(original, clone);
        }
    }

    public class EnumTests
    {
        [Fact]
        public void ByteEnumTest() => TestEnum(ByteEnumType.Two, 1);
        public enum ByteEnumType : byte
        {
            One, Two, Three, Four
        }

        [Fact]
        public void ShortEnumTest() => TestEnum(ShortEnumType.Two, 2);
        public enum ShortEnumType : short
        {
            One, Two, Three, Four
        }

        [Fact]
        public void IntEnumTest() => TestEnum(IntEnumType.Two, 4);
        public enum IntEnumType : int
        {
            One, Two, Three, Four
        }

        [Fact]
        public void LongEnumTest() => TestEnum(LongEnumType.Two, 8);
        public enum LongEnumType : long
        {
            One, Two, Three, Four
        }

        void TestEnum<[NetworkSerializationMarker] T>(T original, int size)
        {
            var writer = new NetDataWriter(true, 512);

            NetworkSerializer.WriteValue(in original, writer);

            var reader = new NetDataReader(writer);

            var clone = NetworkSerializer.ReadValue<T>(reader);

            Assert.True(reader.Position == size);
            Assert.True(writer.Length == size);

            Assert.Equal(original, clone);
        }
    }

    public class ArrayTests
    {
        [Fact]
        public void GeneralTest()
        {
            var array = new string[]
            {
                "Hello",
                "World",
                "Bye",
                "World",
            };

            var source = array;
            var clone = Utility.Duplicate(source);

            Assert.Equal(source, clone);
        }

        [Fact]
        public void EmptyTest()
        {
            var array = new string[] { };

            var source = array;
            var clone = Utility.Duplicate(source);

            Assert.Equal(source, clone);
        }

        [Fact]
        public void InPlaceTest()
        {
            var source = new string[]
            {
                "Hello",
                "World",
                "Later",
            };
            var destination = new string[source.Length];
            var marker = destination;

            Utility.WriteInto(ref source, ref destination);

            Assert.Equal(source, destination);
            Assert.Same(destination, marker);
        }
    }

    public class ArraySegmentTests
    {
        [Fact]
        public void GeneralTest()
        {
            var array = new string[]
            {
                "Hello",
                "World",
                "Bye",
                "World",
            };

            var source = new ArraySegment<string>(array, 1, 2);
            var clone = Utility.Duplicate(source);

            Assert.Equal(source, clone);
        }

        [Fact]
        public void EmptyTest()
        {
            var array = new string[] { };

            var source = new ArraySegment<string>(array);
            var clone = Utility.Duplicate(source);

            Assert.Equal(source, clone);
        }

        [Fact]
        public void InPlaceTest()
        {
            var source = new ArraySegment<string>(["Hello", "World", "Later"]);
            var destination = new ArraySegment<string>(new string[42]);
            var marker = destination;

            Utility.WriteInto(ref source, ref destination);

            Assert.Equal(source, destination);
            Assert.Same(destination.Array, marker.Array);
        }
    }

    public class ListTests
    {
        [Fact]
        public void GeneralTest()
        {
            var list = new List<string>
            {
                "Hello",
                "World",
                "Bye",
                "World",
            };

            var source = list;
            var clone = Utility.Duplicate(source);

            Assert.Equal(source, clone);
        }

        [Fact]
        public void InPlaceTest()
        {
            var source = new List<string>
            {
                "Hello",
                "World",
                "Later",
            };
            var destination = new List<string>() { "1", "2", "3", "4", "5" };
            var marker = destination;

            Utility.WriteInto(ref source, ref destination);

            Assert.Equal(source, destination);
            Assert.Same(destination, marker);
        }
    }

    public class DictionaryTests
    {
        [Fact]
        public void GeneralTest()
        {
            var dictionary = new Dictionary<string, string>
            {
                { "Hello", "World" },
                { "Bye", "World" },
                { "Rustle", "Mania" },
                { "James", "Bond" },
            };

            var source = dictionary;
            var clone = Utility.Duplicate(source);

            Assert.Equal(source, clone);
        }

        [Fact]
        public void InPlaceTest()
        {
            var source = new Dictionary<string, string>
            {
                { "Hello", "World" },
                { "Bye", "World" },
                { "Rustle", "Mania" },
                { "James", "Bond" },
            };

            var destination = new Dictionary<string, string>()
            {
                { "1", "Bla" },
                { "2", "Bla" },
                { "3", "Bla" },
                { "4", "Bla" },
                { "5", "Bla" },
                { "6", "Bla" },
            };

            var marker = destination;

            Utility.WriteInto(ref source, ref destination);

            Assert.Equal(source, destination);
            Assert.Same(destination, marker);
        }
    }

    public class BlittableTest
    {
        [Fact]
        public void GeneralTest()
        {
            var source = new Data()
            {
                x = 1,
                y = 2,
                z = 3,
                w = 4,
            };

            var clone = Utility.Duplicate(source);

            Assert.Equal(source, clone);
        }

        [NetworkBlittable]
        public struct Data
        {
            public int x, y, z, w;
        }
    }

    public class JsonIPAddress
    {
        [Fact]
        public void Serialize()
        {
            var original = new IPAddress(stackalloc byte[] { 10, 0, 0, 10 });
            var json = JsonSerializer.Serialize(original, SharedAPI.JsonOptions);
            var clone = JsonSerializer.Deserialize<IPAddress>(json, options: SharedAPI.JsonOptions);

            Assert.Equal(original, clone);
        }

        [Fact]
        public void Warpper()
        {
            var original = new Wrapper()
            {
                Address = new IPAddress(stackalloc byte[] { 10, 0, 0, 10 })
            };

            var json = JsonSerializer.Serialize(original, options: SharedAPI.JsonOptions);
            var clone = JsonSerializer.Deserialize<Wrapper>(json, options: SharedAPI.JsonOptions);

            Assert.Equal(original.Address, clone.Address);
        }

        public struct Wrapper
        {
            public IPAddress Address;
        }
    }

    public static class Utility
    {
        public static T Duplicate<[NetworkSerializationMarker] T>(T original)
        {
            var writer = new NetDataWriter(true, 512);

            NetworkSerializer.WriteValue(in original, writer);

            var reader = new NetDataReader(writer);

            var clone = NetworkSerializer.ReadValue<T>(reader);

            Assert.Equal(reader.Position, writer.Length);

            return clone;
        }

        public static void WriteInto<[NetworkSerializationMarker] T>(ref T source, ref T destination)
        {
            var writer = new NetDataWriter(true, 512);

            NetworkSerializer.WriteValue(in source, writer);

            var reader = new NetDataReader(writer);

            NetworkSerializer.ReadValue<T>(ref destination, reader);

            Assert.Equal(reader.Position, writer.Length);
        }
    }
}

namespace JsonSerializationTests
{
    public class FixedStringTests
    {
        [Fact]
        void Fixed20Test()
        {
            var source = new FixedString<FS20>("Hello World");
            var clone = Utility.Duplicate(source);

            Assert.Equal(source.ToString(), clone.ToString());
        }
        [Fact]
        void Fixed40Test()
        {
            var source = new FixedString<FS40>("Hello World");
            var clone = Utility.Duplicate(source);

            Assert.Equal(source.ToString(), clone.ToString());
        }
        [Fact]
        void Fixed60Test()
        {
            var source = new FixedString<FS60>("Hello World");
            var clone = Utility.Duplicate(source);

            Assert.Equal(source.ToString(), clone.ToString());
        }
        [Fact]
        void Fixed80Test()
        {
            var source = new FixedString<FS80>("Hello World");
            var clone = Utility.Duplicate(source);

            Assert.Equal(source.ToString(), clone.ToString());
        }

        [Fact]
        void MinTestTest()
        {
            var source = new FixedString<FS20>("");
            var clone = Utility.Duplicate(source);

            Assert.Equal(source.ToString(), clone.ToString());
        }

        [Fact]
        void MaxTest()
        {
            var source = new FixedString<FS80>(new string('A', 80));
            var clone = Utility.Duplicate(source);

            Assert.Equal(source.ToString(), clone.ToString());
        }
    }

    public class IPAddressTests
    {
        [Fact]
        void MinTest()
        {
            var source = new IPAddress([0, 0, 0, 0]);
            var clone = Utility.Duplicate(source);

            Assert.Equal(source, clone);
        }

        [Fact]
        void MaxTest()
        {
            var source = new IPAddress([255, 255, 255, 255]);
            var clone = Utility.Duplicate(source);

            Assert.Equal(source, clone);
        }

        [Fact]
        void OverflowTest()
        {
            var json = '"' + new string('0', 100) + '"';
            var clone = JsonSerializer.Deserialize<IPAddress>(json, options: SharedAPI.JsonOptions);
        }
    }

    public static class Utility
    {
        public static T Duplicate<[NetworkSerializationMarker] T>(T original)
        {
            var json = JsonSerializer.Serialize(original, options: SharedAPI.JsonOptions);

            var clone = JsonSerializer.Deserialize<T>(json, options: SharedAPI.JsonOptions);

            return clone;
        }
    }
}
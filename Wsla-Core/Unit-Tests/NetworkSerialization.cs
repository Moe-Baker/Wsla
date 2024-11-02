using LiteNetLib.Utils;

using System.Text;

using Wsla.Serialization;

using static SerializationTests.Utility;

namespace SerializationTests
{
    public class StringTests
    {
        [Fact]
        public void NullStringTest()
        {
            string? source = null;
            var clone = Duplicate(source);

            Assert.Equal(source, clone);
        }

        [Fact]
        public void ShortStringTest()
        {
            var source = "Hello World";
            var clone = Duplicate(source);

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
            var clone = Duplicate(source);

            Assert.Equal(source, clone);
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
            var clone = Duplicate(source);

            Assert.Equal(source, clone);
        }

        [Fact]
        public void EmptyTest()
        {
            var array = new string[] { };

            var source = array;
            var clone = Duplicate(source);

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

            WriteInto(ref source, ref destination);

            Assert.Equal(source, destination);
            Assert.True(ReferenceEquals(destination, marker));
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
            var clone = Duplicate(source);

            Assert.Equal(source, clone);
        }

        [Fact]
        public void EmptyTest()
        {
            var array = new string[] { };

            var source = new ArraySegment<string>(array);
            var clone = Duplicate(source);

            Assert.Equal(source, clone);
        }

        [Fact]
        public void InPlaceTest()
        {
            var source = new ArraySegment<string>(["Hello", "World", "Later"]);
            var destination = new ArraySegment<string>(new string[42]);
            var marker = destination;

            WriteInto(ref source, ref destination);

            Assert.Equal(source, destination);
            Assert.True(ReferenceEquals(destination.Array, marker.Array)); ;
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
            var clone = Duplicate(source);

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
            var destination = new List<string>();
            var marker = destination;

            WriteInto(ref source, ref destination);

            Assert.Equal(source, destination);
            Assert.True(ReferenceEquals(destination, marker));
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
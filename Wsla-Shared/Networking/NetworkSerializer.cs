using System;

using LiteNetLib;
using LiteNetLib.Utils;

using MemoryPack;

namespace Wsla.Shared.Global
{
    public static class NetworkSerializer
    {
        #region Write
        public static void WriteType<T>(in NetDataWriter writer) => WriteType(in writer, typeof(T));

        public static void WriteType(in NetDataWriter writer, Type type)
        {
            if (NetworkTypes.TryGet(type, out var id) is false)
                throw new ArgumentException($"Type ({type}) not Registered as NetworkType");

            MemoryPackSerializer.Serialize(writer, in id);
        }
        public static void WriteValue<T>(in NetDataWriter writer, in T value)
        {
            MemoryPackSerializer.Serialize(in writer, in value);
        }

        /// <summary>
        /// Write both type and value
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="writer"></param>
        /// <param name="value"></param>
        public static void WriteHeader<T>(in NetDataWriter writer, in T value)
        {
            WriteType<T>(in writer);
            WriteValue(in writer, in value);
        }
        #endregion

        #region Read
        public static Type ReadType(in NetPacketReader reader)
        {
            var id = ReadValue<byte>(in reader);

            if (NetworkTypes.TryGet(id, out var type) is false)
                throw new ArgumentException($"No NetworkType with ID {id} Registered");

            return type;
        }

        public static T ReadValue<T>(in NetPacketReader reader)
        {
            var value = default(T);

            ReadValue(in reader, ref value);

            return value;
        }
        public static void ReadValue<T>(in NetPacketReader reader, ref T value)
        {
            var span = reader.GetRemainingBytesSpan();

            reader.Position += MemoryPackSerializer.Deserialize(span, ref value);
        }
        #endregion
    }
}
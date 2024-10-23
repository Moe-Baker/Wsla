using System;
using System.Collections;
using System.Runtime.CompilerServices;

namespace Wsla.Serialization
{
    public static class NetworkSerializer
    {
        public static void Write<[NetworkSerializationMarker] TValue, TStream>(in TValue value, ref TStream stream)
            where TStream : INetworkStream
        {
            var resolver = NetworkSerializationResolver.Retrieve<TValue>();

            resolver.Write(in value, ref stream);
        }

        public static void Read<[NetworkSerializationMarker] TValue, TStream>(ref TValue value, ref TStream stream)
            where TStream : INetworkStream
        {
            var resolver = NetworkSerializationResolver.Retrieve<TValue>();

            resolver.Read(ref value, ref stream);
        }
        public static void Read<[NetworkSerializationMarker] TValue, TStream>(ref TStream stream, out TValue value)
            where TValue : new()
            where TStream : INetworkStream
        {
            value = new TValue();

            var resolver = NetworkSerializationResolver.Retrieve<TValue>();

            resolver.Read(ref value, ref stream);
        }
        public static TValue Read<[NetworkSerializationMarker] TValue, TStream>(ref TStream stream)
            where TValue : new()
            where TStream : INetworkStream
        {
            var instance = new TValue();

            var resolver = NetworkSerializationResolver.Retrieve<TValue>();

            resolver.Read(ref instance, ref stream);

            return instance;
        }

        public static TValue Clone<[NetworkSerializationMarker] TValue>(in TValue original)
            where TValue : new()
        {
            var stream = new NetworkStream(1024);

            Write(in original, ref stream);

            stream.Reset();

            return Read<TValue, NetworkStream>(ref stream);
        }

        public static class Helper
        {
            public unsafe static class Blittable
            {
                public static void Write<TValue, TStream>(in TValue value, ref TStream stream)
                    where TValue : unmanaged
                    where TStream : INetworkStream
                {
                    var span = stream.Take(sizeof(TValue));

                    fixed (byte* destination = span)
                    {
#if UNITY_ANDROID
                var source = &instance;
                Buffer.MemoryCopy(source, destination, writer.Remaining, sizeof(T));
#else
                        ref var reference = ref Unsafe.AsRef<TValue>(destination);
                        reference = value;
#endif
                    }
                }
                public static void Read<TValue, TStream>(ref TValue value, ref TStream stream)
                    where TValue : unmanaged
                    where TStream : INetworkStream
                {
                    var span = stream.Take(sizeof(TValue));

                    fixed (byte* source = span)
                    {
#if UNITY_ANDROID
                var destination = &value;
                Buffer.MemoryCopy(source, destination, reader.Remaining, sizeof(T));
#else
                        value = Unsafe.AsRef<TValue>(source);
#endif
                    }
                }
            }

            public static class Nullability
            {
                static class Values
                {
                    public const byte IsNull = 0;
                    public const byte NotNull = 1;
                }

                public static class Length
                {
                    /// <summary>
                    /// Writes both the nullability and length of the collection
                    /// </summary>
                    /// <typeparam name="TValue"></typeparam>
                    /// <typeparam name="TStream"></typeparam>
                    /// <param name="collection"></param>
                    /// <param name="stream"></param>
                    /// <returns>true if null, false if not</returns>
                    public static bool Write<TValue, TStream>(in TValue collection, ref TStream stream)
                        where TValue : ICollection
                        where TStream : INetworkStream
                    {
                        ushort length;

                        if (collection is null)
                        {
                            length = 0;
                            NetworkSerializer.Write(in length, ref stream);
                            return true;
                        }
                        else
                        {
                            length = (ushort)(collection.Count + 1);
                            NetworkSerializer.Write(in length, ref stream);
                            return false;
                        }
                    }

                    /// <summary>
                    /// Reads both the nullability and length of the collection
                    /// </summary>
                    /// <typeparam name="TStream"></typeparam>
                    /// <param name="stream"></param>
                    /// <param name="length"></param>
                    /// <returns>true if null, false if not</returns>
                    public static bool Read<TStream>(ref TStream stream, out int length)
                        where TStream : INetworkStream
                    {
                        length = NetworkSerializer.Read<ushort, TStream>(ref stream);

                        if (length == 0)
                            return true;

                        length -= 1;
                        return false;
                    }
                }

                public static bool IsNullable<T>()
                {
                    var type = typeof(T);

                    if (type.IsClass)
                        return true;

                    return false;
                }

                /// <summary>
                /// Writes the passed object nullability
                /// </summary>
                /// <typeparam name="TValue"></typeparam>
                /// <typeparam name="TStream"></typeparam>
                /// <param name="value"></param>
                /// <param name="stream"></param>
                /// <returns>true if null, false if not</returns>
                public static bool Write<TValue, TStream>(in TValue value, ref TStream stream)
                    where TStream : INetworkStream
                {
                    bool IsNull = value is null;

                    Write(IsNull, ref stream);

                    return IsNull;
                }

                /// <summary>
                /// Writes nullability byte, pass true for null, false if not
                /// </summary>
                /// <typeparam name="TValue"></typeparam>
                /// <typeparam name="TStream"></typeparam>
                /// <param name="value"></param>
                /// <param name="stream"></param>
                public static void Write<TStream>(bool value, ref TStream stream)
                    where TStream : INetworkStream
                {
                    var span = stream.Take(1);

                    if (value)
                        span[0] = Values.IsNull;
                    else
                        span[0] = Values.NotNull;
                }

                /// <summary>
                /// Reads nullability
                /// </summary>
                /// <typeparam name="TStream"></typeparam>
                /// <param name="stream"></param>
                /// <returns>true for null, false for not</returns>
                public static bool Read<TStream>(ref TStream stream)
                    where TStream : INetworkStream
                {
                    var span = stream.Take(1);

                    if (span[0] == Values.IsNull)
                        return true;
                    else
                        return false;
                }
            }

            public static class Length
            {
                public static void Write<TValue, TStream>(in TValue collection, ref TStream stream)
                    where TValue : ICollection
                    where TStream : INetworkStream
                {
                    NetworkSerializer.Write((ushort)(collection.Count), ref stream);
                }

                public static void Write<TStream>(int value, ref TStream stream)
                    where TStream : INetworkStream
                {
                    NetworkSerializer.Write((ushort)(value), ref stream);
                }

                public static int Read<TStream>(ref TStream stream)
                    where TStream : INetworkStream
                {
                    return NetworkSerializer.Read<ushort, TStream>(ref stream);
                }
            }
        }
    }

    [AttributeUsage(AttributeTargets.GenericParameter, Inherited = true, AllowMultiple = false)]
    public sealed class NetworkSerializationMarkerAttribute : Attribute { }
}
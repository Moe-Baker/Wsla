using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wsla.Serialization
{
    public static class NetworkSerializer
    {
        public static void WriteValue<[NetworkSerializationMarker] TValue>(in TValue value, INetworkStream stream)
        {
            var resolver = NetworkSerializationResolver.Retrieve<TValue>();

            resolver.Write(in value, stream);
        }

        public static void WriteHeader<[NetworkSerializationMarker] TValue>(in TValue value, INetworkStream stream)
        {
            var type = typeof(TValue);

            WriteValue(in type, stream);
            WriteValue(in value, stream);
        }

        public static void ReadValue<[NetworkSerializationMarker] TValue>(ref TValue value, INetworkStream stream)
        {
            var resolver = NetworkSerializationResolver.Retrieve<TValue>();

            resolver.Read(ref value, stream);
        }
        public static void ReadValue<[NetworkSerializationMarker] TValue>(INetworkStream stream, out TValue value)
        {
            value = default;

            var resolver = NetworkSerializationResolver.Retrieve<TValue>();

            resolver.Read(ref value, stream);
        }
        public static TValue ReadValue<[NetworkSerializationMarker] TValue>(INetworkStream stream)
        {
            var instance = default(TValue);

            var resolver = NetworkSerializationResolver.Retrieve<TValue>();

            resolver.Read(ref instance, stream);

            return instance;
        }

        public static class Helper
        {
            public unsafe static class Blittable
            {
                public static bool UseMemCopy
                {
                    get
                    {
#if WSLA_UNITY && UNITY_ANDROID || true
                        return Environment.Is64BitOperatingSystem;
#else
                        return false;
#endif
                    }
                }

                public static void Write<TValue>(in TValue value, INetworkStream stream)
                    where TValue : unmanaged
                {
                    var span = stream.PopSpan(sizeof(TValue));

                    fixed (byte* destination = span)
                    {
                        if (UseMemCopy)
                        {
                            fixed (void* source = &value)
                            {
                                Buffer.MemoryCopy(source, destination, span.Length, span.Length);
                            }
                        }
                        else
                        {
                            ref var reference = ref Unsafe.AsRef<TValue>(destination);
                            reference = value;
                        }
                    }
                }
                public static void Read<TValue>(ref TValue value, INetworkStream stream)
                    where TValue : unmanaged
                {
                    var span = stream.PopSpan(sizeof(TValue));

                    fixed (byte* source = span)
                    {
                        if (UseMemCopy)
                        {
                            fixed (void* destination = &value)
                            {
                                Buffer.MemoryCopy(source, destination, span.Length, span.Length);
                            }
                        }
                        else
                        {
                            value = Unsafe.AsRef<TValue>(source);
                        }
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
                    /// <param name="collection"></param>
                    /// <param name="stream"></param>
                    /// <returns>true if null, false if not</returns>
                    public static bool Write<TValue>(in TValue collection, INetworkStream stream)
                        where TValue : ICollection
                    {
                        ushort length;

                        if (collection is null)
                        {
                            length = 0;
                            NetworkSerializer.WriteValue(in length, stream);
                            return true;
                        }
                        else
                        {
                            length = (ushort)(collection.Count + 1);
                            NetworkSerializer.WriteValue(in length, stream);
                            return false;
                        }
                    }

                    /// <summary>
                    /// Writes both the nullability and length of the collection
                    /// </summary>
                    /// <typeparam name="TValue"></typeparam>
                    /// <param name="collection"></param>
                    /// <param name="stream"></param>
                    /// <returns>true if null, false if not</returns>
                    public static bool Write<TValue>(in TValue collection, int count, INetworkStream stream)
                    {
                        ushort length;

                        if (collection is null)
                        {
                            length = 0;
                            NetworkSerializer.WriteValue(in length, stream);
                            return true;
                        }
                        else
                        {
                            length = (ushort)(count + 1);
                            NetworkSerializer.WriteValue(in length, stream);
                            return false;
                        }
                    }

                    /// <summary>
                    /// Reads both the nullability and length of the collection
                    /// </summary>
                    /// <param name="stream"></param>
                    /// <param name="length"></param>
                    /// <returns>true if null, false if not</returns>
                    public static bool Read(INetworkStream stream, out int length)
                    {
                        length = NetworkSerializer.ReadValue<ushort>(stream);

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
                /// <param name="value"></param>
                /// <param name="stream"></param>
                /// <returns>true if null, false if not</returns>
                public static bool Write<TValue>(in TValue value, INetworkStream stream)
                {
                    bool IsNull = value is null;

                    Write(IsNull, stream);

                    return IsNull;
                }

                /// <summary>
                /// Writes nullability byte, pass true for null, false for not
                /// </summary>
                /// <typeparam name="TValue"></typeparam>
                /// <param name="value"></param>
                /// <param name="stream"></param>
                public static void Write(bool value, INetworkStream stream)
                {
                    var span = stream.PopSpan(1);

                    if (value)
                        span[0] = Values.IsNull;
                    else
                        span[0] = Values.NotNull;
                }

                /// <summary>
                /// Reads nullability
                /// </summary>
                /// <param name="stream"></param>
                /// <returns>true for null, false if not</returns>
                public static bool Read(INetworkStream stream)
                {
                    var span = stream.PopSpan(1);

                    if (span[0] == Values.IsNull)
                        return true;
                    else
                        return false;
                }
            }

            public static class Length
            {
                public static void Write<TValue>(in TValue collection, INetworkStream stream)
                    where TValue : ICollection
                {
                    NetworkSerializer.WriteValue((ushort)(collection.Count), stream);
                }

                public static void Write(int value, INetworkStream stream)
                {
                    NetworkSerializer.WriteValue((ushort)(value), stream);
                }

                public static int Read(INetworkStream stream)
                {
                    return NetworkSerializer.ReadValue<ushort>(stream);
                }
            }
        }
    }

    [AttributeUsage(AttributeTargets.GenericParameter, Inherited = true, AllowMultiple = false)]
    public sealed class NetworkSerializationMarkerAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = true)]
    public sealed class NetworkSerializationResolverRegisterationAttribute : Attribute
    {
        public Type Type { get; }
        public int Order { get; }
        public string Entrypoint { get; }

        public void Invoke()
        {
            const BindingFlags Binding = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            var method = Type.GetMethod(Entrypoint, Binding);

            method.Invoke(null, null);
        }

        public NetworkSerializationResolverRegisterationAttribute(Type Type, int Order, string Entrypoint)
        {
            this.Type = Type;
            this.Order = Order;
            this.Entrypoint = Entrypoint;
        }
    }
}
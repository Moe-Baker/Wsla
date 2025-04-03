using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;

namespace Wsla.Serialization
{
    public static class NetworkSerializationResolver
    {
        public static void Register<TValue, TResolver>()
            where TResolver : NetworkSerializationResolver<TValue>, new()
        {
            //Ignore Duplicates
            if (Collection<TValue>.Instance is not null)
                return;

            var resolver = new TResolver();
            Register(resolver);
        }
        public static void Register<TValue>(NetworkSerializationResolver<TValue> resolver)
        {
            //Ignore Duplicates
            if (Collection<TValue>.Instance is not null)
                return;

            Collection<TValue>.Instance = resolver;
        }

        public static NetworkSerializationResolver<TValue> Retrieve<TValue>()
        {
            ref var Instance = ref Collection<TValue>.Instance;

#if DEBUG
            if (Instance is null)
                throw new NullReferenceException($"No Serialization Resolver Defined for Type ({typeof(TValue)})");
#endif

            return Instance;
        }

        public static class Implicit
        {
            static Dictionary<Type, IEntry> Dictionary;
            public interface IEntry
            {
                void Write(object value, INetworkStream stream);

                object Read(INetworkStream stream);
                void Read(ref object value, INetworkStream stream);
            }
            class Entry<T> : IEntry
            {
                NetworkSerializationResolver<T> Resolver;

                public void Write(object value, INetworkStream stream)
                {
                    var cast = (T)value;
                    Resolver.Write(in cast, stream);
                }

                public object Read(INetworkStream stream)
                {
                    var cast = default(T);
                    Resolver.Read(ref cast, stream);
                    return cast;
                }
                public void Read(ref object value, INetworkStream stream)
                {
                    var cast = (T)value;
                    Resolver.Read(ref cast, stream);
                    value = cast;
                }

                public Entry(NetworkSerializationResolver<T> Resolver)
                {
                    this.Resolver = Resolver;
                }
            }

            public static IEntry Get(Type type)
            {
                if (Dictionary.TryGetValue(type, out var entry) is false)
                    throw new ArgumentException($"No Implicit Serialization Resolver Registered for Type ({type})");

                return entry;
            }

            public static void Register<T>(NetworkSerializationResolver<T> resolver)
            {
                var type = typeof(T);
                var entry = new Entry<T>(resolver);

                Dictionary[type] = entry;
            }

            static Implicit()
            {
                Dictionary = new();
            }
        }

        internal static class Collection<TValue>
        {
            internal static NetworkSerializationResolver<TValue> Instance;
        }

        public static class Registration
        {
            public static void LoadAll()
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    Load(assembly);
            }

            public static void Load(Assembly assembly)
            {
                var attributes = assembly.GetCustomAttributes<NetworkSerializationResolverRegistrationAttribute>();

                foreach (var attribute in attributes)
                    attribute.Invoke();
            }
        }

        static NetworkSerializationResolver()
        {
            Register<bool, BoolNetworkSerializationResolver>();

            Register<byte, BlittableNetworkSerializationResolver<byte>>();
            Register<sbyte, BlittableNetworkSerializationResolver<sbyte>>();

            Register<short, BlittableNetworkSerializationResolver<short>>();
            Register<ushort, BlittableNetworkSerializationResolver<ushort>>();

            Register<int, BlittableNetworkSerializationResolver<int>>();
            Register<uint, BlittableNetworkSerializationResolver<uint>>();

            Register<long, BlittableNetworkSerializationResolver<long>>();
            Register<ulong, BlittableNetworkSerializationResolver<ulong>>();

            Register<float, BlittableNetworkSerializationResolver<float>>();
            Register<double, BlittableNetworkSerializationResolver<double>>();

            Register<string, StringNetworkSerializationResolver>();

            Register<Guid, BlittableNetworkSerializationResolver<Guid>>();
            Register<DateTime, DateTimeSerializationResolver>();
            Register<TimeSpan, TimeSpanSerializationResolver>();

            Register<IPAddress, IPAddressNetworkSerializationResolver>();

            Register<Type, NetworkTypeSerializationResolver>();

            Register<NetworkVariableID, BlittableNetworkSerializationResolver<NetworkVariableID>>();
            Register<NetworkRpcID, BlittableNetworkSerializationResolver<NetworkRpcID>>();

            Register(new FixedStringNetworkSerializationResolver<FixedString<FS20>>());
            Register(new FixedStringNetworkSerializationResolver<FixedString<FS40>>());
            Register(new FixedStringNetworkSerializationResolver<FixedString<FS60>>());
            Register(new FixedStringNetworkSerializationResolver<FixedString<FS80>>());

            Registration.LoadAll();
        }
    }
    public abstract class NetworkSerializationResolver<TValue>
    {
        public abstract void Write(in TValue value, INetworkStream stream);
        public abstract void Read(ref TValue value, INetworkStream stream);
    }

    public class BoolNetworkSerializationResolver : NetworkSerializationResolver<bool>
    {
        public override void Write(in bool value, INetworkStream stream)
        {
            ref var octet = ref stream.PopByte();

            if (value)
                octet = 1;
            else
                octet = 0;
        }
        public override void Read(ref bool value, INetworkStream stream)
        {
            var octet = stream.PopByte();

            if (octet is 1)
                value = true;
            else
                value = false;
        }
    }

    public class DateTimeSerializationResolver : NetworkSerializationResolver<DateTime>
    {
        public override void Write(in DateTime value, INetworkStream stream)
        {
            long binary = value.ToBinary();
            NetworkSerializer.WriteValue(in binary, stream);
        }
        public override void Read(ref DateTime value, INetworkStream stream)
        {
            var binary = NetworkSerializer.ReadValue<long>(stream);
            value = DateTime.FromBinary(binary);
        }
    }
    public class TimeSpanSerializationResolver : NetworkSerializationResolver<TimeSpan>
    {
        public override void Write(in TimeSpan value, INetworkStream stream)
        {
            NetworkSerializer.WriteValue(value.Ticks, stream);
        }
        public override void Read(ref TimeSpan value, INetworkStream stream)
        {
            var ticks = NetworkSerializer.ReadValue<long>(stream);
            value = new TimeSpan(ticks);
        }
    }

    //Derived Resolvers

    public class StringNetworkSerializationResolver : NetworkSerializationResolver<string>
    {
        static Encoding Encoder => Encoding.UTF8;

        public const int StackOptimizationLimit = 1024;

        public override void Write(in string value, INetworkStream stream)
        {
            if (value is null)
            {
                NetworkSerializer.Helper.Length.Write(0, stream);
                return;
            }

            if (value.Length > StackOptimizationLimit)
            {
                var length = Encoder.GetByteCount(value);
                NetworkSerializer.Helper.Length.Write(length + 1, stream);

                var buffer = stream.PopSpan(length);
                Encoder.GetBytes(value, buffer);
            }
            else
            {
                //Small strings optimization

                Span<byte> buffer = stackalloc byte[Encoder.GetMaxByteCount(value.Length)];

                var length = Encoder.GetBytes(value, buffer);
                NetworkSerializer.Helper.Length.Write(length + 1, stream);

                var destination = stream.PopSpan(length);

                buffer.Slice(0, length).CopyTo(destination);
            }

            var count = Encoder.GetByteCount(value);
        }

        public override void Read(ref string value, INetworkStream stream)
        {
            var length = NetworkSerializer.Helper.Length.Read(stream);

            if (length is 0)
            {
                value = null;
                return;
            }

            length -= 1;

            var span = stream.PopSpan(length);

            value = Encoder.GetString(span);
        }
    }

    public unsafe class EnumNetworkSerializationResolver<TEnum> : NetworkSerializationResolver<TEnum>
        where TEnum : unmanaged, Enum
    {
        public override void Write(in TEnum value, INetworkStream stream)
        {
            NetworkSerializer.Helper.Blittable.Write(in value, stream);
        }
        public override void Read(ref TEnum value, INetworkStream stream)
        {
            NetworkSerializer.Helper.Blittable.Read(ref value, stream);
        }
    }

    public class IPAddressNetworkSerializationResolver : NetworkSerializationResolver<IPAddress>
    {
        const int V4Size = 4;
        const int V4ID = 1;

        const int V6Size = 16;
        const int V6ID = 2;

        public override void Write(in IPAddress value, INetworkStream stream)
        {
            switch (value.AddressFamily)
            {
                case AddressFamily.InterNetwork:
                {
                    stream.PopByte() = V4ID;

                    var span = stream.PopSpan(V4Size);

                    if (value.TryWriteBytes(span, out var written) is false || written != span.Length)
                        throw new NotImplementedException();
                }
                break;

                case AddressFamily.InterNetworkV6:
                {
                    stream.PopByte() = V6ID;

                    var span = stream.PopSpan(V6Size);

                    if (value.TryWriteBytes(span, out var written) is false || written != span.Length)
                        throw new NotImplementedException();
                }
                break;

                default:
                    throw new NotImplementedException("Can Only Serialize IP v4/v6 Addresses");
            }
        }
        public override void Read(ref IPAddress value, INetworkStream stream)
        {
            var type = stream.PopByte();

            switch (type)
            {
                case V4ID: //IPv4
                {
                    var span = stream.PopSpan(V4Size);
                    value = new IPAddress(span);
                }
                break;

                case V6ID: //IPv6
                {
                    var span = stream.PopSpan(V6Size);
                    value = new IPAddress(span);
                }
                break;
            }
        }
    }

    #region Tuple
    public class TupleSerializationResolver : NetworkSerializationResolver<ValueTuple>
    {
        public override void Write(in ValueTuple value, INetworkStream stream) { }
        public override void Read(ref ValueTuple value, INetworkStream stream) { }
    }
    public class TupleSerializationResolver<T1> : NetworkSerializationResolver<ValueTuple<T1>>
    {
        public override void Write(in ValueTuple<T1> value, INetworkStream stream)
        {
            NetworkSerializer.WriteValue(in value.Item1, stream);
        }
        public override void Read(ref ValueTuple<T1> value, INetworkStream stream)
        {
            NetworkSerializer.ReadValue(ref value.Item1, stream);
        }
    }
    public class TupleSerializationResolver<T1, T2> : NetworkSerializationResolver<ValueTuple<T1, T2>>
    {
        public override void Write(in ValueTuple<T1, T2> value, INetworkStream stream)
        {
            NetworkSerializer.WriteValue(in value.Item1, stream);
            NetworkSerializer.WriteValue(in value.Item2, stream);
        }
        public override void Read(ref ValueTuple<T1, T2> value, INetworkStream stream)
        {
            NetworkSerializer.ReadValue(ref value.Item1, stream);
            NetworkSerializer.ReadValue(ref value.Item2, stream);
        }
    }
    public class TupleSerializationResolver<T1, T2, T3> : NetworkSerializationResolver<ValueTuple<T1, T2, T3>>
    {
        public override void Write(in ValueTuple<T1, T2, T3> value, INetworkStream stream)
        {
            NetworkSerializer.WriteValue(in value.Item1, stream);
            NetworkSerializer.WriteValue(in value.Item2, stream);
            NetworkSerializer.WriteValue(in value.Item3, stream);
        }
        public override void Read(ref ValueTuple<T1, T2, T3> value, INetworkStream stream)
        {
            NetworkSerializer.ReadValue(ref value.Item1, stream);
            NetworkSerializer.ReadValue(ref value.Item2, stream);
            NetworkSerializer.ReadValue(ref value.Item3, stream);
        }
    }
    public class TupleSerializationResolver<T1, T2, T3, T4> : NetworkSerializationResolver<ValueTuple<T1, T2, T3, T4>>
    {
        public override void Write(in ValueTuple<T1, T2, T3, T4> value, INetworkStream stream)
        {
            NetworkSerializer.WriteValue(in value.Item1, stream);
            NetworkSerializer.WriteValue(in value.Item2, stream);
            NetworkSerializer.WriteValue(in value.Item3, stream);
            NetworkSerializer.WriteValue(in value.Item4, stream);
        }
        public override void Read(ref ValueTuple<T1, T2, T3, T4> value, INetworkStream stream)
        {
            NetworkSerializer.ReadValue(ref value.Item1, stream);
            NetworkSerializer.ReadValue(ref value.Item2, stream);
            NetworkSerializer.ReadValue(ref value.Item3, stream);
            NetworkSerializer.ReadValue(ref value.Item4, stream);
        }
    }
    public class TupleSerializationResolver<T1, T2, T3, T4, T5> : NetworkSerializationResolver<ValueTuple<T1, T2, T3, T4, T5>>
    {
        public override void Write(in ValueTuple<T1, T2, T3, T4, T5> value, INetworkStream stream)
        {
            NetworkSerializer.WriteValue(in value.Item1, stream);
            NetworkSerializer.WriteValue(in value.Item2, stream);
            NetworkSerializer.WriteValue(in value.Item3, stream);
            NetworkSerializer.WriteValue(in value.Item4, stream);
            NetworkSerializer.WriteValue(in value.Item5, stream);
        }
        public override void Read(ref ValueTuple<T1, T2, T3, T4, T5> value, INetworkStream stream)
        {
            NetworkSerializer.ReadValue(ref value.Item1, stream);
            NetworkSerializer.ReadValue(ref value.Item2, stream);
            NetworkSerializer.ReadValue(ref value.Item3, stream);
            NetworkSerializer.ReadValue(ref value.Item4, stream);
            NetworkSerializer.ReadValue(ref value.Item5, stream);
        }
    }
    public class TupleSerializationResolver<T1, T2, T3, T4, T5, T6> : NetworkSerializationResolver<ValueTuple<T1, T2, T3, T4, T5, T6>>
    {
        public override void Write(in ValueTuple<T1, T2, T3, T4, T5, T6> value, INetworkStream stream)
        {
            NetworkSerializer.WriteValue(in value.Item1, stream);
            NetworkSerializer.WriteValue(in value.Item2, stream);
            NetworkSerializer.WriteValue(in value.Item3, stream);
            NetworkSerializer.WriteValue(in value.Item4, stream);
            NetworkSerializer.WriteValue(in value.Item5, stream);
            NetworkSerializer.WriteValue(in value.Item6, stream);
        }
        public override void Read(ref ValueTuple<T1, T2, T3, T4, T5, T6> value, INetworkStream stream)
        {
            NetworkSerializer.ReadValue(ref value.Item1, stream);
            NetworkSerializer.ReadValue(ref value.Item2, stream);
            NetworkSerializer.ReadValue(ref value.Item3, stream);
            NetworkSerializer.ReadValue(ref value.Item4, stream);
            NetworkSerializer.ReadValue(ref value.Item5, stream);
            NetworkSerializer.ReadValue(ref value.Item6, stream);
        }
    }
    public class TupleSerializationResolver<T1, T2, T3, T4, T5, T6, T7> : NetworkSerializationResolver<ValueTuple<T1, T2, T3, T4, T5, T6, T7>>
    {
        public override void Write(in ValueTuple<T1, T2, T3, T4, T5, T6, T7> value, INetworkStream stream)
        {
            NetworkSerializer.WriteValue(in value.Item1, stream);
            NetworkSerializer.WriteValue(in value.Item2, stream);
            NetworkSerializer.WriteValue(in value.Item3, stream);
            NetworkSerializer.WriteValue(in value.Item4, stream);
            NetworkSerializer.WriteValue(in value.Item5, stream);
            NetworkSerializer.WriteValue(in value.Item6, stream);
            NetworkSerializer.WriteValue(in value.Item7, stream);
        }
        public override void Read(ref ValueTuple<T1, T2, T3, T4, T5, T6, T7> value, INetworkStream stream)
        {
            NetworkSerializer.ReadValue(ref value.Item1, stream);
            NetworkSerializer.ReadValue(ref value.Item2, stream);
            NetworkSerializer.ReadValue(ref value.Item3, stream);
            NetworkSerializer.ReadValue(ref value.Item4, stream);
            NetworkSerializer.ReadValue(ref value.Item5, stream);
            NetworkSerializer.ReadValue(ref value.Item6, stream);
            NetworkSerializer.ReadValue(ref value.Item7, stream);
        }
    }
    public class TupleSerializationResolver<T1, T2, T3, T4, T5, T6, T7, TRest> : NetworkSerializationResolver<ValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest>>
        where TRest : struct
    {
        public override void Write(in ValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest> value, INetworkStream stream)
        {
            NetworkSerializer.WriteValue(in value.Item1, stream);
            NetworkSerializer.WriteValue(in value.Item2, stream);
            NetworkSerializer.WriteValue(in value.Item3, stream);
            NetworkSerializer.WriteValue(in value.Item4, stream);
            NetworkSerializer.WriteValue(in value.Item5, stream);
            NetworkSerializer.WriteValue(in value.Item6, stream);
            NetworkSerializer.WriteValue(in value.Item7, stream);
            NetworkSerializer.WriteValue(in value.Rest, stream);
        }
        public override void Read(ref ValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest> value, INetworkStream stream)
        {
            NetworkSerializer.ReadValue(ref value.Item1, stream);
            NetworkSerializer.ReadValue(ref value.Item2, stream);
            NetworkSerializer.ReadValue(ref value.Item3, stream);
            NetworkSerializer.ReadValue(ref value.Item4, stream);
            NetworkSerializer.ReadValue(ref value.Item5, stream);
            NetworkSerializer.ReadValue(ref value.Item6, stream);
            NetworkSerializer.ReadValue(ref value.Item7, stream);
            NetworkSerializer.ReadValue(ref value.Rest, stream);
        }
    }
    #endregion

    public class NullableNetworkSerializationResolver<T> : NetworkSerializationResolver<Nullable<T>>
        where T : struct
    {
        public override void Write(in Nullable<T> value, INetworkStream stream)
        {
            if (value.HasValue)
            {
                NetworkSerializer.Helper.Nullability.Write(false, stream);

                NetworkSerializer.WriteValue(value.Value, stream);
            }
            else
            {
                NetworkSerializer.Helper.Nullability.Write(true, stream);
            }
        }
        public override void Read(ref Nullable<T> value, INetworkStream stream)
        {
            if (NetworkSerializer.Helper.Nullability.Read(stream))
            {
                value = default;
            }
            else
            {
                var reference = value.GetValueOrDefault();
                NetworkSerializer.ReadValue(ref reference, stream);
                value = new Nullable<T>(reference);
            }
        }
    }

    public class ArrayNetworkSerializationResolver<TValue> : NetworkSerializationResolver<TValue[]>
    {
        public override void Write(in TValue[] array, INetworkStream stream)
        {
            if (NetworkSerializer.Helper.Nullability.Length.Write(in array, stream))
                return;

            for (int i = 0; i < array.Length; i++)
                NetworkSerializer.WriteValue(in array[i], stream);
        }

        public override void Read(ref TValue[] array, INetworkStream stream)
        {
            if (NetworkSerializer.Helper.Nullability.Length.Read(stream, out var length))
            {
                array = default;
                return;
            }

            EnsureLength(ref array, length);

            for (int i = 0; i < length; i++)
                NetworkSerializer.ReadValue(ref array[i], stream);
        }

        void EnsureLength(ref TValue[] array, int length)
        {
            if (array is null || array.Length != length)
            {
                if (length is 0)
                    array = Array.Empty<TValue>();
                else
                    array = new TValue[length];
            }
        }
    }

    public class ArraySegmentNetworkSerializationResolver<TValue> : NetworkSerializationResolver<ArraySegment<TValue>>
    {
        public override void Write(in ArraySegment<TValue> segment, INetworkStream stream)
        {
            NetworkSerializer.Helper.Length.Write(segment.Count, stream);

            for (int i = 0; i < segment.Count; i++)
                NetworkSerializer.WriteValue(segment[i], stream);
        }

        public override void Read(ref ArraySegment<TValue> segment, INetworkStream stream)
        {
            var length = NetworkSerializer.Helper.Length.Read(stream);

            EnsureCount(ref segment, length);

            for (int i = 0; i < length; i++)
            {
                var item = segment[i];
                NetworkSerializer.ReadValue(ref item, stream);
                segment[i] = item;
            }
        }

        void EnsureCount(ref ArraySegment<TValue> segment, int length)
        {
            if (segment.Array is null)
            {
                if (length is 0)
                    segment = ArraySegment<TValue>.Empty;
                else
                    segment = new TValue[length];
            }
            else
            {
                if (length > segment.Array.Length)
                    segment = new TValue[length];
                else
                    segment = new(segment.Array, 0, length);
            }
        }
    }

    public class ListNetworkSerializationResolver<TValue> : NetworkSerializationResolver<List<TValue>>
    {
        public override void Write(in List<TValue> list, INetworkStream stream)
        {
            if (NetworkSerializer.Helper.Nullability.Length.Write(in list, stream))
                return;

            for (int i = 0; i < list.Count; i++)
                NetworkSerializer.WriteValue(list[i], stream);
        }

        public override void Read(ref List<TValue> list, INetworkStream stream)
        {
            if (NetworkSerializer.Helper.Nullability.Length.Read(stream, out var length))
            {
                list = default;
                return;
            }

            EnsureCapacity(ref list, length);

            for (int i = 0; i < length; i++)
            {
                if (i >= list.Count)
                {
                    NetworkSerializer.ReadValue(stream, out TValue item);
                    list.Add(item);
                }
                else
                {
                    var item = list[i];
                    NetworkSerializer.ReadValue(ref item, stream);
                    list[i] = item;
                }
            }

            if (list.Count > length)
                list.RemoveRange(length, list.Count - length);
        }

        void EnsureCapacity(ref List<TValue> list, int length)
        {
            if (list is null)
                list = new List<TValue>(length);
            else if (length > list.Capacity)
                list.Capacity = length;
        }
    }

    public class DictionaryNetworkSerializationResolver<TKey, TValue> : NetworkSerializationResolver<Dictionary<TKey, TValue>>
    {
        public override void Write(in Dictionary<TKey, TValue> collection, INetworkStream stream)
        {
            if (NetworkSerializer.Helper.Nullability.Length.Write(in collection, stream))
                return;

            foreach (var (key, value) in collection)
            {
                NetworkSerializer.WriteValue(in key, stream);
                NetworkSerializer.WriteValue(in value, stream);
            }
        }

        public override void Read(ref Dictionary<TKey, TValue> collection, INetworkStream stream)
        {
            if (NetworkSerializer.Helper.Nullability.Length.Read(stream, out var length))
            {
                collection = null;
                return;
            }

            if (collection is null)
            {
                collection = new Dictionary<TKey, TValue>(length);
            }
            else
            {
                collection.Clear();
                collection.EnsureCapacity(length);
            }

            for (int i = 0; i < length; i++)
            {
                var key = NetworkSerializer.ReadValue<TKey>(stream);
                var value = NetworkSerializer.ReadValue<TValue>(stream);

                collection.Add(key, value);
            }
        }
    }

    #region Manual
    public class ManualNetworkSerializationResolver<TValue> : NetworkSerializationResolver<TValue>
        where TValue : IManualNetworkSerialization, new()
    {
        readonly bool IsNullable;

        public override void Write(in TValue value, INetworkStream stream)
        {
            if (IsNullable && NetworkSerializer.Helper.Nullability.Write(in value, stream))
                return;

            value.Write(stream);
        }
        public override void Read(ref TValue value, INetworkStream stream)
        {
            if (IsNullable && NetworkSerializer.Helper.Nullability.Read(stream))
            {
                value = default;
                return;
            }

            value ??= new();

            value.Read(stream);
        }

        public ManualNetworkSerializationResolver()
        {
            IsNullable = NetworkSerializer.Helper.Nullability.IsNullable<TValue>();
        }
    }
    public interface IManualNetworkSerialization
    {
        void Write(INetworkStream stream);
        void Read(INetworkStream stream);
    }
    #endregion

    #region Auto
    public class AutoNetworkSerializationResolver<TValue> : NetworkSerializationResolver<TValue>
        where TValue : IAutoNetworkSerialization, new()
    {
        readonly bool IsNullable;

        public override void Write(in TValue value, INetworkStream stream)
        {
            if (IsNullable && NetworkSerializer.Helper.Nullability.Write(in value, stream))
                return;

            var context = new AutoSerializationContext(stream, AutoSerializationMode.Write);

            value.Select(ref context);
        }
        public override void Read(ref TValue value, INetworkStream stream)
        {
            if (IsNullable && NetworkSerializer.Helper.Nullability.Read(stream))
            {
                value = default;
                return;
            }

            value ??= new();

            var context = new AutoSerializationContext(stream, AutoSerializationMode.Read);
            value.Select(ref context);
        }

        public AutoNetworkSerializationResolver()
        {
            IsNullable = NetworkSerializer.Helper.Nullability.IsNullable<TValue>();
        }
    }
    public interface IAutoNetworkSerialization
    {
        void Select(ref AutoSerializationContext context);
    }
    public readonly ref struct AutoSerializationContext
    {
        public INetworkStream Stream { get; }

        public AutoSerializationMode Mode { get; }

        public readonly bool IsWriting => Mode is AutoSerializationMode.Write;
        public readonly bool IsReading => Mode is AutoSerializationMode.Read;

        public readonly void Select<[NetworkSerializationMarker] TValue>(ref TValue value)
        {
            switch (Mode)
            {
                case AutoSerializationMode.Write:
                    NetworkSerializer.WriteValue(in value, Stream);
                    break;

                case AutoSerializationMode.Read:
                    NetworkSerializer.ReadValue(ref value, Stream);
                    break;

                default: throw new NotImplementedException();
            }
        }

        public AutoSerializationContext(INetworkStream Stream, AutoSerializationMode Mode)
        {
            this.Stream = Stream;
            this.Mode = Mode;
        }
    }
    public enum AutoSerializationMode
    {
        Write,
        Read,
    }
    #endregion

    #region Blittable
    public unsafe class BlittableNetworkSerializationResolver<TValue> : NetworkSerializationResolver<TValue>
        where TValue : unmanaged
    {
        public override void Write(in TValue value, INetworkStream stream)
            => NetworkSerializer.Helper.Blittable.Write(in value, stream);

        public override void Read(ref TValue value, INetworkStream stream)
            => NetworkSerializer.Helper.Blittable.Read(ref value, stream);
    }

    [AttributeUsage(AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
    public sealed class NetworkBlittableAttribute : Attribute { }
    #endregion
}
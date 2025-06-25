using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;

namespace Wsla.Serialization
{
    public class NetworkSerializationResolver
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
            Implicit.Register(resolver);
        }

        public static class Implicit
        {
            static Dictionary<Type, IEntry> Dictionary;
            public interface IEntry
            {
                void Write(object value, ref BinarySource stream);

                object Read(ref BinarySource stream);
                void Read(ref object value, ref BinarySource stream);
            }
            class Entry<T> : IEntry
            {
                NetworkSerializationResolver<T> Resolver;

                public void Write(object value, ref BinarySource stream)
                {
                    var cast = (T)value;
                    Resolver.Write(in cast, ref stream);
                }

                public object Read(ref BinarySource stream)
                {
                    var cast = default(T);
                    Resolver.Read(ref cast, ref stream);
                    return cast;
                }
                public void Read(ref object value, ref BinarySource stream)
                {
                    var cast = (T)value;
                    Resolver.Read(ref cast, ref stream);
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

        public static NetworkSerializationResolver<TValue> Retrieve<TValue>()
        {
            ref var Instance = ref Collection<TValue>.Instance;

#if DEBUG
            if (Instance is null)
                throw new NullReferenceException($"No Serialization Resolver Defined for Type ({typeof(TValue)})");
#endif

            return Instance;
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

            Register<NetworkSyncMemberID, BlittableNetworkSerializationResolver<NetworkSyncMemberID>>();

            Register<ValueTuple, TupleSerializationResolver>();

            Registration.LoadAll();
        }

        public class SourceGenerator : Attribute
        {
            public abstract class Condition : Attribute
            {
                public class ImplementsInterface : Condition
                {
                    public ImplementsInterface(Type type) { }
                }

                public class ConstructedFrom : Condition
                {
                    public ConstructedFrom(Type type) { }
                }

                public class DecoratedBy : Condition
                {
                    public DecoratedBy(Type type) { }
                }

                public class IsArray : Condition { }

                public class IsEnum : Condition { }
            }

            public abstract class Builder : Attribute
            {
                public class FromSourceType : Builder { }

                public class FromSourceArguments : Builder { }

                public class FromArrayType : Builder { }
            }

            public abstract class Options : Attribute
            {
                public class ResolveGenericArguments : Attribute { }
            }
        }
    }
    public abstract class NetworkSerializationResolver<TValue> : NetworkSerializationResolver
    {
        public abstract void Write(in TValue value, ref BinarySource stream);
        public abstract void Read(ref TValue value, ref BinarySource stream);
    }

    public class BoolNetworkSerializationResolver : NetworkSerializationResolver<bool>
    {
        public override void Write(in bool value, ref BinarySource stream)
        {
            if (value)
                stream.WriteByte(1);
            else
                stream.WriteByte(0);
        }
        public override void Read(ref bool value, ref BinarySource stream)
        {
            if (stream.ReadByte() is 1)
                value = true;
            else
                value = false;
        }
    }

    public class DateTimeSerializationResolver : NetworkSerializationResolver<DateTime>
    {
        public override void Write(in DateTime value, ref BinarySource stream)
        {
            long binary = value.ToBinary();
            NetworkSerializer.WriteValue(in binary, ref stream);
        }
        public override void Read(ref DateTime value, ref BinarySource stream)
        {
            var binary = NetworkSerializer.ReadValue<long>(ref stream);
            value = DateTime.FromBinary(binary);
        }
    }
    public class TimeSpanSerializationResolver : NetworkSerializationResolver<TimeSpan>
    {
        public override void Write(in TimeSpan value, ref BinarySource stream)
        {
            NetworkSerializer.WriteValue(value.Ticks, ref stream);
        }
        public override void Read(ref TimeSpan value, ref BinarySource stream)
        {
            var ticks = NetworkSerializer.ReadValue<long>(ref stream);
            value = new TimeSpan(ticks);
        }
    }

    public class StringNetworkSerializationResolver : NetworkSerializationResolver<string>
    {
        static Encoding Encoder => Encoding.UTF8;

        public const int StackOptimizationLimit = 1024;

        public override void Write(in string value, ref BinarySource stream)
        {
            if (value is null)
            {
                NetworkSerializer.Helper.Length.Write(0, ref stream);
                return;
            }

            if (value.Length > StackOptimizationLimit)
            {
                var length = Encoder.GetByteCount(value);
                NetworkSerializer.Helper.Length.Write(length + 1, ref stream);

                var buffer = stream.AllocateSpan(length);
                Encoder.GetBytes(value, buffer);
            }
            else
            {
                //Small strings optimization

                Span<byte> buffer = stackalloc byte[Encoder.GetMaxByteCount(value.Length)];

                var length = Encoder.GetBytes(value, buffer);
                NetworkSerializer.Helper.Length.Write(length + 1, ref stream);

                var destination = stream.AllocateSpan(length);

                buffer.Slice(0, length).CopyTo(destination);
            }

            var count = Encoder.GetByteCount(value);
        }

        public override void Read(ref string value, ref BinarySource stream)
        {
            var length = NetworkSerializer.Helper.Length.Read(ref stream);

            if (length is 0)
            {
                value = null;
                return;
            }

            length -= 1;

            var span = stream.ReadSpan(length);

            value = Encoder.GetString(span);
        }
    }

    [SourceGenerator]
    [SourceGenerator.Condition.IsEnum]
    [SourceGenerator.Builder.FromSourceType]
    public unsafe class EnumNetworkSerializationResolver<TEnum> : NetworkSerializationResolver<TEnum>
        where TEnum : unmanaged, Enum
    {
        public override void Write(in TEnum value, ref BinarySource stream)
        {
            NetworkSerializer.Helper.Blittable.Write(in value, ref stream);
        }
        public override void Read(ref TEnum value, ref BinarySource stream)
        {
            NetworkSerializer.Helper.Blittable.Read(ref value, ref stream);
        }
    }

    public class IPAddressNetworkSerializationResolver : NetworkSerializationResolver<IPAddress>
    {
        const int V4Size = 4;
        const int V4ID = 1;

        const int V6Size = 16;
        const int V6ID = 2;

        public override void Write(in IPAddress value, ref BinarySource stream)
        {
            switch (value.AddressFamily)
            {
                case AddressFamily.InterNetwork:
                {
                    stream.WriteByte(V4ID);

                    var span = stream.AllocateSpan(V4Size);

                    if (value.TryWriteBytes(span, out var written) is false || written != span.Length)
                        throw new NotImplementedException();
                }
                break;

                case AddressFamily.InterNetworkV6:
                {
                    stream.WriteByte(V6ID);

                    var span = stream.AllocateSpan(V6Size);

                    if (value.TryWriteBytes(span, out var written) is false || written != span.Length)
                        throw new NotImplementedException();
                }
                break;

                default:
                    throw new NotImplementedException("Can Only Serialize IP v4/v6 Addresses");
            }
        }
        public override void Read(ref IPAddress value, ref BinarySource stream)
        {
            var type = stream.ReadByte();

            switch (type)
            {
                case V4ID: //IPv4
                {
                    var span = stream.ReadSpan(V4Size);
                    value = new IPAddress(span);
                }
                break;

                case V6ID: //IPv6
                {
                    var span = stream.ReadSpan(V6Size);
                    value = new IPAddress(span);
                }
                break;
            }
        }
    }

    [SourceGenerator]
    [SourceGenerator.Condition.ConstructedFrom(typeof(Nullable<>))]
    [SourceGenerator.Builder.FromSourceArguments]
    [SourceGenerator.Options.ResolveGenericArguments]
    public class NullableNetworkSerializationResolver<T> : NetworkSerializationResolver<Nullable<T>>
        where T : struct
    {
        public override void Write(in Nullable<T> value, ref BinarySource stream)
        {
            if (value.HasValue)
            {
                NetworkSerializer.Helper.Nullability.Write(false, ref stream);

                NetworkSerializer.WriteValue(value.Value, ref stream);
            }
            else
            {
                NetworkSerializer.Helper.Nullability.Write(true, ref stream);
            }
        }
        public override void Read(ref Nullable<T> value, ref BinarySource stream)
        {
            if (NetworkSerializer.Helper.Nullability.Read(ref stream))
            {
                value = default;
            }
            else
            {
                var reference = value.GetValueOrDefault();
                NetworkSerializer.ReadValue(ref reference, ref stream);
                value = new Nullable<T>(reference);
            }
        }
    }

    #region Tuples
    public class TupleSerializationResolver : NetworkSerializationResolver<ValueTuple>
    {
        public override void Write(in ValueTuple value, ref BinarySource stream) { }
        public override void Read(ref ValueTuple value, ref BinarySource stream) { }
    }

    [SourceGenerator]
    [SourceGenerator.Condition.ConstructedFrom(typeof(ValueTuple<>))]
    [SourceGenerator.Builder.FromSourceArguments]
    [SourceGenerator.Options.ResolveGenericArguments]
    public class TupleSerializationResolver<T1> : NetworkSerializationResolver<ValueTuple<T1>>
    {
        public override void Write(in ValueTuple<T1> value, ref BinarySource stream)
        {
            NetworkSerializer.WriteValue(in value.Item1, ref stream);
        }
        public override void Read(ref ValueTuple<T1> value, ref BinarySource stream)
        {
            NetworkSerializer.ReadValue(ref value.Item1, ref stream);
        }
    }

    [SourceGenerator]
    [SourceGenerator.Condition.ConstructedFrom(typeof(ValueTuple<,>))]
    [SourceGenerator.Builder.FromSourceArguments]
    [SourceGenerator.Options.ResolveGenericArguments]
    public class TupleSerializationResolver<T1, T2> : NetworkSerializationResolver<ValueTuple<T1, T2>>
    {
        public override void Write(in ValueTuple<T1, T2> value, ref BinarySource stream)
        {
            NetworkSerializer.WriteValue(in value.Item1, ref stream);
            NetworkSerializer.WriteValue(in value.Item2, ref stream);
        }
        public override void Read(ref ValueTuple<T1, T2> value, ref BinarySource stream)
        {
            NetworkSerializer.ReadValue(ref value.Item1, ref stream);
            NetworkSerializer.ReadValue(ref value.Item2, ref stream);
        }
    }

    [SourceGenerator]
    [SourceGenerator.Condition.ConstructedFrom(typeof(ValueTuple<,,>))]
    [SourceGenerator.Builder.FromSourceArguments]
    [SourceGenerator.Options.ResolveGenericArguments]
    public class TupleSerializationResolver<T1, T2, T3> : NetworkSerializationResolver<ValueTuple<T1, T2, T3>>
    {
        public override void Write(in ValueTuple<T1, T2, T3> value, ref BinarySource stream)
        {
            NetworkSerializer.WriteValue(in value.Item1, ref stream);
            NetworkSerializer.WriteValue(in value.Item2, ref stream);
            NetworkSerializer.WriteValue(in value.Item3, ref stream);
        }
        public override void Read(ref ValueTuple<T1, T2, T3> value, ref BinarySource stream)
        {
            NetworkSerializer.ReadValue(ref value.Item1, ref stream);
            NetworkSerializer.ReadValue(ref value.Item2, ref stream);
            NetworkSerializer.ReadValue(ref value.Item3, ref stream);
        }
    }

    [SourceGenerator]
    [SourceGenerator.Condition.ConstructedFrom(typeof(ValueTuple<,,,>))]
    [SourceGenerator.Builder.FromSourceArguments]
    [SourceGenerator.Options.ResolveGenericArguments]
    public class TupleSerializationResolver<T1, T2, T3, T4> : NetworkSerializationResolver<ValueTuple<T1, T2, T3, T4>>
    {
        public override void Write(in ValueTuple<T1, T2, T3, T4> value, ref BinarySource stream)
        {
            NetworkSerializer.WriteValue(in value.Item1, ref stream);
            NetworkSerializer.WriteValue(in value.Item2, ref stream);
            NetworkSerializer.WriteValue(in value.Item3, ref stream);
            NetworkSerializer.WriteValue(in value.Item4, ref stream);
        }
        public override void Read(ref ValueTuple<T1, T2, T3, T4> value, ref BinarySource stream)
        {
            NetworkSerializer.ReadValue(ref value.Item1, ref stream);
            NetworkSerializer.ReadValue(ref value.Item2, ref stream);
            NetworkSerializer.ReadValue(ref value.Item3, ref stream);
            NetworkSerializer.ReadValue(ref value.Item4, ref stream);
        }
    }

    [SourceGenerator]
    [SourceGenerator.Condition.ConstructedFrom(typeof(ValueTuple<,,,,>))]
    [SourceGenerator.Builder.FromSourceArguments]
    [SourceGenerator.Options.ResolveGenericArguments]
    public class TupleSerializationResolver<T1, T2, T3, T4, T5> : NetworkSerializationResolver<ValueTuple<T1, T2, T3, T4, T5>>
    {
        public override void Write(in ValueTuple<T1, T2, T3, T4, T5> value, ref BinarySource stream)
        {
            NetworkSerializer.WriteValue(in value.Item1, ref stream);
            NetworkSerializer.WriteValue(in value.Item2, ref stream);
            NetworkSerializer.WriteValue(in value.Item3, ref stream);
            NetworkSerializer.WriteValue(in value.Item4, ref stream);
            NetworkSerializer.WriteValue(in value.Item5, ref stream);
        }
        public override void Read(ref ValueTuple<T1, T2, T3, T4, T5> value, ref BinarySource stream)
        {
            NetworkSerializer.ReadValue(ref value.Item1, ref stream);
            NetworkSerializer.ReadValue(ref value.Item2, ref stream);
            NetworkSerializer.ReadValue(ref value.Item3, ref stream);
            NetworkSerializer.ReadValue(ref value.Item4, ref stream);
            NetworkSerializer.ReadValue(ref value.Item5, ref stream);
        }
    }

    [SourceGenerator]
    [SourceGenerator.Condition.ConstructedFrom(typeof(ValueTuple<,,,,,>))]
    [SourceGenerator.Builder.FromSourceArguments]
    [SourceGenerator.Options.ResolveGenericArguments]
    public class TupleSerializationResolver<T1, T2, T3, T4, T5, T6> : NetworkSerializationResolver<ValueTuple<T1, T2, T3, T4, T5, T6>>
    {
        public override void Write(in ValueTuple<T1, T2, T3, T4, T5, T6> value, ref BinarySource stream)
        {
            NetworkSerializer.WriteValue(in value.Item1, ref stream);
            NetworkSerializer.WriteValue(in value.Item2, ref stream);
            NetworkSerializer.WriteValue(in value.Item3, ref stream);
            NetworkSerializer.WriteValue(in value.Item4, ref stream);
            NetworkSerializer.WriteValue(in value.Item5, ref stream);
            NetworkSerializer.WriteValue(in value.Item6, ref stream);
        }
        public override void Read(ref ValueTuple<T1, T2, T3, T4, T5, T6> value, ref BinarySource stream)
        {
            NetworkSerializer.ReadValue(ref value.Item1, ref stream);
            NetworkSerializer.ReadValue(ref value.Item2, ref stream);
            NetworkSerializer.ReadValue(ref value.Item3, ref stream);
            NetworkSerializer.ReadValue(ref value.Item4, ref stream);
            NetworkSerializer.ReadValue(ref value.Item5, ref stream);
            NetworkSerializer.ReadValue(ref value.Item6, ref stream);
        }
    }

    [SourceGenerator]
    [SourceGenerator.Condition.ConstructedFrom(typeof(ValueTuple<,,,,,,>))]
    [SourceGenerator.Builder.FromSourceArguments]
    [SourceGenerator.Options.ResolveGenericArguments]
    public class TupleSerializationResolver<T1, T2, T3, T4, T5, T6, T7> : NetworkSerializationResolver<ValueTuple<T1, T2, T3, T4, T5, T6, T7>>
    {
        public override void Write(in ValueTuple<T1, T2, T3, T4, T5, T6, T7> value, ref BinarySource stream)
        {
            NetworkSerializer.WriteValue(in value.Item1, ref stream);
            NetworkSerializer.WriteValue(in value.Item2, ref stream);
            NetworkSerializer.WriteValue(in value.Item3, ref stream);
            NetworkSerializer.WriteValue(in value.Item4, ref stream);
            NetworkSerializer.WriteValue(in value.Item5, ref stream);
            NetworkSerializer.WriteValue(in value.Item6, ref stream);
            NetworkSerializer.WriteValue(in value.Item7, ref stream);
        }
        public override void Read(ref ValueTuple<T1, T2, T3, T4, T5, T6, T7> value, ref BinarySource stream)
        {
            NetworkSerializer.ReadValue(ref value.Item1, ref stream);
            NetworkSerializer.ReadValue(ref value.Item2, ref stream);
            NetworkSerializer.ReadValue(ref value.Item3, ref stream);
            NetworkSerializer.ReadValue(ref value.Item4, ref stream);
            NetworkSerializer.ReadValue(ref value.Item5, ref stream);
            NetworkSerializer.ReadValue(ref value.Item6, ref stream);
            NetworkSerializer.ReadValue(ref value.Item7, ref stream);
        }
    }

    [SourceGenerator]
    [SourceGenerator.Condition.ConstructedFrom(typeof(ValueTuple<,,,,,,,>))]
    [SourceGenerator.Builder.FromSourceArguments]
    [SourceGenerator.Options.ResolveGenericArguments]
    public class TupleSerializationResolver<T1, T2, T3, T4, T5, T6, T7, TRest> : NetworkSerializationResolver<ValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest>>
        where TRest : struct
    {
        public override void Write(in ValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest> value, ref BinarySource stream)
        {
            NetworkSerializer.WriteValue(in value.Item1, ref stream);
            NetworkSerializer.WriteValue(in value.Item2, ref stream);
            NetworkSerializer.WriteValue(in value.Item3, ref stream);
            NetworkSerializer.WriteValue(in value.Item4, ref stream);
            NetworkSerializer.WriteValue(in value.Item5, ref stream);
            NetworkSerializer.WriteValue(in value.Item6, ref stream);
            NetworkSerializer.WriteValue(in value.Item7, ref stream);
            NetworkSerializer.WriteValue(in value.Rest, ref stream);
        }
        public override void Read(ref ValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest> value, ref BinarySource stream)
        {
            NetworkSerializer.ReadValue(ref value.Item1, ref stream);
            NetworkSerializer.ReadValue(ref value.Item2, ref stream);
            NetworkSerializer.ReadValue(ref value.Item3, ref stream);
            NetworkSerializer.ReadValue(ref value.Item4, ref stream);
            NetworkSerializer.ReadValue(ref value.Item5, ref stream);
            NetworkSerializer.ReadValue(ref value.Item6, ref stream);
            NetworkSerializer.ReadValue(ref value.Item7, ref stream);
            NetworkSerializer.ReadValue(ref value.Rest, ref stream);
        }
    }
    #endregion

    #region Collections
    [SourceGenerator]
    [SourceGenerator.Condition.IsArray]
    [SourceGenerator.Builder.FromArrayType]
    public class ArrayNetworkSerializationResolver<TValue> : NetworkSerializationResolver<TValue[]>
    {
        public override void Write(in TValue[] array, ref BinarySource stream)
        {
            if (NetworkSerializer.Helper.Nullability.Length.Write(in array, ref stream))
                return;

            for (int i = 0; i < array.Length; i++)
                NetworkSerializer.WriteValue(in array[i], ref stream);
        }

        public override void Read(ref TValue[] array, ref BinarySource stream)
        {
            if (NetworkSerializer.Helper.Nullability.Length.Read(ref stream, out var length))
            {
                array = default;
                return;
            }

            EnsureLength(ref array, length);

            for (int i = 0; i < length; i++)
                NetworkSerializer.ReadValue(ref array[i], ref stream);
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

    [SourceGenerator]
    [SourceGenerator.Condition.ConstructedFrom(typeof(ArraySegment<>))]
    [SourceGenerator.Builder.FromSourceArguments]
    [SourceGenerator.Options.ResolveGenericArguments]
    public class ArraySegmentNetworkSerializationResolver<TValue> : NetworkSerializationResolver<ArraySegment<TValue>>
    {
        public override void Write(in ArraySegment<TValue> segment, ref BinarySource stream)
        {
            NetworkSerializer.Helper.Length.Write(segment.Count, ref stream);

            for (int i = 0; i < segment.Count; i++)
                NetworkSerializer.WriteValue(segment[i], ref stream);
        }

        public override void Read(ref ArraySegment<TValue> segment, ref BinarySource stream)
        {
            var length = NetworkSerializer.Helper.Length.Read(ref stream);

            EnsureCount(ref segment, length);

            for (int i = 0; i < length; i++)
            {
                var item = segment[i];
                NetworkSerializer.ReadValue(ref item, ref stream);
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

    [SourceGenerator]
    [SourceGenerator.Condition.ConstructedFrom(typeof(List<>))]
    [SourceGenerator.Builder.FromSourceArguments]
    [SourceGenerator.Options.ResolveGenericArguments]
    public class ListNetworkSerializationResolver<TValue> : NetworkSerializationResolver<List<TValue>>
    {
        public override void Write(in List<TValue> list, ref BinarySource stream)
        {
            if (NetworkSerializer.Helper.Nullability.Length.Write(in list, ref stream))
                return;

            for (int i = 0; i < list.Count; i++)
                NetworkSerializer.WriteValue(list[i], ref stream);
        }

        public override void Read(ref List<TValue> list, ref BinarySource stream)
        {
            if (NetworkSerializer.Helper.Nullability.Length.Read(ref stream, out var length))
            {
                list = default;
                return;
            }

            EnsureCapacity(ref list, length);

            for (int i = 0; i < length; i++)
            {
                if (i >= list.Count)
                {
                    var item = NetworkSerializer.ReadValue<TValue>(ref stream);
                    list.Add(item);
                }
                else
                {
                    var item = list[i];
                    NetworkSerializer.ReadValue(ref item, ref stream);
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

    [SourceGenerator]
    [SourceGenerator.Condition.ConstructedFrom(typeof(HashSet<>))]
    [SourceGenerator.Builder.FromSourceArguments]
    [SourceGenerator.Options.ResolveGenericArguments]
    public class HashSetNetworkSerializationResolver<TValue> : NetworkSerializationResolver<HashSet<TValue>>
    {
        public override void Write(in HashSet<TValue> list, ref BinarySource stream)
        {
            if (NetworkSerializer.Helper.Nullability.Length.Write(list?.Count, ref stream))
                return;

            foreach (var item in list)
                NetworkSerializer.WriteValue(item, ref stream);
        }

        public override void Read(ref HashSet<TValue> set, ref BinarySource stream)
        {
            if (NetworkSerializer.Helper.Nullability.Length.Read(ref stream, out var length))
            {
                set = default;
                return;
            }

            EnsureCapacity(ref set, length);

            for (int i = 0; i < length; i++)
            {
                var item = NetworkSerializer.ReadValue<TValue>(ref stream);
                set.Add(item);
            }
        }

        void EnsureCapacity(ref HashSet<TValue> list, int length)
        {
            if (list is null)
            {
                list = new HashSet<TValue>(length);
            }
            else
            {
                list.Clear();
                list.EnsureCapacity(length);
            }
        }
    }

    [SourceGenerator]
    [SourceGenerator.Condition.ConstructedFrom(typeof(Queue<>))]
    [SourceGenerator.Builder.FromSourceArguments]
    [SourceGenerator.Options.ResolveGenericArguments]
    public class QueueNetworkSerializationResolver<TValue> : NetworkSerializationResolver<Queue<TValue>>
    {
        public override void Write(in Queue<TValue> list, ref BinarySource stream)
        {
            if (NetworkSerializer.Helper.Nullability.Length.Write(list?.Count, ref stream))
                return;

            foreach (var item in list)
                NetworkSerializer.WriteValue(item, ref stream);
        }

        public override void Read(ref Queue<TValue> set, ref BinarySource stream)
        {
            if (NetworkSerializer.Helper.Nullability.Length.Read(ref stream, out var length))
            {
                set = default;
                return;
            }

            EnsureCapacity(ref set, length);

            for (int i = 0; i < length; i++)
            {
                var item = NetworkSerializer.ReadValue<TValue>(ref stream);
                set.Enqueue(item);
            }
        }

        void EnsureCapacity(ref Queue<TValue> list, int length)
        {
            if (list is null)
            {
                list = new Queue<TValue>(length);
            }
            else
            {
                list.Clear();

                //No ensure capacity method available
            }
        }
    }

    [SourceGenerator]
    [SourceGenerator.Condition.ConstructedFrom(typeof(Stack<>))]
    [SourceGenerator.Builder.FromSourceArguments]
    [SourceGenerator.Options.ResolveGenericArguments]
    public class StackNetworkSerializationResolver<TValue> : NetworkSerializationResolver<Stack<TValue>>
    {
        public override void Write(in Stack<TValue> list, ref BinarySource stream)
        {
            if (NetworkSerializer.Helper.Nullability.Length.Write(list?.Count, ref stream))
                return;

            foreach (var item in list)
                NetworkSerializer.WriteValue(item, ref stream);
        }

        public override void Read(ref Stack<TValue> set, ref BinarySource stream)
        {
            if (NetworkSerializer.Helper.Nullability.Length.Read(ref stream, out var length))
            {
                set = default;
                return;
            }

            var cache = new TValue[length]; //Must cache because Stack is LIFO and no Insert or Reverse methods are available for the stack collection type

            for (int i = 0; i < length; i++) //Read all elements
                cache[i] = NetworkSerializer.ReadValue<TValue>(ref stream);

            EnsureCapacity(ref set, length);

            for (int i = cache.Length - 1; i >= 0; i--) //Push in reverse order
                set.Push(cache[i]);
        }

        void EnsureCapacity(ref Stack<TValue> list, int length)
        {
            if (list is null)
            {
                list = new Stack<TValue>(length);
            }
            else
            {
                list.Clear();

                //No ensure capacity method available
            }
        }
    }

    [SourceGenerator]
    [SourceGenerator.Condition.ConstructedFrom(typeof(Dictionary<,>))]
    [SourceGenerator.Builder.FromSourceArguments]
    [SourceGenerator.Options.ResolveGenericArguments]
    public class DictionaryNetworkSerializationResolver<TKey, TValue> : NetworkSerializationResolver<Dictionary<TKey, TValue>>
    {
        public override void Write(in Dictionary<TKey, TValue> collection, ref BinarySource stream)
        {
            if (NetworkSerializer.Helper.Nullability.Length.Write(in collection, ref stream))
                return;

            foreach (var (key, value) in collection)
            {
                NetworkSerializer.WriteValue(in key, ref stream);
                NetworkSerializer.WriteValue(in value, ref stream);
            }
        }

        public override void Read(ref Dictionary<TKey, TValue> collection, ref BinarySource stream)
        {
            if (NetworkSerializer.Helper.Nullability.Length.Read(ref stream, out var length))
            {
                collection = null;
                return;
            }

            EnsureCapacity(ref collection, length);

            for (int i = 0; i < length; i++)
            {
                var key = NetworkSerializer.ReadValue<TKey>(ref stream);
                var value = NetworkSerializer.ReadValue<TValue>(ref stream);

                collection.Add(key, value);
            }
        }

        void EnsureCapacity(ref Dictionary<TKey, TValue> collection, int length)
        {
            if (collection is null)
            {
                collection = new Dictionary<TKey, TValue>(length);
            }
            else
            {
                collection.Clear();
                collection.EnsureCapacity(length);
            }
        }
    }
    #endregion

    #region Manual
    [SourceGenerator]
    [SourceGenerator.Condition.ImplementsInterface(typeof(IManualNetworkSerialization))]
    [SourceGenerator.Builder.FromSourceType]
    public class ManualNetworkSerializationResolver<TValue> : NetworkSerializationResolver<TValue>
        where TValue : IManualNetworkSerialization, new()
    {
        readonly bool IsNullable;

        public override void Write(in TValue value, ref BinarySource stream)
        {
            if (IsNullable && NetworkSerializer.Helper.Nullability.Write(in value, ref stream))
                return;

            value.Write(ref stream);
        }
        public override void Read(ref TValue value, ref BinarySource stream)
        {
            if (IsNullable && NetworkSerializer.Helper.Nullability.Read(ref stream))
            {
                value = default;
                return;
            }

            value ??= new();

            value.Read(ref stream);
        }

        public ManualNetworkSerializationResolver()
        {
            IsNullable = NetworkSerializer.Helper.Nullability.IsNullable<TValue>();
        }
    }
    public interface IManualNetworkSerialization
    {
        void Write(ref BinarySource stream);
        void Read(ref BinarySource stream);
    }
    #endregion

    #region Auto
    [SourceGenerator]
    [SourceGenerator.Condition.ImplementsInterface(typeof(IAutoNetworkSerialization))]
    [SourceGenerator.Builder.FromSourceType]
    public class AutoNetworkSerializationResolver<TValue> : NetworkSerializationResolver<TValue>
        where TValue : IAutoNetworkSerialization, new()
    {
        readonly bool IsNullable;

        public override void Write(in TValue value, ref BinarySource stream)
        {
            if (IsNullable && NetworkSerializer.Helper.Nullability.Write(in value, ref stream))
                return;

            var context = new AutoSerializationContext(ref stream, AutoSerializationMode.Write);

            value.Select(ref context);

            stream = context.Stream;
        }
        public override void Read(ref TValue value, ref BinarySource stream)
        {
            if (IsNullable && NetworkSerializer.Helper.Nullability.Read(ref stream))
            {
                value = default;
                return;
            }

            value ??= new();

            var context = new AutoSerializationContext(ref stream, AutoSerializationMode.Read);
            value.Select(ref context);

            stream = context.Stream;
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

    public ref struct AutoSerializationContext
    {
        internal BinarySource Stream;

        public AutoSerializationMode Mode { get; }

        public readonly bool IsWriting => Mode is AutoSerializationMode.Write;
        public readonly bool IsReading => Mode is AutoSerializationMode.Read;

        public void Select<[NetworkSerializationMarker] TValue>(ref TValue value)
        {
            switch (Mode)
            {
                case AutoSerializationMode.Write:
                    NetworkSerializer.WriteValue(in value, ref Stream);
                    break;

                case AutoSerializationMode.Read:
                    NetworkSerializer.ReadValue(ref value, ref Stream);
                    break;

                default: throw new NotImplementedException();
            }
        }

        public AutoSerializationContext(ref BinarySource Stream, AutoSerializationMode Mode)
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
    [SourceGenerator]
    [SourceGenerator.Condition.DecoratedBy.DecoratedBy(typeof(NetworkBlittableAttribute))]
    [SourceGenerator.Builder.FromSourceType]
    public unsafe class BlittableNetworkSerializationResolver<TValue> : NetworkSerializationResolver<TValue>
        where TValue : unmanaged
    {
        public override void Write(in TValue value, ref BinarySource stream)
            => NetworkSerializer.Helper.Blittable.Write(in value, ref stream);

        public override void Read(ref TValue value, ref BinarySource stream)
            => NetworkSerializer.Helper.Blittable.Read(ref value, ref stream);
    }

    [AttributeUsage(AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
    public sealed class NetworkBlittableAttribute : Attribute { }
    #endregion
}
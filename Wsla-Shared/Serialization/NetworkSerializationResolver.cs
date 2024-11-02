using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Wsla.Serialization
{
    public static partial class NetworkSerializationResolver
    {
        public static void Register<TValue, TResolver>()
            where TResolver : NetworkSerializationResolver<TValue>, new()
        {
            var resolver = new TResolver();
            Register(resolver);
        }
        public static void Register<TValue>(NetworkSerializationResolver<TValue> resolver)
        {
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

        internal static class Collection<TValue>
        {
            internal static NetworkSerializationResolver<TValue> Instance;
        }

        public static class Registeration
        {
            public static void LoadAll()
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    Load(assembly);
            }

            public static void Load(Assembly assembly)
            {
                var attributes = assembly.GetCustomAttributes<NetworkSerializationResolverRegisterationAttribute>();

                foreach (var attribute in attributes)
                    attribute.Invoke();
            }
        }

        static NetworkSerializationResolver()
        {
            Register<byte, BlittableNetworkSerializationResolver<byte>>();
            Register<sbyte, BlittableNetworkSerializationResolver<sbyte>>();

            Register<short, BlittableNetworkSerializationResolver<short>>();
            Register<ushort, BlittableNetworkSerializationResolver<ushort>>();

            Register<int, BlittableNetworkSerializationResolver<int>>();
            Register<uint, BlittableNetworkSerializationResolver<uint>>();

            Register<float, BlittableNetworkSerializationResolver<float>>();
            Register<double, BlittableNetworkSerializationResolver<double>>();

            Register<string, StringNetworkSerializationResolver>();

            Registeration.LoadAll();
        }
    }

    public abstract class NetworkSerializationResolver<TValue>
    {
        public abstract void Write(in TValue value, INetworkStream stream);
        public abstract void Read(ref TValue value, INetworkStream stream);
    }

    #region Poco
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

    public unsafe class EnumNetworkSerializationResolver<TEnum, TBacking> : NetworkSerializationResolver<TEnum>
        where TEnum : unmanaged, Enum
        where TBacking : unmanaged
    {
        readonly int Size = sizeof(TBacking);

        public override void Write(in TEnum value, INetworkStream stream)
        {
            var buffer = stream.PopSpan(Size);

            fixed (void* source = &value)
            fixed (void* destination = buffer)
            {
                Buffer.MemoryCopy(source, destination, Size, Size);
            }
        }
        public override void Read(ref TEnum value, INetworkStream stream)
        {
            var buffer = stream.PopSpan(Size);

            fixed (void* source = buffer)
            fixed (void* destination = &value)
            {
                Buffer.MemoryCopy(source, destination, Size, Size);
            }
        }
    }
    #endregion

    #region List
    public class ArrayNetworkSerializationResolver<TValue> : NetworkSerializationResolver<TValue[]>
    {
        public override void Write(in TValue[] array, INetworkStream stream)
        {
            if (NetworkSerializer.Helper.Nullability.Length.Write(in array, stream))
                return;

            for (int i = 0; i < array.Length; i++)
                NetworkSerializer.WriteValue(array[i], stream);
        }

        public override void Read(ref TValue[] array, INetworkStream stream)
        {
            if (NetworkSerializer.Helper.Nullability.Length.Read(stream, out var length))
            {
                array = default;
                return;
            }

            if (length == 0)
            {
                array = Array.Empty<TValue>();
                return;
            }

            if (array is null || array.Length != length)
                array = new TValue[length];

            for (int i = 0; i < length; i++)
                array[i] = NetworkSerializer.ReadValue<TValue>(stream);
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

            if (length is 0)
            {
                segment = ArraySegment<TValue>.Empty;
                return;
            }

            if (segment.Array is null || segment.Array.Length < length)
                segment = new TValue[length];
            else
                segment = new ArraySegment<TValue>(segment.Array, 0, length);

            for (int i = 0; i < length; i++)
                segment[i] = NetworkSerializer.ReadValue<TValue>(stream);
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

            if (list is null)
            {
                list = new List<TValue>(length);
            }
            else
            {
                list.Clear();
                list.Capacity = length;
            }

            for (int i = 0; i < length; i++)
            {
                var item = NetworkSerializer.ReadValue<TValue>(stream);
                list.Add(item);
            }
        }
    }
    #endregion

    #region Custom
    public class ManualNetworkSerializationResolver<TValue> : NetworkSerializationResolver<TValue>
        where TValue : IManualNetworkSerialization, new()
    {
        readonly bool IsNullable = NetworkSerializer.Helper.Nullability.IsNullable<TValue>();

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
            }
            else
            {
                value ??= new();
                value.Read(stream);
            }
        }
    }
    public interface IManualNetworkSerialization
    {
        void Write(INetworkStream stream);
        void Read(INetworkStream stream);
    }

    public class AutoNetworkSerializationResolver<TValue> : NetworkSerializationResolver<TValue>
        where TValue : IAutoNetworkSerialization, new()
    {
        readonly bool IsNullable = NetworkSerializer.Helper.Nullability.IsNullable<TValue>();

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
            }
            else
            {
                value ??= new();
                var context = new AutoSerializationContext(stream, AutoSerializationMode.Read);
                value.Select(ref context);
            }
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
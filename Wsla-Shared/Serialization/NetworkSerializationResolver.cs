using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
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
        public abstract void Write<TStream>(in TValue value, ref TStream stream)
            where TStream : INetworkStream;

        public abstract void Read<TStream>(ref TValue value, ref TStream stream)
            where TStream : INetworkStream;
    }

    #region Poco
    public class StringNetworkSerializationResolver : NetworkSerializationResolver<string>
    {
        static Encoding Encoder => Encoding.UTF8;

        public override void Write<TStream>(in string value, ref TStream stream)
        {
            if (NetworkSerializer.Helper.Nullability.Length.Write(in value, value.Length, ref stream))
                return;

            if (value.Length <= 1024)
            {
                //Small strings optimization

                Span<byte> buffer = stackalloc byte[Encoder.GetMaxByteCount(value.Length)];

                var written = Encoder.GetBytes(value, buffer);

                var destination = stream.Take(written);

                buffer.Slice(0, written).CopyTo(destination);
            }
            else
            {
                var buffer = stream.Take(Encoder.GetByteCount(value));

                Encoder.GetBytes(value, buffer);
            }

            var count = Encoder.GetByteCount(value);
        }

        public override void Read<TStream>(ref string value, ref TStream stream)
        {
            if (NetworkSerializer.Helper.Nullability.Length.Read(ref stream, out var length))
            {
                value = null;
                return;
            }

            var span = stream.Take(length);

            value = Encoder.GetString(span);
        }
    }

    public unsafe class EnumNetworkSerializationResolver<TEnum, TBacking> : NetworkSerializationResolver<TEnum>
        where TEnum : unmanaged, Enum
        where TBacking : unmanaged
    {
        readonly int Size = sizeof(TBacking);

        public override void Write<TStream>(in TEnum value, ref TStream stream)
        {
            var buffer = stream.Take(Size);

            fixed (void* source = &value)
            fixed (void* destination = buffer)
            {
                Buffer.MemoryCopy(source, destination, Size, Size);
            }
        }
        public override void Read<TStream>(ref TEnum value, ref TStream stream)
        {
            var buffer = stream.Take(Size);

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
        where TValue : new()
    {
        public override void Write<TStream>(in TValue[] array, ref TStream stream)
        {
            if (NetworkSerializer.Helper.Nullability.Length.Write(in array, ref stream))
                return;

            for (int i = 0; i < array.Length; i++)
                NetworkSerializer.WriteValue(array[i], ref stream);
        }

        public override void Read<TStream>(ref TValue[] array, ref TStream stream)
        {
            if (NetworkSerializer.Helper.Nullability.Length.Read(ref stream, out var length))
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
                array[i] = NetworkSerializer.ReadValue<TValue, TStream>(ref stream);
        }
    }

    public class ArraySegmentNetworkSerializationResolver<TValue> : NetworkSerializationResolver<ArraySegment<TValue>>
        where TValue : new()
    {
        public override void Write<TStream>(in ArraySegment<TValue> segment, ref TStream stream)
        {
            NetworkSerializer.Helper.Length.Write(segment.Count, ref stream);

            for (int i = 0; i < segment.Count; i++)
                NetworkSerializer.WriteValue(segment[i], ref stream);
        }

        public override void Read<TStream>(ref ArraySegment<TValue> segment, ref TStream stream)
        {
            var length = NetworkSerializer.Helper.Length.Read(ref stream);

            if (length is 0)
            {
                segment = new ArraySegment<TValue>(default, 0, 0);
                return;
            }

            if (segment.Array is null || segment.Array.Length < length)
                segment = new TValue[length];
            else
                segment = new ArraySegment<TValue>(segment.Array, 0, length);

            for (int i = 0; i < length; i++)
                segment[i] = NetworkSerializer.ReadValue<TValue, TStream>(ref stream);
        }
    }

    public class ListNetworkSerializationResolver<TValue> : NetworkSerializationResolver<List<TValue>>
        where TValue : new()
    {
        public override void Write<TStream>(in List<TValue> list, ref TStream stream)
        {
            if (NetworkSerializer.Helper.Nullability.Length.Write(in list, ref stream))
                return;

            for (int i = 0; i < list.Count; i++)
                NetworkSerializer.WriteValue(list[i], ref stream);
        }

        public override void Read<TStream>(ref List<TValue> list, ref TStream stream)
        {
            if (NetworkSerializer.Helper.Nullability.Length.Read(ref stream, out var length))
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
                var item = NetworkSerializer.ReadValue<TValue, TStream>(ref stream);
                list.Add(item);
            }
        }
    }
    #endregion

    #region Custom
    public class ManualNetworkSerializationResolver<TValue> : NetworkSerializationResolver<TValue>
        where TValue : IManualNetworkSerialization
    {
        readonly bool IsNullable = NetworkSerializer.Helper.Nullability.IsNullable<TValue>();

        public override void Write<TStream>(in TValue value, ref TStream stream)
        {
            if (IsNullable && NetworkSerializer.Helper.Nullability.Write(in value, ref stream))
                return;

            value.Write(ref stream);
        }
        public override void Read<TStream>(ref TValue value, ref TStream stream)
        {
            if (IsNullable && NetworkSerializer.Helper.Nullability.Read(ref stream))
            {
                value = default;
                return;
            }

            value.Read(ref stream);
        }
    }
    public interface IManualNetworkSerialization
    {
        void Write<TStream>(ref TStream stream)
            where TStream : INetworkStream;

        void Read<TStream>(ref TStream stream)
            where TStream : INetworkStream;
    }

    public class AutoNetworkSerializationResolver<TValue> : NetworkSerializationResolver<TValue>
        where TValue : IAutoNetworkSerialization
    {
        readonly bool IsNullable = NetworkSerializer.Helper.Nullability.IsNullable<TValue>();

        public override void Write<TStream>(in TValue value, ref TStream stream)
        {
            if (IsNullable && NetworkSerializer.Helper.Nullability.Write(in value, ref stream))
                return;

            var context = new AutoSerializationContext(AutoSerializationMode.Write);

            value.Select(ref stream, ref context);
        }
        public override void Read<TStream>(ref TValue value, ref TStream stream)
        {
            if (IsNullable && NetworkSerializer.Helper.Nullability.Read(ref stream))
            {
                value = default;
                return;
            }

            var context = new AutoSerializationContext(AutoSerializationMode.Read);

            value.Select(ref stream, ref context);
        }
    }
    public interface IAutoNetworkSerialization
    {
        void Select<TStream>(ref TStream stream, ref AutoSerializationContext context)
            where TStream : INetworkStream;
    }
    public readonly ref struct AutoSerializationContext
    {
        public AutoSerializationMode Mode { get; }

        public readonly bool IsWriting => Mode is AutoSerializationMode.Write;
        public readonly bool IsReading => Mode is AutoSerializationMode.Read;

        public readonly void Select<[NetworkSerializationMarker] TValue, TStream>(ref TValue value, ref TStream stream)
            where TStream : INetworkStream
        {
            switch (Mode)
            {
                case AutoSerializationMode.Write:
                    NetworkSerializer.WriteValue(in value, ref stream);
                    break;

                case AutoSerializationMode.Read:
                    NetworkSerializer.ReadValue(ref value, ref stream);
                    break;

                default: throw new NotImplementedException();
            }
        }

        public AutoSerializationContext(AutoSerializationMode Mode)
        {
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
        public override void Write<TStream>(in TValue value, ref TStream stream)
            => NetworkSerializer.Helper.Blittable.Write(in value, ref stream);

        public override void Read<TStream>(ref TValue value, ref TStream stream)
            => NetworkSerializer.Helper.Blittable.Read(ref value, ref stream);
    }

    [AttributeUsage(AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
    public sealed class NetworkBlittableAttribute : Attribute { }
    #endregion
}
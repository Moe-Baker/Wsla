using System;
using System.Collections.Generic;
using System.Reflection;

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
            Register<byte, ByteNetworkSerializationResolver>();
            Register<int, IntNetworkSerializationResolver>();
            Register<float, FloatNetworkSerializationResolver>();

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
    public class ByteNetworkSerializationResolver : NetworkSerializationResolver<byte>
    {
        public override void Write<TStream>(in byte value, ref TStream stream)
        {
            var span = stream.Take(1);
            span[0] = value;
        }
        public override void Read<TStream>(ref byte value, ref TStream stream)
        {
            var span = stream.Take(1);
            value = span[0];
        }
    }

    public class IntNetworkSerializationResolver : NetworkSerializationResolver<int>
    {
        public override void Write<TStream>(in int value, ref TStream stream)
            => NetworkSerializer.Helper.Blittable.Write(in value, ref stream);
        public override void Read<TStream>(ref int value, ref TStream stream)
            => NetworkSerializer.Helper.Blittable.Read(ref value, ref stream);
    }

    public class FloatNetworkSerializationResolver : NetworkSerializationResolver<float>
    {
        public override void Write<TStream>(in float value, ref TStream stream)
            => NetworkSerializer.Helper.Blittable.Write(in value, ref stream);

        public override void Read<TStream>(ref float value, ref TStream stream)
            => NetworkSerializer.Helper.Blittable.Read(ref value, ref stream);
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
                NetworkSerializer.Write(array[i], ref stream);
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
                array[i] = NetworkSerializer.Read<TValue, TStream>(ref stream);
        }
    }

    public class ArraySegmentNetworkSerializationResolver<TValue> : NetworkSerializationResolver<ArraySegment<TValue>>
        where TValue : new()
    {
        public override void Write<TStream>(in ArraySegment<TValue> segment, ref TStream stream)
        {
            NetworkSerializer.Helper.Length.Write(segment.Count, ref stream);

            for (int i = 0; i < segment.Count; i++)
                NetworkSerializer.Write(segment[i], ref stream);
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
                segment[i] = NetworkSerializer.Read<TValue, TStream>(ref stream);
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
                NetworkSerializer.Write(list[i], ref stream);
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
                var item = NetworkSerializer.Read<TValue, TStream>(ref stream);
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
                    NetworkSerializer.Write(in value, ref stream);
                    break;

                case AutoSerializationMode.Read:
                    NetworkSerializer.Read(ref value, ref stream);
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
using System;
using System.Collections.Generic;

using Wsla.Serialization;

[assembly: NetworkSerializationResolverRegisteration(typeof(Wsla.NetworkTypeSerializationResolver), 0, "Register")]

namespace Wsla
{
    public static class NetworkTypes
    {
        static Dictionary<Type, byte> TypeToID;
        static Type[] IDToType;

        public const byte Capacity = byte.MaxValue;

        public const byte UserSpace = 50;

        public static bool TryGet<T>(out byte id) => TypeToID.TryGetValue(typeof(T), out id);
        public static bool TryGet(Type type, out byte id) => TypeToID.TryGetValue(type, out id);
        public static bool TryGet(byte id, out Type type)
        {
            type = IDToType[id];
            return type is not null;
        }

        public static byte Get<T>() => Get(typeof(T));
        public static byte Get(Type type)
        {
            if (TryGet(type, out var id) is false)
                throw new ArgumentException($"Type {type} is not Registerd as a NetworkType");

            return id;
        }
        public static Type Get(byte id)
        {
            if (TryGet(id, out var type) is false)
                throw new ArgumentException($"No NetworkType with ID {id} Registered");

            return type;
        }

        public static void Register<T>(byte id) => Register(typeof(T), id);
        public static void Register(Type type, byte id)
        {
            TypeToID[type] = id;
            IDToType[id] = type;
        }

        static NetworkTypes()
        {
            TypeToID = new Dictionary<Type, byte>(Capacity);
            IDToType = new Type[Capacity];

            byte counter = 0;
            static void Add<T>(ref byte counter)
            {
                Register<T>(counter);
                counter += 1;
            }

            //Sample
            Add<ClientConnectionResponse>(ref counter);

            Add<ClientConnectMessage>(ref counter);
            Add<ClientDisconnectMessage>(ref counter);

            Add<SpawnEntityRequest>(ref counter);
            Add<SpawnEntityResponse>(ref counter);
            Add<SpawnEntityCommand>(ref counter);

            Add<ChangeScenesRequest>(ref counter);
            Add<ChangeScenesCommand>(ref counter);
        }
    }

    public class NetworkTypeSerializationResolver : NetworkSerializationResolver<Type>
    {
        public override void Write<TStream>(in Type value, ref TStream stream)
        {
            if (NetworkTypes.TryGet(value, out var id) is false)
                throw new ArgumentException($"Type ({value}) not Registered as NetworkType");

            NetworkSerializer.WriteValue(in id, ref stream);
        }

        public override void Read<TStream>(ref Type value, ref TStream stream)
        {
            var id = ReadValue(ref stream);

            if (NetworkTypes.TryGet(id, out var type) is false)
                throw new ArgumentException($"No NetworkType with ID {id} Registered");

            value = type;
        }

        public static byte ReadValue<TStream>(ref TStream stream) where TStream : INetworkStream
            => NetworkSerializer.ReadValue<byte, TStream>(ref stream);

        static void Register()
        {
            NetworkSerializationResolver.Register<Type, NetworkTypeSerializationResolver>();
        }
    }
}
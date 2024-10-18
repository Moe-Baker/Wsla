using System;
using System.Collections.Generic;

namespace Wsla.Shared.Global
{
    public static class NetworkTypes
    {
        static Dictionary<Type, byte> TypeToID;
        static Type[] IDToType;

        public const byte Capacity = byte.MaxValue;

        public const byte UserSpace = 100;

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

            Add<ClientConnectEvent>(ref counter);
            Add<ClientDisconnectEvent>(ref counter);

            Add<NetworkPingEvent>(ref counter);
            Add<NetworkPongEvent>(ref counter);
        }
    }
}
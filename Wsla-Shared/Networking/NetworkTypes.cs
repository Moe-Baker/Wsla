using System;
using System.Collections.Generic;

namespace Wsla.Shared
{
    public static class NetworkTypes
    {
        static Dictionary<Type, byte> TypeToID;
        static Type[] IDToType;

        public const byte Capacity = byte.MaxValue;

        public const byte UserSpace = 100;

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
            Add<string>(ref counter);
        }
    }
}
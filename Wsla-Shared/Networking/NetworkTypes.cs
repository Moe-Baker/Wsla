using System;
using System.Collections.Generic;

using Wsla.Serialization;

[assembly: NetworkSerializationResolverRegisteration(typeof(Wsla.WslaSerializationResolvers), 0, "Register")]

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

            Add<ClientConnectionResponse>(ref counter);

            Add<ClientConnectMessage>(ref counter);
            Add<ClientDisconnectMessage>(ref counter);

            Add<SpawnPrefabEntityRequest>(ref counter);
            Add<SpawnPrefabEntityResponse>(ref counter);
            Add<SpawnPrefabEntityCommand>(ref counter);

            Add<DespawnEntityRequest>(ref counter);
            Add<DespawnEntityCommand>(ref counter);

            Add<ChangeSceneRequest>(ref counter);
            Add<ChangeSceneCommand>(ref counter);

            Add<BroadcastNetworkRpcRequest>(ref counter);
            Add<BufferNetworkRpcRequest>(ref counter);
            Add<TargetNetworkRpcRequest>(ref counter);
            Add<NetworkRpcCommand>(ref counter);

            Add<SpawnScenenRequest>(ref counter);
            Add<SpawnSceneCommand>(ref counter);

            Add<BroadcastNetworkVariableRequest>(ref counter);
            Add<BufferNetworkVariableRequest>(ref counter);
            Add<NetworkVariableCommand>(ref counter);

            Add<TakeEntityOwnershipRequest>(ref counter);
            Add<TransferEntityOwnershipCommand>(ref counter);
        }
    }

    public class NetworkTypeSerializationResolver : NetworkSerializationResolver<Type>
    {
        public override void Write(in Type value, INetworkStream stream)
        {
            if (NetworkTypes.TryGet(value, out var id) is false)
                throw new ArgumentException($"Type ({value}) not Registered as NetworkType");

            NetworkSerializer.WriteValue(in id, stream);
        }

        public override void Read(ref Type value, INetworkStream stream)
        {
            var id = ReadValue(stream);

            if (NetworkTypes.TryGet(id, out var type) is false)
                throw new ArgumentException($"No NetworkType with ID {id} Registered");

            value = type;
        }

        public static byte ReadValue(INetworkStream stream)
            => NetworkSerializer.ReadValue<byte>(stream);
    }

    public static class WslaSerializationResolvers
    {
        static void Register()
        {
            NetworkSerializationResolver.Register<Type, NetworkTypeSerializationResolver>();

            NetworkSerializationResolver.Register<NetworkVariableID, BlittableNetworkSerializationResolver<NetworkVariableID>>();
            NetworkSerializationResolver.Register<NetworkRpcID, BlittableNetworkSerializationResolver<NetworkRpcID>>();

            NetworkSerializationResolver.Register(new FixedStringNetworkSerializationResolver<FixedString20>(x => new FixedString20(x)));
            NetworkSerializationResolver.Register(new FixedStringNetworkSerializationResolver<FixedString40>(x => new FixedString40(x)));
            NetworkSerializationResolver.Register(new FixedStringNetworkSerializationResolver<FixedString60>(x => new FixedString60(x)));
            NetworkSerializationResolver.Register(new FixedStringNetworkSerializationResolver<FixedString80>(x => new FixedString80(x)));
        }
    }
}
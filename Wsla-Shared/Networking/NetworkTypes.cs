using System;
using System.Collections.Generic;

using Wsla.Serialization;

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

            Add<WslaError>(ref counter);

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

            Add<SpawnSceneRequest>(ref counter);
            Add<SpawnSceneCommand>(ref counter);

            Add<BroadcastNetworkVariableRequest>(ref counter);
            Add<BufferNetworkVariableRequest>(ref counter);
            Add<NetworkVariableCommand>(ref counter);

            Add<TakeEntityOwnershipRequest>(ref counter);
            Add<TransferEntityOwnershipCommand>(ref counter);

            Add<RegisterRelayRequest>(ref counter);

            Add<CreateRoomCommand>(ref counter);
            Add<CreateRoomConfirmation>(ref counter);
            Add<CreateRoomRequest>(ref counter);

            Add<RemoveRoomRequest>(ref counter);

            Add<UpdateRoomRequest>(ref counter);
            Add<UpdateRoomsRequest>(ref counter);

            Add<StartMatchMakingRequest>(ref counter);
            Add<MatchmakingFailResponse>(ref counter);
            Add<MatchmakingSuccessResponse>(ref counter);
        }
    }

    public class NetworkTypeSerializationResolver : NetworkSerializationResolver<Type>
    {
        public override void Write(in Type value, ref BinarySource stream)
        {
            if (NetworkTypes.TryGet(value, out var id) is false)
                throw new ArgumentException($"Type ({value}) not Registered as NetworkType");

            NetworkSerializer.WriteValue(in id, ref stream);
        }

        public override void Read(ref Type value, ref BinarySource stream)
        {
            var id = ReadValue(ref stream);

            if (NetworkTypes.TryGet(id, out var type) is false)
                throw new ArgumentException($"No NetworkType with ID {id} Registered");

            value = type;
        }

        public static byte ReadValue(ref BinarySource stream)
            => NetworkSerializer.ReadValue<byte>(ref stream);
    }
}
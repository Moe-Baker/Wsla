using System;

using UnityEngine;

using Wsla.Serialization;
using Wsla.Unity;

[assembly: NetworkSerializationResolverRegistration(typeof(UnityTypesNetworkSerialization), 0, "Register")]

namespace Wsla.Unity
{
    public static class UnityTypesNetworkSerialization
    {
        static void Register()
        {
            NetworkSerializationResolver.Register<Quaternion, BlittableNetworkSerializationResolver<Quaternion>>();

            NetworkSerializationResolver.Register<Color, BlittableNetworkSerializationResolver<Color>>();

            NetworkSerializationResolver.Register<Vector2, BlittableNetworkSerializationResolver<Vector2>>();
            NetworkSerializationResolver.Register<Vector2Int, BlittableNetworkSerializationResolver<Vector2Int>>();

            NetworkSerializationResolver.Register<Vector3, BlittableNetworkSerializationResolver<Vector3>>();
            NetworkSerializationResolver.Register<Vector3Int, BlittableNetworkSerializationResolver<Vector3Int>>();

            NetworkSerializationResolver.Register<Vector4, BlittableNetworkSerializationResolver<Vector4>>();

            NetworkSerializationResolver.Register<NetworkClient, NetworkClientSerializationResolver>();
            NetworkSerializationResolver.Register<NetworkEntity, NetworkEntitySerializationResolver>();
        }
    }

    public class NetworkClientSerializationResolver : NetworkSerializationResolver<NetworkClient>
    {
        NetworkAPI API => NetworkAPI.Instance;

        public override void Write(in NetworkClient value, ref BinarySource stream)
        {
            if (value == null)
                NetworkSerializer.WriteValue(NetworkClientID.None, ref stream);
            else
                NetworkSerializer.WriteValue(value.ID, ref stream);
        }

        public override void Read(ref NetworkClient value, ref BinarySource stream)
        {
            NetworkSerializer.ReadValue(ref stream, out NetworkClientID id);

            if (id == NetworkClientID.None)
            {
                value = null;
            }
            else
            {
                if (API.Room.Clients.TryGet(id, out value) is false)
                    throw new InvalidOperationException($"No Network Client with ID {id} Found");
            }
        }
    }

    public class NetworkEntitySerializationResolver : NetworkSerializationResolver<NetworkEntity>
    {
        NetworkAPI API => NetworkAPI.Instance;

        public override void Write(in NetworkEntity value, ref BinarySource stream)
        {
            if (value == null)
            {
                NetworkSerializer.WriteValue(NetworkEntityID.None, ref stream);
            }
            else
            {
                if (value.IsSpawned is false)
                    throw new InvalidOperationException($"Can't Serialize Entity {value} Since it's not Spawned");

                NetworkSerializer.WriteValue(value.ID, ref stream);
            }
        }

        public override void Read(ref NetworkEntity value, ref BinarySource stream)
        {
            NetworkSerializer.ReadValue(ref stream, out NetworkEntityID id);

            if (id == NetworkEntityID.None)
            {
                value = null;
            }
            else
            {
                if (API.Room.Entities.TryGet(id, out value) is false)
                    throw new InvalidOperationException($"No Network Entity with ID {id} Found");
            }
        }
    }

    public class NetworkBehaviourSerializationResolver<T> : NetworkSerializationResolver<T>
        where T : class, INetworkBehaviour
    {
        public override void Write(in T value, ref BinarySource stream)
        {
            if (value == null)
            {
                NetworkSerializer.WriteValue(NetworkBehaviourID.None, ref stream);
            }
            else
            {
                NetworkSerializer.WriteValue(value.Network.ID, ref stream);
                NetworkSerializer.WriteValue(value.Network.Entity, ref stream);
            }
        }

        public override void Read(ref T value, ref BinarySource stream)
        {
            NetworkSerializer.ReadValue(ref stream, out NetworkBehaviourID id);

            if (id == NetworkBehaviourID.None)
            {
                value = null;
                return;
            }

            NetworkSerializer.ReadValue(ref stream, out NetworkEntity entity);

            if (entity.Behaviours.TryGet(id, out var behaviour) is false)
                throw new InvalidOperationException($"No Behaviour with ID {id} Found on Entity ({entity})");

            value = behaviour.Script as T;

            if (value == null)
                throw new InvalidCastException($"Can't Cast Behaviour ({behaviour.Script}) as Type ({typeof(T).Name})");
        }
    }
}
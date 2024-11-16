using UnityEngine;

using Wsla.Serialization;
using Wsla.Unity;

[assembly: NetworkSerializationResolverRegisteration(typeof(UnityTypesNetworkSerialization), 0, "Register")]

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
        }
    }
}
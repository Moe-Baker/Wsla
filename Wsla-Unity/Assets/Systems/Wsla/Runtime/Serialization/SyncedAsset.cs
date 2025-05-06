using UnityEngine;

using Wsla.Serialization;

namespace Wsla.Unity
{
    /// <summary>
    /// An interface to implement on Scriptable Objects to make them serializable across the network
    /// </summary>
    public interface ISyncedAsset
    {
        public bool IncludeInSync => true;
    }

    public class SyncedAssetSerializationResolver<T> : NetworkSerializationResolver<T>
        where T : ScriptableObject, ISyncedAsset
    {
        NetworkAPI API => NetworkAPI.Instance;

        public override void Write(in T value, ref BinarySource stream)
        {
            if (API.SyncedAssets.TryGet(value, out var id) is false)
            {
                id = NetworkEntityResource.None;
                NetworkLog.Error($"No Synced Asset [{value}] Registered to Network API Synced Assets, Will Serialize as Null");
            }

            NetworkSerializer.WriteValue(in id, ref stream);
        }
        public override void Read(ref T value, ref BinarySource stream)
        {
            var id = NetworkSerializer.ReadValue<NetworkEntityResource>(ref stream);

            if (id == NetworkEntityResource.None)
            {
                value = null;
                return;
            }

            if (API.SyncedAssets.TryGet(id, out var asset) is false)
            {
                NetworkLog.Error($"No Synced Asset Found with ID {id}");
                value = null;
                return;
            }

            if (asset is not T target)
            {
                NetworkLog.Error($"Mismatched Synced Asset Type, Expected [{typeof(T)}] Found [{asset.GetType()}]");
                value = null;
                return;
            }

            value = target;
        }
    }
}
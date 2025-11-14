#if UNITY_ADDRESSABLE
using System;

using UnityEngine;
using UnityEngine.UIElements;

using Toolbox;

#if UNITY_EDITOR
using UnityEditor;
#endif

using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Wsla.Unity
{
    [CreateAssetMenu(menuName = NetworkAPI.Path + "Synced Addressable Scene List", order = 5)]
    public class SyncedAddressableSceneList : ScriptableObject
    {
        [field: SerializeField]
        public AddressableNetworkSceneReference[] Scenes { get; private set; }
    }

    //Provided by JamesFrowenDev
    //https://discussions.unity.com/t/something-like-assetreferencet-sceneasset/773034/9
    [Serializable]
    public class AddressableNetworkSceneReference : AssetReference, IEquatable<AddressableNetworkSceneReference>
    {
        /// <summary>
        /// Construct a new AssetReference object.
        /// </summary>
        /// <param name="guid">The guid of the asset.</param>
        public AddressableNetworkSceneReference(string guid) : base(guid) { }

        public override bool Equals(object obj)
        {
            if (obj is AddressableNetworkSceneReference other)
                return Equals(other);

            return false;
        }
        public bool Equals(AddressableNetworkSceneReference other)
        {
            return (this.AssetGUID == other.AssetGUID);
        }

        public override int GetHashCode() => AssetGUID.GetHashCode();

        /// <inheritdoc/>
        public override bool ValidateAsset(UnityEngine.Object obj)
        {
#if UNITY_EDITOR
            var type = obj.GetType();
            return typeof(UnityEditor.SceneAsset).IsAssignableFrom(type);
#else
        return false;
#endif

        }
        /// <inheritdoc/>
        public override bool ValidateAsset(string path)
        {
#if UNITY_EDITOR
            var type = UnityEditor.AssetDatabase.GetMainAssetTypeAtPath(path);
            return typeof(UnityEditor.SceneAsset).IsAssignableFrom(type);
#else
        return false;
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// Type-specific override of parent editorAsset.  Used by the editor to represent the asset referenced.
        /// </summary>
        public new UnityEditor.SceneAsset editorAsset => (UnityEditor.SceneAsset)base.editorAsset;
#endif

        public static bool operator ==(AddressableNetworkSceneReference a, AddressableNetworkSceneReference b) => a.Equals(b);
        public static bool operator !=(AddressableNetworkSceneReference a, AddressableNetworkSceneReference b) => !a.Equals(b);
    }
}
#endif
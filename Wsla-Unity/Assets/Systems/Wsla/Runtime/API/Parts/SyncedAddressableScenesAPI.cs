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
    [Serializable]
    public class SyncedAddressableScenesAPI : NetworkAPI.Property
    {
        [field: SerializeField]
        public AddressableNetworkSceneReference[] Scenes { get; private set; }
        public object[] GetAllAddressableKeys()
        {
            var keys = new object[Scenes.Length];

            for (int i = 0; i < keys.Length; i++)
                keys[i] = Scenes[i].RuntimeKey;

            return keys;
        }

        public bool TryGetReference(NetworkSceneID id, out AssetReference reference)
        {
            if (Scenes.IsValidIndex(id.Index) is false)
            {
                reference = default;
                return false;
            }

            reference = Scenes[id.Index];
            return true;
        }

        /// <summary>
        /// Downloads all the registered scenes, ensuring that their bundles are cached an ready to load,
        /// not required for general usage, but will accelerate first ever scene loading
        /// </summary>
        public AsyncOperationHandle DownloadAllAddressables()
        {
            var keys = GetAllAddressableKeys();
            return Addressables.DownloadDependenciesAsync(keys, Addressables.MergeMode.Union);
        }

#if UNITY_EDITOR
        [CustomPropertyDrawer(typeof(SyncedAddressableScenesAPI))]
        class Drawer : PropertyDrawer
        {
            public override VisualElement CreatePropertyGUI(SerializedProperty property)
            {
                var Scenes = property.FindBackingFieldRelative(nameof(SyncedAddressableScenesAPI.Scenes));
                return new UnityEditor.UIElements.PropertyField(Scenes, property.displayName);
            }
        }
#endif
    }

    //Provided by JamesFrowenDev
    //https://discussions.unity.com/t/something-like-assetreferencet-sceneasset/773034/9
    [Serializable]
    public class AddressableNetworkSceneReference : AssetReference
    {
        /// <summary>
        /// Construct a new AssetReference object.
        /// </summary>
        /// <param name="guid">The guid of the asset.</param>
        public AddressableNetworkSceneReference(string guid) : base(guid) { }

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
    }
}
#endif
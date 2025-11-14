#if UNITY_ADDRESSABLE
using System;

using Cysharp.Threading.Tasks;

using Toolbox;

using UnityEditor;

using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UIElements;

namespace Wsla.Unity
{
    [Serializable]
    public class SyncedAddressableScenesAPI : NetworkAPI.Property
    {
        [SerializeField]
        AssetReferenceT<SyncedAddressableSceneList> List;
        AsyncOperationHandle<SyncedAddressableSceneList> Handle;
        public bool Assigned { get; private set; }

        public AddressableNetworkSceneReference[] Scenes { get; private set; }
        bool IsPrepared => Scenes is not null;

        public override void Set(NetworkAPI value)
        {
            base.Set(value);
            Scenes = null;
        }

        internal async UniTask<WslaResponse<WslaError>> Prepare()
        {
            if (IsPrepared) return true;

            Assigned = List.RuntimeKeyIsValid();
            if (Assigned is false)
            {
                Scenes = Array.Empty<AddressableNetworkSceneReference>();
                return true;
            }

            try
            {
                Handle = List.LoadAssetAsync();
                await Handle.ToUniTask();

                Scenes = Handle.Result.Scenes;

                API.OnDispose += Handle.Release;

                return true;
            }
            catch (Exception ex)
            {
                return WslaError.From(ex);
            }
        }

        public object[] GetAllAddressableKeys()
        {
            if (Assigned is false)
                throw new InvalidOperationException($"No Addresssable Scene List Was Assigned");

            var keys = new object[Scenes.Length];

            for (int i = 0; i < keys.Length; i++)
                keys[i] = Scenes[i].RuntimeKey;

            return keys;
        }

        public NetworkSceneID GetID(AddressableNetworkSceneReference reference)
        {
            if (TryGetID(reference, out var id) is false)
                throw new ArgumentException($"Scene ({reference}) Not Registered as Synced Addresable Scene");

            return id;
        }
        public bool TryGetID(AddressableNetworkSceneReference reference, out NetworkSceneID id)
        {
            if (Assigned is false)
            {
                NetworkLog.Error($"Attempting to Reference Addressable Scene {reference}, But No Addresssable Scene List Was Assigned");
                id = default;
                return false;
            }

            for (byte i = 0; i < Scenes.Length; i++)
            {
                if (reference == Scenes[i])
                {
                    id = new(i, NetworkSceneSource.Addressable);
                    return true;
                }
            }

            id = default;
            return false;
        }

        public bool TryGetReference(NetworkSceneID id, out AssetReference reference)
        {
            if (Assigned is false)
            {
                NetworkLog.Error($"Attempting to Reference Addressable Scene {id}, But No Addresssable Scene List Was Assigned");
                reference = default;
                return false;
            }

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
            if (Assigned is false)
                throw new InvalidOperationException($"No Addresssable Scene List Was Assigned");

            var keys = GetAllAddressableKeys();
            return Addressables.DownloadDependenciesAsync(keys, Addressables.MergeMode.Union);
        }

#if UNITY_EDITOR
        [CustomPropertyDrawer(typeof(SyncedAddressableScenesAPI))]
        class Drawer : PropertyDrawer
        {
            public override VisualElement CreatePropertyGUI(SerializedProperty property)
            {
                var Scenes = property.FindPropertyRelative(nameof(SyncedAddressableScenesAPI.List));
                return new UnityEditor.UIElements.PropertyField(Scenes, property.displayName);
            }
        }
#endif
    }
}
#endif
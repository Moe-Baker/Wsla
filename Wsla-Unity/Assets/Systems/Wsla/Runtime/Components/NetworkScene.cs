using Cysharp.Threading.Tasks;

using System;
using System.Collections.Generic;

using Toolbox;

using UnityEditor;

#if UNITY_EDITOR
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
#endif

using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_ADDRESSABLE
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
#endif

namespace Wsla.Unity
{
    public class NetworkScene : MonoBehaviour
    {
        public NetworkSceneID ID { get; private set; }
        public NetworkSceneVersion Version { get; private set; }

        public int BuildIndex => ID.Index;
        public Scene UnityScene => gameObject.scene;

        public NetworkSceneDefinition Definition => new(ID, Version);

        [field: SerializeField]
        public NetworkEntity[] Locals { get; private set; }
        public bool TryGetLocal(NetworkResourceID resource, out NetworkEntity entity)
        {
            var index = resource.Value;

            if (Locals.IsValidIndex(index) is false)
            {
                entity = default;
                return false;
            }

            entity = Locals[index];
            return true;
        }

        static NetworkAPI API => NetworkAPI.Instance;
        static RoomAPI Room => API.Room;

        #region Spawn
        public bool IsSpawned { get; private set; }

        internal void Spawn()
        {
            NetworkLog.Info($"Spawning Scene {BuildIndex}");

            //Destroy any non-spawned scene objects as they were destroyed during gameplay
            for (int i = 0; i < Locals.Length; i++)
                if (Locals[i].IsSpawned is false)
                    Locals[i].Destroy();

            IsSpawned = true;
            OnSpawn?.Invoke();
        }
        public event Action OnSpawn;

        internal void Despawn()
        {
            IsSpawned = false;
            OnDespawn?.Invoke();
        }
        public event Action OnDespawn;
        #endregion

        internal void Assign(IList<NetworkEntity> source)
        {
            Locals = new NetworkEntity[source.Count];
            for (int i = 0; i < source.Count; i++)
                Locals[i] = source[i];
        }

        void Awake()
        {
            Manager.Register(this);
        }
        void OnDestroy()
        {
            Manager.Unregister(this);
        }

#if UNITY_EDITOR
        public class SceneProcessor : IProcessSceneWithReport
        {
            public const int CallbackOrder = 0;
            int IOrderedCallback.callbackOrder => CallbackOrder;

            static (List<NetworkEntity> Total, List<NetworkEntity> Temp) Cache = (new(), new());

            public void OnProcessScene(Scene scene, BuildReport report)
            {
                Cache.Temp.Clear();
                Cache.Total.Clear();

                var roots = scene.GetRootGameObjects();

                //Ensure No Existing Network Scene
                foreach (var root in roots)
                {
                    if (root.TryGetComponent(out NetworkScene existing) is false)
                        continue;

                    NetworkLog.Warning($"Pre-Existing Network Scene Found In Scene {scene.name}, This is Fine if This Scene is an Addresssable Scene, Otherwise, Remove the Network Scene Component");
                    return;
                }

                //Collect All Network Entity
                foreach (var root in roots)
                {
                    root.GetComponentsInChildren(true, Cache.Temp);
                    Cache.Total.AddRange(Cache.Temp);
                }

                if (Cache.Total.Count is 0)
                    return;

                var gameObject = new GameObject("Network Scene");
                SceneManager.MoveGameObjectToScene(gameObject, scene);

                gameObject.SetActive(false);

                var component = gameObject.AddComponent<NetworkScene>();
                component.Assign(Cache.Total);

                gameObject.SetActive(true);
            }
        }

        [CustomEditor(typeof(NetworkScene))]
        class Inspector : Editor
        {
            public override void OnInspectorGUI()
            {
                base.OnInspectorGUI();

                EditorGUILayout.HelpBox("This component is automatically added to scenes, you don't need to modify it or add it manually", MessageType.Info);
            }
        }
#endif

        public static class Manager
        {
            static List<NetworkScene> List;

            public static class Loading
            {
                static UniTaskCompletionSource<NetworkScene> Entry;
                static UniTaskCompletionSource<NetworkScene> RegisterEntry()
                {
                    Entry = new();
                    return Entry;
                }
                static void CompleteEntry(NetworkScene scene)
                {
                    if (Entry == null)
                    {
                        NetworkLog.Warning($"No Loading Entry Found For Network Scene ({scene}), Network Scenes Can Only Be Loaded Via Network Methods");
                        return;
                    }

                    Entry.TrySetResult(scene);
                    Entry = null;
                }

                public static async UniTask<NetworkScene> Load(NetworkSceneID id, NetworkSceneVersion version, LoadSceneMode mode, IProgress<float> progress)
                {
                    var entry = RegisterEntry();

                    switch (id.Source)
                    {
                        case NetworkSceneSource.Build:
                            await LoadFromBuild(id, version, mode, progress);
                            break;

                        case NetworkSceneSource.Addressable:
                            await LoadFromAddressable(id, version, mode, progress);
                            break;
                    }

                    progress?.Report(1f);

                    var scene = await entry.Task;

                    scene.ID = id;
                    scene.Version = version;

                    return scene;
                }

                static async UniTask LoadFromBuild(NetworkSceneID id, NetworkSceneVersion version, LoadSceneMode mode, IProgress<float> progress)
                {
                    await SceneManager.LoadSceneAsync(id.Index, mode).ToUniTask(progress: progress);
                }

#pragma warning disable CS1998 //Method only runs synchronyously for exceptional path
                static async UniTask LoadFromAddressable(NetworkSceneID id, NetworkSceneVersion version, LoadSceneMode mode, IProgress<float> progress)
                {
#if UNITY_ADDRESSABLE
                    if (API.AddressableScenes.TryGetReference(id, out var reference) is false)
                        throw new ArgumentException($"No Addressable Scene With ID: {id} Found");

                    var handle = reference.LoadSceneAsync(mode);

                    while (true)
                    {
                        switch (handle.Status)
                        {
                            case AsyncOperationStatus.None:
                                progress?.Report(handle.PercentComplete);
                                break;

                            case AsyncOperationStatus.Succeeded:
                                return;

                            case AsyncOperationStatus.Failed:
                                throw handle.OperationException;
                        }

                        await UniTask.NextFrame();
                    }
#else
                    throw new InvalidOperationException($"Addressable Network Scene Load Requested But Addressable Package Not Installed");
#endif
                }
#pragma warning restore CS1998

                static Loading()
                {
                    OnRegister += CompleteEntry;
                }
            }
            public static class Unloading
            {
                public static async UniTask Unload(NetworkScene scene, IProgress<float> progress)
                {
                    await SceneManager.UnloadSceneAsync(scene.UnityScene).ToUniTask(progress);
                    progress?.Report(1f);
                }
            }

            internal static void Register(NetworkScene scene)
            {
                List.Add(scene);

                OnRegister?.Invoke(scene);
            }
            public static event RegistrationDelegate OnRegister;

            internal static void Unregister(NetworkScene scene)
            {
                List.Remove(scene);

                OnUnregister?.Invoke(scene);
            }
            public static event RegistrationDelegate OnUnregister;

            public delegate void RegistrationDelegate(NetworkScene scene);

            static Manager()
            {
                List = new(1);
            }
        }
    }
}
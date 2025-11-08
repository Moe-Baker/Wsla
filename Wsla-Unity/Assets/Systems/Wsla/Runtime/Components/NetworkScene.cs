using Cysharp.Threading.Tasks;

using LiteNetLib.Utils;

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

namespace Wsla.Unity
{
    public class NetworkScene : MonoBehaviour
    {
        public NetworkSceneID ID { get; private set; }
        public NetworkSceneVersion Version { get; private set; }

        public int BuildIndex => ID.Value;
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
            ID = new NetworkSceneID((byte)UnityScene.buildIndex);

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
                static List<Entry> Entries;
                struct Entry
                {
                    public NetworkSceneID ID { get; }
                    public UniTaskCompletionSource<NetworkScene> Operation { get; }

                    public Entry(NetworkSceneID ID) : this(ID, new()) { }
                    public Entry(NetworkSceneID ID, UniTaskCompletionSource<NetworkScene> Operation)
                    {
                        this.ID = ID;
                        this.Operation = Operation;
                    }
                }

                static void CompleteEntry(NetworkScene scene)
                {
                    for (int i = 0; i < Entries.Count; i++)
                    {
                        var entry = Entries[i];

                        if (entry.ID != scene.ID)
                            continue;

                        entry.Operation.TrySetResult(scene);
                        Entries.RemoveAt(i);
                        return;
                    }

                    NetworkLog.Warning($"No Loading Entry Found For Network Scene ({scene}), Network Scenes Can Only Be Loaded Via Network Methods");
                }

                public static async UniTask<NetworkScene> Load(NetworkSceneID ID, NetworkSceneVersion Version, LoadSceneMode mode)
                {
                    var entry = new Entry(ID);
                    Entries.Add(entry);

                    await SceneManager.LoadSceneAsync(ID.Value, mode).ToUniTask();

                    var scene = await entry.Operation.Task;

                    scene.Version = Version;

                    return scene;
                }

                static Loading()
                {
                    Entries = new(1);

                    OnRegister += CompleteEntry;
                }
            }
            public static class Unloading
            {
                public static async UniTask Unload(NetworkScene scene)
                {
                    await SceneManager.UnloadSceneAsync(scene.UnityScene).ToUniTask();
                }
            }

            public static bool TryGet(NetworkSceneID ID, out NetworkScene instance)
            {
                for (int i = 0; i < List.Count; i++)
                {
                    instance = List[i];

                    if (instance.ID == ID)
                        return true;
                }

                instance = default;
                return false;
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
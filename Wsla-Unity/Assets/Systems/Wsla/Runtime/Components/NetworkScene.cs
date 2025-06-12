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

using Wsla.Serialization;

namespace Wsla.Unity
{
    public class NetworkScene : MonoBehaviour
    {
        public NetworkSceneID ID { get; private set; }
        public int BuildIndex => ID.Value;

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

        internal void WriteRequest(NetDataWriter writer)
        {
            NetworkSerializer.WriteValue((byte)Locals.Length, writer);

            foreach (var entity in Locals)
            {
                if (entity.Authority is NetworkEntityAuthorityMode.Explicit)
                {
                    Debug.LogWarning($"Network Entity ({entity.gameObject}) in Scene ({entity.gameObject.scene.name}) Has an {entity.Authority} Authority, Scene Objects Can Only have {NetworkEntityAuthorityMode.Authoritative} & {NetworkEntityAuthorityMode.Transferable} Authority, Switching");
                    entity.Authority = NetworkEntityAuthorityMode.Authoritative;
                }

                NetworkSerializer.WriteValue(entity.Authority, writer);
            }
        }

        internal void Assign(IList<NetworkEntity> source)
        {
            Locals = new NetworkEntity[source.Count];
            for (int i = 0; i < source.Count; i++)
                Locals[i] = source[i];
        }

        void Awake()
        {
            ID = new NetworkSceneID((byte)gameObject.scene.buildIndex);
        }
        void Start()
        {
            if (Room is null)
            {
                Debug.LogWarning($"Network Scene Loaded Without an Active Room, Having Network Entities on you Main Menu is not Supported!");
                return;
            }

            Room.Scene.Register(this);
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
    }
}
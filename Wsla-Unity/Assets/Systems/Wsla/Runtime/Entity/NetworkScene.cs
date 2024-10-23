using System;

using Toolbox;

using UnityEngine;

namespace Wsla.Unity
{
    public class NetworkScene : MonoBehaviour
    {
        [field: SerializeField]
        public NetworkEntity[] Entities { get; private set; }
        public bool TryGet(NetworkEntityResource resource, out NetworkEntity entity)
        {
            var index = resource.Value;

            if (Entities.IsValidIndex(index) is false)
            {
                entity = default;
                return false;
            }

            entity = Entities[index];
            return true;
        }

        static NetworkAPI API => NetworkAPI.Instance;

        public NetworkSceneID ID { get; private set; }

        public bool IsSpawned { get; private set; }
        internal void Spawn()
        {
            IsSpawned = true;

            OnSpawn?.Invoke();
        }
        public event Action OnSpawn;

        void Awake()
        {
            ID = new NetworkSceneID((byte)gameObject.scene.buildIndex);
        }
        void Start()
        {
            API.Room.Instance.Scenes.Register(this);
        }
    }
}
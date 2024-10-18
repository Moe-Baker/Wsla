using System;

using Toolbox;

using UnityEngine;

using Wsla.Shared.Global;

namespace Wsla.Unity
{
    [CreateAssetMenu(menuName = Path + "Network API")]
    public partial class NetworkAPI : ScriptableManager<NetworkAPI>
    {
        public const string Path = "Wsla/";

        [field: SerializeField]
        public RoomAPI Room { get; private set; }

        [field: SerializeField]
        public SyncedPrefabsAPI SyncedPrefabs { get; private set; }

        [Serializable]
        public class Property : ReferenceProperty<NetworkAPI>
        {
            public NetworkAPI API => Reference;
        }

        protected override void Init()
        {
            base.Init();

            NetworkLog.Handler = (type, item) =>
            {
                switch (type)
                {
                    case NetworkLogType.Trace:
                        Debug.Log(item);
                        break;

                    case NetworkLogType.Info:
                        Debug.Log(item);
                        break;

                    case NetworkLogType.Warning:
                        Debug.LogWarning(item);
                        break;

                    case NetworkLogType.Error:
                        Debug.LogError(item);
                        break;

                    default: throw new NotImplementedException();
                }
            };

            Room.Set(this);
        }
    }
}
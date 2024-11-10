using System;

using Toolbox;

using UnityEngine;

namespace Wsla.Unity
{
    [CreateAssetMenu(menuName = Path + "Network API")]
    public partial class NetworkAPI : ScriptableManager<NetworkAPI>
    {
        public const string Path = "Wsla/";

        [field: SerializeField]
        public RoomAPI Room { get; private set; }

        [field: SerializeField]
        public ChannelsAPI Channels { get; private set; }

        [field: SerializeField]
        public SyncedPrefabsAPI SyncedPrefabs { get; private set; }

        [Serializable]
        public class Property : ReferenceProperty<NetworkAPI>
        {
            internal NetworkAPI API => Reference;
        }

        public override ExecutionModeSelection ExecutionMode => ExecutionModeSelection.All;

        void OnValidate()
        {
            Channels.Validate();
        }

        protected override void Init()
        {
            base.Init();

            NetworkLog.Handler = LogHandler;

            Room.Set(this);
            Channels.Set(this);
            SyncedPrefabs.Set(this);
        }

        [HideInCallstack]
        void LogHandler(NetworkLogType type, object item)
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
        }
    }
}
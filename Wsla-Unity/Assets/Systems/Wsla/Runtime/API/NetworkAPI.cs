using System;
using System.Threading.Tasks;

using Toolbox;

using UnityEngine;

namespace Wsla.Unity
{
    [CreateAssetMenu(menuName = Path + "Network API")]
    public partial class NetworkAPI : ScriptableManager<NetworkAPI>, IPreCache
    {
        public const string Path = "Wsla/";

        [field: SerializeField]
        public CoordinatorAddressProperty CoordinatorAddress { get; private set; }

        [field: Space]

        [field: SerializeField]
        public ApplicationIDProperty ApplicationID { get; private set; }

        [field: SerializeField]
        public GameVersionProperty GameVersion { get; private set; }

        [field: SerializeField]
        public SerializedTimeSpan Timeout { get; private set; } = SerializedTimeSpan.FromSeconds(5);

        [field: SerializeField]
        public RestAPI REST { get; private set; }

        [field: SerializeField]
        public MatchMakingAPI MatchMaking { get; private set; }

        [field: Space]

        [field: SerializeField]
        public NetworkUpdateAPI NetworkUpdate { get; private set; }

        [field: SerializeField]
        public TickAPI Tick { get; private set; }

        [field: SerializeField]
        public ChannelsAPI Channels { get; private set; }

        [field: SerializeField]
        public SyncedPrefabsAPI SyncedPrefabs { get; private set; }

        [field: SerializeField]
        public SyncedAssetsAPI SyncedAssets { get; private set; }

#if UNITY_ADDRESSABLE
        [field: SerializeField]
        public SyncedAddressableScenesAPI AddressableScenes { get; private set; }
#endif

        [field: SerializeField]
        public RoomAPI Room { get; private set; }

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

            GameVersion.Initialize();
            ApplicationID.Initialize();

            REST.Set(this);
            MatchMaking.Set(this);
            NetworkUpdate.Set(this);
            Tick.Set(this);
            Channels.Set(this);
            SyncedPrefabs.Set(this);
            SyncedAssets.Set(this);

#if UNITY_ADDRESSABLE
            AddressableScenes.Set(this);
#endif

            Room.Set(this);
        }

        public void PreCache()
        {
            SyncedPrefabs.PreCache();
            SyncedAssets.PreCache();
        }

        public bool IsPrepared { get; private set; }
        public async Task<WslaResponse<WslaError>> Prepare()
        {
            if (IsPrepared)
                return true;

            IsPrepared = true;

            //Preare Address
            {
                var response = await CoordinatorAddress.Prepare();
                if (response.IsError)
                {
                    IsPrepared = false;
                    return response.Error;
                }
            }

#if UNITY_ADDRESSABLE
            //Prepare Addressables
            {
                var response = await AddressableScenes.Prepare();
                if (response.IsError)
                {
                    IsPrepared = false;
                    return response.Error;
                }
            }
#endif

            //Update Regions
            {
                var response = await MatchMaking.UpdateRegions();
                if (response.IsError)
                {
                    IsPrepared = false;
                    return response.Error;
                }
            }

            return true;
        }

        protected override void Dispose()
        {
            base.Dispose();

            IsPrepared = false;
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
                {
                    if (item is Exception ex)
                        Debug.LogException(ex);
                    else
                        Debug.LogError(item);
                }
                break;

                default: throw new NotImplementedException();
            }
        }
    }
}
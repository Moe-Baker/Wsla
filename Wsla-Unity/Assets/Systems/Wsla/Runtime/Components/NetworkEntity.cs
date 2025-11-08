using System;
using System.Collections.Generic;

using Toolbox;

using UnityEngine;
using UnityEngine.SceneManagement;


#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Wsla.Unity
{
    public sealed class NetworkEntity : MonoBehaviour, IPreCache
    {
        public NetworkEntityID ID { get; private set; }
        public NetworkEntityOrigin Origin { get; private set; }

        public NetworkResourceID Resource { get; private set; }
        internal void SetResource(NetworkResourceID value)
        {
            Resource = Resource;
        }

        public NetworkClient Owner { get; private set; }
        internal int OwnerRegistration;

        /// <summary>
        /// Are you the owner of this entity? opposite of <see cref="IsRemote"/>
        /// </summary>
        public bool IsLocal => IsSpawned && Owner.IsLocal;

        /// <summary>
        /// Are you NOT the owner of this entity? opposite of <see cref="IsLocal"/>
        /// </summary>
        public bool IsRemote => IsSpawned && Owner.IsRemote;

        internal void AssignOwner(NetworkClient target)
        {
            Owner = target;
        }

        public event TransferOwnerDelegate OnTransferOwner;
        public delegate void TransferOwnerDelegate(ChangePairData<NetworkClient> owner);

        public event OwnershipSetDelegate OnGainedOwnership;
        public event OwnershipSetDelegate OnLostOwnership;
        public delegate void OwnershipSetDelegate();

        internal void TransferOwner(NetworkClient current)
        {
            var previous = Owner;

            AssignOwner(current);

            var change = new ChangePairData<NetworkClient>(previous, current);
            OnTransferOwner?.Invoke(change);

            //Gain/Lost Ownership Events
            {
                if (previous.IsLocal)
                    OnLostOwnership?.Invoke();

                if (current.IsLocal)
                    OnGainedOwnership?.Invoke();
            }
        }

        public void TakeOwnership() => Room.Entities.TakeOwnership(this);

        [field: SerializeField]
        public NetworkEntityAuthorityMode Authority { get; internal set; }

        public NetworkScene Scene { get; private set; }
        public void SetNetworkScene(NetworkScene value)
        {
            Scene = value;
            SceneManager.MoveGameObjectToScene(gameObject, Scene.UnityScene);
        }

        /// <summary>
        /// The default groups that this entity will output data (RPCs) to
        /// </summary>
        public NetworkGroupCollection OutputGroups { get; set; } = NetworkGroupCollection.Everyone;

        internal NetworkEntityTransferToken TransferToken;
        internal void AssignTransferToken(NetworkEntityTransferToken value)
        {
            TransferToken = value;
        }
        internal void IncrementTransferToken() => TransferToken = NetworkEntityTransferToken.Increment(TransferToken);
        void Reset()
        {
            Authority = NetworkEntityAuthorityMode.Transferable;
        }

        public NetworkAPI API => NetworkAPI.Instance;
        public RoomAPI Room => API.Room;

        [field: SerializeField]
        public BehavioursProperty Behaviours { get; private set; }
        [Serializable]
        public class BehavioursProperty
        {
            [field: SerializeField]
            public MonoBehaviour[] Components { get; private set; }

            public Behaviour[] List { get; private set; }
            public bool TryGet(NetworkBehaviourID id, out Behaviour behaviour)
            {
                var index = id.Value;

                if (List.IsValidIndex(index) is false)
                {
                    behaviour = default;
                    return false;
                }

                behaviour = List[index];
                return true;
            }

            public bool TryGet<T>(out T target)
                where T : INetworkBehaviour
            {
                foreach (var behaviour in Entity.Behaviours.List)
                {
                    if (behaviour.Contract is T)
                    {
                        target = (T)behaviour.Contract;
                        return true;
                    }
                }

                target = default;
                return false;
            }
            public T Get<T>()
                where T : INetworkBehaviour
            {
                foreach (var behaviour in Entity.Behaviours.List)
                    if (behaviour.Contract is T target)
                        return target;

                return default;
            }

            internal void PreCache()
            {
                var collection = Entity.GetComponentsInChildren<INetworkBehaviour>(true);
                Components = Array.ConvertAll(collection, x => x as MonoBehaviour);
            }

            internal void Create()
            {
                List = new Behaviour[Components.Length];

                for (byte i = 0; i < Components.Length; i++)
                {
                    var id = new NetworkBehaviourID(i);

                    List[i] = new Behaviour(Entity, id, Components[i]);
                }
            }

            readonly NetworkEntity Entity;
            public BehavioursProperty(NetworkEntity Entity)
            {
                this.Entity = Entity;
            }
        }

        public class Behaviour
        {
            public NetworkEntity Entity { get; }

            public NetworkBehaviourID ID { get; }

            public MonoBehaviour Script { get; }
            public INetworkBehaviour Contract { get; }

            public NetworkAPI API => NetworkAPI.Instance;
            public RoomAPI Room => API.Room;

            public NetworkClient Owner => Entity.Owner;
            public NetworkEntityAuthorityMode Authority => Entity.Authority;

            /// <summary>
            /// <inheritdoc cref="NetworkEntity.IsLocal"/>
            /// </summary>
            public bool IsLocal => Entity.IsLocal;

            /// <summary>
            /// <inheritdoc cref="NetworkEntity.IsRemote"/>
            /// </summary>
            public bool IsRemote => Entity.IsRemote;

            public bool IsSpawned => Entity.IsSpawned;
            public event Action OnSpawn
            {
                add => Entity.OnSpawn += value;
                remove => Entity.OnSpawn -= value;
            }

            public bool IsReplicated => Entity.IsReplicated;
            public event Action OnReplicated
            {
                add => Entity.OnReplicated += value;
                remove => Entity.OnReplicated -= value;
            }

            public event Action OnDespawn
            {
                add => Entity.OnDespawn += value;
                remove => Entity.OnDespawn -= value;
            }

            public RpcProperty RPC { get; }
            public class RpcProperty
            {
                readonly Behaviour Behaviour;

                List<BaseRpcBind> List;
                public bool TryGet(NetworkSyncMemberID id, out BaseRpcBind bind)
                {
                    var index = id.Value;

                    if (List.IsValidIndex(index) is false)
                    {
                        bind = default;
                        return false;
                    }

                    bind = List[index];
                    return true;
                }

                NetworkSyncMemberID Index;

                void Register(BaseRpcBind bind)
                {
                    if (NetworkSyncMemberID.Increment(ref Index, out var id) is false)
                        throw new InvalidOperationException($"Network RPCs Count Exceeded on {Behaviour.Script}, Max Count is {NetworkSyncMemberID.MaxValue}");

                    bind.Set(id, Behaviour);

                    List.Add(bind);
                }

                public RpcProperty(Behaviour Behaviour)
                {
                    this.Behaviour = Behaviour;

                    Collector.Clear();

                    //Attributed Registration
                    if (Behaviour.Contract is IRemoteSyncMembers members)
                        members.RegisterRPCs(Collector);

                    //Custom Registration
                    if (Behaviour.Script is IRegisterCustomRPCs custom)
                    {
                        custom.RegisterCustomRPCs(Collector);

#if UNITY_EDITOR
                        var removed = Collector.RemoveAll(x => x is null);
                        if (removed is not 0)
                            throw new InvalidOperationException($"Removed {removed} Null Custom RPC Binds, Please Ensure Your Custom RPCs are Set inside the Registration Method");
#endif
                    }

                    //Register All
                    {
                        List = new List<BaseRpcBind>(Collector.Count);

                        foreach (var bind in Collector)
                            Register(bind);
                    }
                }

                static List<BaseRpcBind> Collector = new(10);
            }

            public VariablesProperty Variables { get; }
            public class VariablesProperty
            {
                readonly Behaviour Behaviour;

                internal List<NetworkVariable> List;
                public bool TryGet(NetworkSyncMemberID id, out NetworkVariable variable)
                {
                    var index = id.Value;

                    if (List.IsValidIndex(index) is false)
                    {
                        variable = default;
                        return false;
                    }

                    variable = List[index];
                    return true;
                }

                NetworkSyncMemberID Index;
                void Register(NetworkVariable variable)
                {
                    if (NetworkSyncMemberID.Increment(ref Index, out var id) is false)
                        throw new InvalidOperationException($"Network Variables Count Exceeded on {Behaviour.Script}, Max Count is {NetworkSyncMemberID.MaxValue}");

                    variable.Set(id, Behaviour);

                    List.Add(variable);
                }

                public VariablesProperty(Behaviour Behaviour)
                {
                    this.Behaviour = Behaviour;

                    Collector.Clear();

                    //Declared Registration
                    if (Behaviour.Contract is IRemoteSyncMembers members)
                    {
                        members.RegisterVariables(Collector);
                    }

                    //Custom Registration
                    if (Behaviour.Script is IRegisterCustomVariables custom)
                    {
                        custom.RegisterCustomVariables(Collector);

#if UNITY_EDITOR
                        var removed = Collector.RemoveAll(x => x is null);
                        if (removed is not 0)
                            throw new Exception($"Detected {removed} Null Custom Network Variables, Please Ensure Your Custom Variables are Set inside the Registration Method");
#endif
                    }

                    //Register All
                    {
                        List = new List<NetworkVariable>(Collector.Count);

                        foreach (var bind in Collector)
                            Register(bind);
                    }
                }

                static List<NetworkVariable> Collector = new(10);
            }

            public override string ToString() => Script.ToString();

            public Behaviour(NetworkEntity Entity, NetworkBehaviourID ID, MonoBehaviour Script)
            {
                this.Entity = Entity;

                this.ID = ID;
                this.Script = Script;

                Contract = Script as INetworkBehaviour;

                RPC = new RpcProperty(this);
                Variables = new VariablesProperty(this);

                Contract.Set(this);
            }
        }

        void Awake()
        {
#if UNITY_EDITOR
            PreCache();
#endif

            Behaviours.Create();
        }

        public void PreCache() => Behaviours.PreCache();

        internal void Assign(NetworkEntityDefinition definition)
        {
            ID = definition.ID;
            Origin = definition.Origin;
            Resource = definition.Resource;
            Authority = definition.Authority;

            TransferToken = definition.TransferToken;

            //Assign Owner
            if (definition.Authority is NetworkEntityAuthorityMode.Authoritative)
            {
                AssignOwner(Room.Clients.Master);
            }
            else
            {
                if (Room.Clients.TryGet(definition.Owner, out var reference) is false)
                    throw new InvalidOperationException($"No Network Client {definition.Owner} Found");

                AssignOwner(reference);
            }
        }

        #region Spawn
        public bool IsSpawned { get; private set; }

        internal void Spawn()
        {
            NetworkLog.Info($"Spawning Entity {ID}");

            IsSpawned = true;

            OnSpawn?.Invoke();
        }
        public event Action OnSpawn;

        public bool IsReplicated { get; private set; }
        internal void Replicate()
        {
            IsReplicated = true;

            OnReplicated?.Invoke();
        }
        public event Action OnReplicated;

        internal void Despawn()
        {
            NetworkLog.Info($"Despawning Entity {ID}");

            IsSpawned = false;

            OnDespawn?.Invoke();

            if (Origin is NetworkEntityOrigin.Prefab)
                Destroy();
        }
        public event Action OnDespawn;

        internal void Destroy()
        {
            Destroy(gameObject);
        }
        #endregion

        public NetworkEntity()
        {
            Behaviours = new BehavioursProperty(this);
        }

#if UNITY_EDITOR
        [CustomEditor(typeof(NetworkEntity))]
        class Inspector : Editor
        {
            static Lazy<GUIStyle> InformationLabelStyle = new(() =>
            {
                return new GUIStyle(EditorStyles.label)
                {
                    normal =
                    {
                        textColor = Color.grey
                    },
                    hover =
                    {
                        textColor = Color.grey
                    },
                    active =
                    {
                        textColor = Color.grey
                    },
                    focused =
                    {
                        textColor = Color.grey
                    }
                };
            });

            public override void OnInspectorGUI()
            {
                base.OnInspectorGUI();

                DisplayInfo();
                DisplayToolbar();
            }

            void DisplayInfo()
            {
                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Information", EditorStyles.boldLabel);

                if (Application.isPlaying is false)
                {
                    DrawInfoField("Is Network Running", false);
                    return;
                }

                if (serializedObject.isEditingMultipleObjects)
                    return;

                var target = base.target as NetworkEntity;

                DrawInfoField("Is Spawned", target.IsSpawned);
                DrawInfoField("Is Replicated", target.IsReplicated);

                if (target.IsSpawned)
                {
                    DrawInfoField("ID", target.ID.Value);
                    DrawInfoField("Owner", target.Owner);
                    DrawInfoField("Origin", target.Origin);
                    DrawInfoField("Authority", target.Authority);
                    DrawInfoField("Resource", target.Resource);
                    DrawInfoField("Transfer Token", target.TransferToken);
                }
            }
            static void DrawInfoField(string title, object value)
            {
                EditorGUILayout.LabelField(title, value.ToString(), InformationLabelStyle.Value);
            }

            void DisplayToolbar()
            {
                EditorGUILayout.Space();

                if (Application.isPlaying is false)
                    return;

                if (serializedObject.isEditingMultipleObjects)
                    return;

                var target = base.target as NetworkEntity;

                EditorGUILayout.BeginHorizontal();
                {
                    //Take Ownership
                    using (new EditorGUI.DisabledGroupScope(target.IsLocal))
                    {
                        if (GUILayout.Button("Take Ownership"))
                        {
                            target.Room.Entities.TakeOwnership(target);
                        }
                    }

                    //Despawn
                    using (new EditorGUI.DisabledGroupScope(target.IsRemote))
                    {
                        if (GUILayout.Button("Despawn"))
                        {
                            target.Room.Entities.Despawn(target);
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }
#endif
    }
}
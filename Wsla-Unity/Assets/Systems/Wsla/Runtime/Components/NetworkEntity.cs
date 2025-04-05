using System;
using System.Collections.Generic;

using Toolbox;

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Wsla.Unity
{
    public sealed class NetworkEntity : MonoBehaviour, IPreCache
    {
        public NetworkEntityID ID { get; private set; }
        public NetworkEntityOrigin Origin { get; private set; }

        public NetworkEntityResource Resource { get; private set; }
        internal void SetResource(NetworkEntityResource value)
        {
            Resource = Resource;
        }

        public NetworkClient Owner { get; private set; }
        internal int OwnerRegistration;

        /// <summary>
        /// Are you the owner of this entity? opposite of <see cref="IsRemote"/>
        /// </summary>
        public bool IsMine => IsSpawned && Owner.IsLocal;

        /// <summary>
        /// Are you NOT the owner of this entity? opposite of <see cref="IsMine"/>
        /// </summary>
        public bool IsRemote => IsSpawned && Owner.IsRemote;

        internal void AssignOwner(NetworkClient target)
        {
            Owner = target;
        }

        public event TransferOwnerDelegate OnTransferOwner;
        public delegate void TransferOwnerDelegate(NetworkClient owner);
        internal void TransferOwner(NetworkClient target)
        {
            AssignOwner(target);

            OnTransferOwner?.Invoke(target);
        }

        [field: SerializeField]
        public NetworkEntityAuthorityMode Authority { get; internal set; }

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
            /// <inheritdoc cref="NetworkEntity.IsMine"/>
            /// </summary>
            public bool IsMine => Entity.IsMine;

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
                public bool TryGet(NetworkRpcID id, out BaseRpcBind bind)
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

                readonly Dictionary<string, BaseRpcBind> Names;

                NetworkRpcID Index;

                public RpcInvocationBuilder Invoke(string name)
                {
                    if (Names.TryGetValue(name, out var bind) is false)
                        throw new ArgumentException($"No RPC Bind Names {name} Found on {Behaviour}");

                    return Invoke(bind);
                }
                public RpcInvocationBuilder Invoke(BaseRpcBind bind) => new RpcInvocationBuilder(bind);

                void Register(BaseRpcBind bind)
                {
                    if (NetworkRpcID.Increment(ref Index, out var id) is false)
                        throw new InvalidOperationException($"Network RPCs Count Exceeded on {Behaviour.Script}, Max Count is {NetworkRpcID.MaxValue}");

                    bind.Set(id, Behaviour);

                    List.Add(bind);
                }

                public RpcProperty(Behaviour Behaviour)
                {
                    this.Behaviour = Behaviour;

                    Collector.Clear();

                    //Attributed Registration
                    if (Behaviour.Contract is IRemoteSyncMembers members)
                    {
                        members.RegisterRPCs(Collector);

                        Names = new Dictionary<string, BaseRpcBind>(Collector.Count);

                        foreach (var bind in Collector)
                        {
                            var name = bind.GetName();

                            if (Names.TryAdd(name, bind) is false)
                                throw new InvalidOperationException($"Duplicate RPCs by the Name of {name} Found on {Behaviour}, RPC Overloading is not Supported");
                        }
                    }

                    //Custom Registration
                    if (Behaviour.Script is IRegisterCustomRPCs custom)
                    {
                        custom.RegisterRPCs(Collector);
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
                public bool TryGet(NetworkVariableID id, out NetworkVariable variable)
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

                NetworkVariableID Index;
                void Register(NetworkVariable variable)
                {
                    if (NetworkVariableID.Increment(ref Index, out var id) is false)
                        throw new InvalidOperationException($"Network Variables Count Exceeded on {Behaviour.Script}, Max Count is {NetworkVariableID.MaxValue}");

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
                        custom.RegisterVariables(Collector);
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
                    using (new EditorGUI.DisabledGroupScope(target.IsMine))
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
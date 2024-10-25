using System;
using System.Collections.Generic;

using LiteNetLib;

using Toolbox;

using UnityEngine;

using Wsla.Serialization;

namespace Wsla.Unity
{
    public sealed class NetworkEntity : MonoBehaviour, IPreCache
    {
        public NetworkEntityID ID { get; private set; }
        public NetworkEntitySource Source { get; private set; }
        public NetworkEntityResource Resource { get; private set; }
        internal void SetProperties(NetworkEntityID ID, NetworkEntitySource Source, NetworkEntityResource Resource)
        {
            this.ID = ID;
            this.Source = Source;
            this.Resource = Resource;
        }

        public RoomInstance Room { get; private set; }
        internal void Set(RoomInstance reference)
        {
            this.Room = reference;
        }

        public bool IsSpawned { get; private set; }
        internal void Spawn()
        {
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

        public void ReadState(ref NetPacketReader reader)
        {
            ID = NetworkSerializer.ReadValue<NetworkEntityID, NetPacketReader>(ref reader);
            Source = NetworkSerializer.ReadValue<NetworkEntitySource, NetPacketReader>(ref reader);
            Resource = NetworkSerializer.ReadValue<NetworkEntityResource, NetPacketReader>(ref reader);
        }

        [field: SerializeField]
        public BehavioursProperty Behaviours { get; private set; }
        [Serializable]
        public class BehavioursProperty
        {
            [field: SerializeField]
            public MonoBehaviour[] Components { get; private set; }

            public Behaviour[] Behaviours { get; private set; }
            public bool TryGet(NetworkEntityID id, out Behaviour behaviour)
            {
                var index = id.Value;

                if (Behaviours.IsValidIndex(index) is false)
                {
                    behaviour = default;
                    return false;
                }

                behaviour = Behaviours[index];
                return true;
            }

            internal void PreCache()
            {
                var collection = Entity.GetComponentsInChildren<INetworkBehaviour>(true);
                Components = Array.ConvertAll(collection, x => x as MonoBehaviour);
            }

            internal void Create()
            {
                Behaviours = new Behaviour[Components.Length];

                for (byte i = 0; i < Components.Length; i++)
                {
                    var id = new NetworkBehaviourID(i);

                    Behaviours[i] = new Behaviour(Entity, id, Components[i]);
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
                public RpcInvocationBuilder Invoke(BaseRpcBind bind) => new RpcInvocationBuilder(bind, Behaviour.Entity.Room);

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

                    //Attributed Registeration
                    if (Behaviour.Contract is IRemoteSyncMembers members)
                    {
                        members.RegisterRPCs(Collector);

                        Names = new Dictionary<string, BaseRpcBind>(Collector.Count);

                        foreach (var bind in Collector)
                        {
                            var name = bind.GetName();

                            if (Names.TryAdd(name, bind) is false)
                                throw new InvalidOperationException($"Dupliate RPCs by the Name of {name} Found on {Behaviour}, RPC Overloading is not Supported");
                        }
                    }

                    //Custom Registeration
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

            public override string ToString() => Script.ToString();

            public Behaviour(NetworkEntity Entity, NetworkBehaviourID ID, MonoBehaviour Script)
            {
                this.Entity = Entity;

                this.ID = ID;
                this.Script = Script;

                Contract = Script as INetworkBehaviour;

                RPC = new RpcProperty(this);

                Contract.Set(this);
            }
        }

        public void PreCache()
        {
            Behaviours.PreCache();
        }

        void Awake()
        {
            Behaviours.Create();
        }

        public NetworkEntity()
        {
            Behaviours = new BehavioursProperty(this);
        }

        //Static Utility
        public static void ReadProperties(NetPacketReader reader, out NetworkEntitySource source, out NetworkEntityResource resource, out NetworkEntityID id)
        {
            source = NetworkSerializer.ReadValue<NetworkEntitySource, NetPacketReader>(ref reader);
            resource = NetworkSerializer.ReadValue<NetworkEntityResource, NetPacketReader>(ref reader);
            id = NetworkSerializer.ReadValue<NetworkEntityID, NetPacketReader>(ref reader);
        }
    }
}
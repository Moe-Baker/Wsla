using System;

using UnityEngine;

using Wsla.Serialization;
using System.Linq;
using System.Collections.Generic;
using Toolbox;
using UnityEngine.UIElements;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.UIElements;
#endif

namespace Wsla.Unity
{
    [RequireComponent(typeof(Animator))]
    public partial class NetworkAnimator : NetworkBehaviour, IPreCache
    {
        [field: SerializeField]
        public TickSliceRate TickSlice { get; private set; } = new(1);

        public Animator Component { get; private set; }

        [SerializeField]
        ParametersProperty Parameters;
        [Serializable]
        public struct ParametersProperty
        {
            [SerializeField]
            internal BoolParameter[] Bools;
            [Serializable]
            public struct BoolParameter : IParameter<bool>
            {
                [field: SerializeField]
                public string Name { get; internal set; }

                public int Hash { get; internal set; }

                public bool Value { get; internal set; }

                public bool Dirty { get; internal set; }
                public void MarkDirty()
                {
                    Dirty = true;
                }

                NetworkAnimator Animator;
                public void Init(NetworkAnimator Animator)
                {
                    this.Animator = Animator;

                    Hash = Animator.StringToHash(Name);
                }

                public void Change(bool input)
                {
                    if (input == Value)
                        return;

                    Dirty = Animator.Parameters.Dirty = true;

                    Value = input;

                    Apply(Value);
                }

                internal void WriteState(ref BitStream bits)
                {
                    bits.Write(Value);
                }
                internal void ReadState(ref BitStream bits)
                {
                    Value = bits.Read();

                    Apply(Value);
                }

                public void Apply(bool value) => Animator.Component.SetBool(Hash, value);

                public BoolParameter(string Name) : this()
                {
                    this.Name = Name;
                }
            }

            [SerializeField]
            internal IntegerParameter[] Integers;
            [Serializable]
            public struct IntegerParameter : IParameter<int>
            {
                [field: SerializeField]
                public string Name { get; internal set; }

                [field: SerializeField]
                public OptionalValue<IntegerQuantizationParameters> Quantization { get; private set; }

                public int Hash { get; internal set; }

                public int Value { get; internal set; }

                public bool Dirty { get; internal set; }
                public void MarkDirty()
                {
                    Dirty = true;
                }

                NetworkAnimator Animator;
                public void Init(NetworkAnimator Animator)
                {
                    this.Animator = Animator;

                    Hash = Animator.StringToHash(Name);
                }

                public void Change(int input)
                {
                    if (input == Value)
                        return;

                    Dirty = Animator.Parameters.Dirty = true;

                    Value = input;

                    Apply(Value);
                }

                internal void WriteState(ref BinarySource stream)
                {
                    if (Quantization.Enabled)
                        Quantize.Integer.Serialize(ref stream, Value, Quantization.Value);
                    else
                        NetworkSerializer.WriteValue(Value, ref stream);
                }
                internal void ReadState(ref BinarySource stream)
                {
                    if (Quantization.Enabled)
                        Value = Quantize.Integer.Deserialize(ref stream, Quantization.Value);
                    else
                        Value = NetworkSerializer.ReadValue<int>(ref stream);

                    Apply(Value);
                }

                public void Apply(int value) => Animator.Component.SetInteger(Hash, value);

                public IntegerParameter(string Name) : this()
                {
                    this.Name = Name;
                }
            }

            [SerializeField]
            internal FloatParameter[] Floats;
            [Serializable]
            public struct FloatParameter : IParameter<float>
            {
                [field: SerializeField]
                public string Name { get; internal set; }

                [field: SerializeField]
                public OptionalValue<FloatQuantizationParameters> Quantization { get; private set; }

                public int Hash { get; internal set; }

                public float Value { get; internal set; }

                public bool Dirty { get; internal set; }
                public void MarkDirty()
                {
                    Dirty = true;
                }

                NetworkAnimator Animator;
                public void Init(NetworkAnimator Animator)
                {
                    this.Animator = Animator;

                    Hash = Animator.StringToHash(Name);
                }

                public void Change(float input)
                {
                    if (Mathf.Approximately(input, Value))
                        return;

                    Dirty = Animator.Parameters.Dirty = true;

                    Value = input;

                    Apply(Value);
                }

                internal void WriteState(ref BinarySource stream)
                {
                    if (Quantization.Enabled)
                        Quantize.Float.Serialize(ref stream, Value, Quantization.Value);
                    else
                        NetworkSerializer.WriteValue(Value, ref stream);
                }
                internal void ReadState(ref BinarySource stream)
                {
                    if (Quantization.Enabled)
                        Value = Quantize.Float.Deserialize(ref stream, Quantization.Value);
                    else
                        Value = NetworkSerializer.ReadValue<int>(ref stream);

                    Apply(Value);
                }

                public void Apply(float value) => Animator.Component.SetFloat(Hash, value);

                public FloatParameter(string Name) : this()
                {
                    this.Name = Name;
                }
            }

            [SerializeField]
            internal TriggerParameter[] Triggers;
            [Serializable]
            public struct TriggerParameter : IParameter<bool>
            {
                [field: SerializeField]
                public string Name { get; internal set; }

                public int Hash { get; internal set; }

                public bool Dirty { get; internal set; }
                public void MarkDirty()
                {
                    Dirty = true;
                }

                public bool Value => Dirty;

                NetworkAnimator Animator;
                public void Init(NetworkAnimator Animator)
                {
                    this.Animator = Animator;

                    Hash = Animator.StringToHash(Name);
                }

                public void Change()
                {
                    Dirty = Animator.Parameters.Dirty = true;

                    Apply();
                }

                public void Apply() => Animator.Component.SetTrigger(Hash);

                public TriggerParameter(string Name) : this()
                {
                    this.Name = Name;
                }
            }

            public bool Dirty { get; private set; }
            public void MarkDirty()
            {
                Dirty = true;

                for (int i = 0; i < Bools.Length; i++)
                    Bools[i].MarkDirty();

                for (int i = 0; i < Integers.Length; i++)
                    Integers[i].MarkDirty();

                for (int i = 0; i < Floats.Length; i++)
                    Floats[i].MarkDirty();
            }

            public int Count => Bools.Length + Integers.Length + Floats.Length + Triggers.Length;

            public interface IParameter<T>
            {
                string Name { get; }
                int Hash { get; }
                T Value { get; }

                bool Dirty { get; }
                void MarkDirty();

                void Init(NetworkAnimator Animator);
            }

#if UNITY_EDITOR
            internal void Refresh(AnimatorController controller)
            {
                var parameters = controller.parameters;

                (int Bools, int Integers, int Floats, int Triggers) Count = (0, 0, 0, 0);

                for (int i = 0; i < parameters.Length; i++)
                {
                    switch (parameters[i].type)
                    {
                        case AnimatorControllerParameterType.Bool:
                            Count.Bools += 1;
                            break;

                        case AnimatorControllerParameterType.Int:
                            Count.Integers += 1;
                            break;

                        case AnimatorControllerParameterType.Float:
                            Count.Floats += 1;
                            break;

                        case AnimatorControllerParameterType.Trigger:
                            Count.Triggers += 1;
                            break;
                    }
                }

                (Dictionary<string, BoolParameter> Bools,
                    Dictionary<string, IntegerParameter> Integers,
                    Dictionary<string, FloatParameter> Floats,
                    Dictionary<string, TriggerParameter> Triggers)
                dictionary =
                (Bools.ToDictionary(x => x.Name),
                    Integers.ToDictionary(x => x.Name),
                    Floats.ToDictionary(x => x.Name),
                    Triggers.ToDictionary(x => x.Name));

                Bools = new BoolParameter[Count.Bools];
                Integers = new IntegerParameter[Count.Integers];
                Floats = new FloatParameter[Count.Floats];
                Triggers = new TriggerParameter[Count.Triggers];

                for (int i = parameters.Length - 1; i >= 0; i--)
                {
                    switch (parameters[i].type)
                    {
                        case AnimatorControllerParameterType.Bool:
                        {
                            Count.Bools -= 1;

                            if (dictionary.Bools.TryGetValue(parameters[i].name, out var parameter) is false)
                                parameter = new(parameters[i].name);

                            Bools[Count.Bools] = parameter;
                        }
                        break;

                        case AnimatorControllerParameterType.Int:
                        {
                            Count.Integers -= 1;

                            if (dictionary.Integers.TryGetValue(parameters[i].name, out var parameter) is false)
                                parameter = new(parameters[i].name);

                            Integers[Count.Integers] = parameter;
                        }
                        break;

                        case AnimatorControllerParameterType.Float:
                        {
                            Count.Floats -= 1;

                            if (dictionary.Floats.TryGetValue(parameters[i].name, out var parameter) is false)
                                parameter = new(parameters[i].name);

                            Floats[Count.Floats] = parameter;
                        }
                        break;

                        case AnimatorControllerParameterType.Trigger:
                        {
                            Count.Triggers -= 1;

                            if (dictionary.Triggers.TryGetValue(parameters[i].name, out var parameter) is false)
                                parameter = new(parameters[i].name);

                            Triggers[Count.Triggers] = parameter;
                        }
                        break;
                    }
                }
            }
#endif

            internal void Init()
            {
                for (int i = 0; i < Bools.Length; i++)
                    Bools[i].Init(Animator);

                for (int i = 0; i < Integers.Length; i++)
                    Integers[i].Init(Animator);

                for (int i = 0; i < Floats.Length; i++)
                    Floats[i].Init(Animator);

                for (int i = 0; i < Triggers.Length; i++)
                    Triggers[i].Init(Animator);
            }

            internal void CollectDirtyMask(ref BitStream mask)
            {
                for (int i = 0; i < Bools.Length; i++)
                {
                    mask.Write(Bools[i].Dirty);
                    Bools[i].Dirty = false;
                }

                for (int i = 0; i < Integers.Length; i++)
                {
                    mask.Write(Integers[i].Dirty);
                    Integers[i].Dirty = false;
                }

                for (int i = 0; i < Floats.Length; i++)
                {
                    mask.Write(Floats[i].Dirty);
                    Floats[i].Dirty = false;
                }

                for (int i = 0; i < Triggers.Length; i++)
                {
                    mask.Write(Triggers[i].Dirty);
                    Triggers[i].Dirty = false;
                }

                Dirty = false;
            }

            internal void WriteState(ref BinarySource stream, ref BitStream mask)
            {
                //Bools
                {
                    var bytes = BitStream.BitsToBytes(Bools.Length);
                    var buffer = stream.AllocateSpan(bytes);
                    var bits = new BitStream(buffer);

                    WriteBoolsState(ref bits, ref mask);
                }

                //Integers
                WriteIntegersState(ref stream, ref mask);

                //Floats
                WriteFloatsState(ref stream, ref mask);
            }
            internal void ReadState(ref BinarySource stream, ref BitStream mask)
            {
                //Bools
                {
                    var bytes = BitStream.BitsToBytes(Bools.Length);
                    var buffer = stream.ReadSpan(bytes);
                    var bits = new BitStream(buffer);

                    ReadBoolsState(ref bits, ref mask);
                }

                //Integers
                ReadIntegersState(ref stream, ref mask);

                //Floats
                ReadFloatsState(ref stream, ref mask);

                //Triggers
                ApplyTriggersInvocation(ref mask);
            }

            void WriteBoolsState(ref BitStream stream, ref BitStream mask)
            {
                for (int i = 0; i < Bools.Length; i++)
                {
                    var dirty = mask.Read();
                    if (dirty is false)
                        continue;

                    Bools[i].WriteState(ref stream);
                }
            }
            void ReadBoolsState(ref BitStream stream, ref BitStream mask)
            {
                for (int i = 0; i < Bools.Length; i++)
                {
                    var dirty = mask.Read();
                    if (dirty is false)
                        continue;

                    Bools[i].ReadState(ref stream);
                }
            }

            void WriteIntegersState(ref BinarySource stream, ref BitStream mask)
            {
                for (int i = 0; i < Integers.Length; i++)
                {
                    var dirty = mask.Read();
                    if (dirty is false)
                        continue;

                    Integers[i].WriteState(ref stream);
                }
            }
            void ReadIntegersState(ref BinarySource stream, ref BitStream mask)
            {
                for (int i = 0; i < Integers.Length; i++)
                {
                    var dirty = mask.Read();
                    if (dirty is false)
                        continue;

                    Integers[i].ReadState(ref stream);
                }
            }

            void WriteFloatsState(ref BinarySource stream, ref BitStream mask)
            {
                for (int i = 0; i < Floats.Length; i++)
                {
                    var dirty = mask.Read();
                    if (dirty is false)
                        continue;

                    Floats[i].WriteState(ref stream);
                }
            }
            void ReadFloatsState(ref BinarySource stream, ref BitStream mask)
            {
                for (int i = 0; i < Floats.Length; i++)
                {
                    var dirty = mask.Read();
                    if (dirty is false)
                        continue;

                    Floats[i].ReadState(ref stream);
                }
            }

            void ApplyTriggersInvocation(ref BitStream mask)
            {
                for (int i = 0; i < Triggers.Length; i++)
                {
                    var dirty = mask.Read();
                    if (dirty is false)
                        continue;

                    Triggers[i].Apply();
                }
            }

            NetworkAnimator Animator;
            public ParametersProperty(NetworkAnimator Animator)
            {
                this.Animator = Animator;

                Bools = default;
                Integers = default;
                Floats = default;
                Triggers = default;

                Dirty = false;
            }
        }

        [SerializeField]
        LayersProperty Layers;
        [Serializable]
        public struct LayersProperty
        {
            [SerializeField]
            internal ElementProperty[] Collection;
            [Serializable]
            public struct ElementProperty
            {
                [SerializeField]
                internal string Name;

                int Index;

                internal float Weight;

                internal bool Dirty;
                public void MarkDirty()
                {
                    Dirty = true;
                }

                //8 bits in 0-1 range = 0.005 Precision
                const int Bits = 8;
                const float Min = 0f;
                const float Max = 1f;

                NetworkAnimator Animator;
                internal void Init(NetworkAnimator Animator, int Index)
                {
                    this.Animator = Animator;
                    this.Index = Index;
                }

                internal void Change(float Weight)
                {
                    this.Weight = Weight;
                    Dirty = Animator.Layers.Dirty = true;

                    Apply(Weight);
                }

                internal void WriteState(ref BinarySource stream)
                {
                    Quantize.Float.Serialize(ref stream, Weight, Min, Max, Bits);
                }
                internal void ReadState(ref BinarySource stream)
                {
                    Weight = Quantize.Float.Deserialize(ref stream, Min, Max, Bits);

                    Apply(Weight);
                }

                internal void Apply(float Weight)
                {
                    Animator.Component.SetLayerWeight(Index, Weight);
                }

                public ElementProperty(string Name) : this()
                {
                    this.Name = Name;
                }
            }

            public int Count => Collection.Length;

            internal bool Dirty { get; private set; }
            public void MarkDirty()
            {
                Dirty = true;

                for (int i = 0; i < Collection.Length; i++)
                    Collection[i].MarkDirty();
            }

#if UNITY_EDITOR
            internal void Refresh(AnimatorController controller)
            {
                var layers = controller.layers;

                var dictionary = Collection.ToDictionary(x => x.Name);

                Collection = new ElementProperty[layers.Length];

                for (int i = 0; i < Collection.Length; i++)
                {
                    if (dictionary.TryGetValue(layers[i].name, out var elemment) is false)
                        elemment = new ElementProperty(layers[i].name);

                    Collection[i] = elemment;
                }
            }
#endif
            internal void Init()
            {
                for (int i = 0; i < Collection.Length; i++)
                    Collection[i].Init(Animator, i);
            }

            internal void CollectDirtyMask(ref BitStream mask)
            {
                for (int i = 0; i < Collection.Length; i++)
                {
                    mask.Write(Collection[i].Dirty);
                    Collection[i].Dirty = false;
                }

                Dirty = false;
            }

            internal void WriteState(ref BinarySource stream, ref BitStream mask)
            {
                for (int i = 0; i < Collection.Length; i++)
                {
                    var dirty = mask.Read();
                    if (dirty is false)
                        continue;

                    Collection[i].WriteState(ref stream);
                }
            }
            internal void ReadState(ref BinarySource stream, ref BitStream mask)
            {
                for (int i = 0; i < Collection.Length; i++)
                {
                    var dirty = mask.Read();
                    if (dirty is false)
                        continue;

                    Collection[i].ReadState(ref stream);
                }
            }

            NetworkAnimator Animator;
            public LayersProperty(NetworkAnimator Animator)
            {
                this.Animator = Animator;

                Collection = default;

                Dirty = false;
            }
        }

        bool Dirty => Parameters.Dirty | Layers.Dirty;
        public void MarkDirty()
        {
            Parameters.MarkDirty();
            Layers.MarkDirty();
        }

        NetworkTickTimer TickTimer;

        void Awake()
        {
            Component = GetComponent<Animator>();

#if UNITY_EDITOR
            Refresh();
#endif
        }

#if UNITY_EDITOR
        public void PreCache() => Refresh();

        void Refresh()
        {
            Component = GetComponent<Animator>();

            var controller = ConvertRuntimeController(Component.runtimeAnimatorController);

            if (controller is null)
                throw new InvalidOperationException($"No Animator Controller Found on {this}");

            Parameters.Refresh(controller);
            Layers.Refresh(controller);
        }
#endif

        public override void Set(NetworkEntity.Behaviour reference)
        {
            base.Set(reference);

            TickTimer = new NetworkTickTimer(TickSlice);
            TickTimer.OnTick += TickCallback;

            Parameters.Init();
            Layers.Init();

            Network.Entity.OnSpawn += SpawnCallback;
            Network.Entity.OnDespawn += DespawnCallback;
        }

        void SpawnCallback()
        {
            if (Network.IsLocal)
            {
                TickTimer.Start();
            }

            Network.Entity.OnGainedOwnership += GainedOwnershipCallback;
            Network.Entity.OnLostOwnership += LostOwnershipCallback;
        }
        void DespawnCallback()
        {
            TickTimer.Stop();
        }

        void GainedOwnershipCallback()
        {
            //Setup Tick Timer
            {
                TickTimer.SetTick(NetworkTickID.Zero + 1);
                TickTimer.Start();
            }

            //Replicate Current State
            {
                MarkDirty();
                WritePayload();
            }
        }
        void LostOwnershipCallback()
        {
            TickTimer.Stop();
        }

        void TickCallback(NetworkTickInfo info)
        {
            if (Dirty is false)
                return;

            WritePayload();
        }

        void WritePayload()
        {
            var stream = RPCs.Replicate.GetSourceStream();
            var source = BinarySource.From(stream);

            var changes = CollectDirtyMask(ref source);

            changes.Reset();

            Parameters.WriteState(ref source, ref changes);
            Layers.WriteState(ref source, ref changes);

            var binary = stream.PeekAllocatedMemory();

            RPCs.Replicate.Invoke(binary)
                .SetIgnoreLocal()
                .Broadcast();
        }

        [RPC]
        void Replicate(ref BinarySource source, RpcInfo info)
        {
            if (info.TryGetSender(out var sender) && sender != Network.Owner)
                return;

            var changes = AllocateChangesMask(ref source);

            Parameters.ReadState(ref source, ref changes);
            Layers.ReadState(ref source, ref changes);
        }

        BitStream CollectDirtyMask(ref BinarySource stream)
        {
            var mask = AllocateChangesMask(ref stream);

            Parameters.CollectDirtyMask(ref mask);
            Layers.CollectDirtyMask(ref mask);

            return mask;
        }

        BitStream AllocateChangesMask(ref BinarySource stream)
        {
            var length = BitStream.BitsToBytes(Parameters.Count + Layers.Count);

            var buffer = stream.AllocateSpan(length);

            return new BitStream(buffer);
        }

        #region Parameter Modifiers
        public NetworkAnimatorMemberIndex IndexBool(string name)
        {
            for (int i = 0; i < Parameters.Bools.Length; i++)
                if (Parameters.Bools[i].Name == name)
                    return new NetworkAnimatorMemberIndex(i);

            throw new ArgumentException($"No Bool Parameter Named {name} Found in {this}");
        }
        public bool GetBool(NetworkAnimatorMemberIndex index) => Parameters.Bools[index.Value].Value;
        public void SetBool(NetworkAnimatorMemberIndex index, bool value) => Parameters.Bools[index.Value].Change(value);

        public NetworkAnimatorMemberIndex IndexInteger(string name)
        {
            for (int i = 0; i < Parameters.Integers.Length; i++)
                if (Parameters.Integers[i].Name == name)
                    return new NetworkAnimatorMemberIndex(i);

            throw new ArgumentException($"No Integer Parameter Named {name} Found in {this}");
        }
        public int GetInteger(NetworkAnimatorMemberIndex index) => Parameters.Integers[index.Value].Value;
        public void SetInteger(NetworkAnimatorMemberIndex index, int value) => Parameters.Integers[index.Value].Change(value);

        public NetworkAnimatorMemberIndex IndexFloat(string name)
        {
            for (int i = 0; i < Parameters.Floats.Length; i++)
                if (Parameters.Floats[i].Name == name)
                    return new NetworkAnimatorMemberIndex(i);

            throw new ArgumentException($"No Float Parameter Named {name} Found in {this}");
        }
        public float GetFloat(NetworkAnimatorMemberIndex index) => Parameters.Floats[index.Value].Value;
        public void SetFloat(NetworkAnimatorMemberIndex index, float value) => Parameters.Floats[index.Value].Change(value);

        public NetworkAnimatorMemberIndex IndexTrigger(string name)
        {
            for (int i = 0; i < Parameters.Triggers.Length; i++)
                if (Parameters.Triggers[i].Name == name)
                    return new NetworkAnimatorMemberIndex(i);

            throw new ArgumentException($"No Trigger Parameter Named {name} Found in {this}");
        }
        public void SetTrigger(NetworkAnimatorMemberIndex index) => Parameters.Triggers[index.Value].Change();
        #endregion

        #region Layer Modifiers
        public int LayerCount => Layers.Count;

        public string GetLayerName(int index) => Layers.Collection[index].Name;
        public int IndexLayer(string name)
        {
            for (int i = 0; i < Layers.Collection.Length; i++)
                if (Layers.Collection[i].Name == name)
                    return i;

            throw new ArgumentException($"No Layer Named {name} Found in {this}");
        }

        public float GetLayerWeight(int index) => Layers.Collection[index].Weight;
        public void SetLayerWeight(int index, float value) => Layers.Collection[index].Change(value);
        #endregion

        int StringToHash(string value) => Animator.StringToHash(value);

        public NetworkAnimator()
        {
            Parameters = new ParametersProperty(this);
            Layers = new LayersProperty(this);
        }

#if UNITY_EDITOR
        AnimatorController ConvertRuntimeController(RuntimeAnimatorController controller)
        {
            var path = AssetDatabase.GetAssetPath(controller);
            return AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        }

        [CustomEditor(typeof(NetworkAnimator))]
        class Inspector : Editor
        {
            public override VisualElement CreateInspectorGUI()
            {
                return new Element(this);
            }

            class Element : VisualElement
            {
                public Element(Editor editor)
                {
                    //Inspector
                    {
                        InspectorElement.FillDefaultInspector(this, editor.serializedObject, editor);
                    }

                    //Refresh
                    {
                        var element = new Button(OnClick)
                        {
                            text = "Refresh"
                        };

                        void OnClick()
                        {
                            foreach (NetworkAnimator target in editor.serializedObject.targetObjects)
                                target.Refresh();
                        }

                        Add(element);
                    }
                }
            }
        }
#endif
    }

    public struct NetworkAnimatorMemberIndex
    {
        public int Value { get; }

        public NetworkAnimatorMemberIndex(int Value)
        {
            this.Value = Value;
        }
    }
}
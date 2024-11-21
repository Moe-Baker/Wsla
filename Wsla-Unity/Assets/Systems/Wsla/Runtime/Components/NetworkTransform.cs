using System;

using Toolbox;

using UnityEngine;

using Wsla.Serialization;

namespace Wsla.Unity
{
    public partial class NetworkTransform : NetworkBehaviour
    {
        [field: SerializeField]
        public TickSliceRate TickSlice { get; private set; } = new(1);

        NetworkTickTimer TickTimer;

        [SerializeField]
        MaskProperty Mask;
        [Serializable]
        public struct MaskProperty
        {
            [field: SerializeField]
            public Vector3Fields<bool> Position { get; private set; }

            [field: SerializeField]
            public bool Rotation { get; private set; }

            [field: SerializeField]
            public bool Scale { get; private set; }

            public ChangeFlags Value
            {
                get
                {
                    var change = ChangeFlags.None;

                    if (Position.X) change |= ChangeFlags.PositionX;
                    if (Position.Y) change |= ChangeFlags.PositionY;
                    if (Position.Z) change |= ChangeFlags.PositionZ;

                    if (Rotation) change |= ChangeFlags.Rotation;

                    if (Scale) change |= ChangeFlags.Scale;

                    return change;
                }
            }

            public MaskProperty(bool value) : this(value, value, value) { }
            public MaskProperty(bool Position, bool Rotation, bool Scale)
            {
                this.Position = new(Position);
                this.Rotation = Rotation;
                this.Scale = Scale;
            }
        }

        CoordinatesProperty Coordinates;
        [Serializable]
        public struct CoordinatesProperty
        {
            public Transform Context => Transform.transform;

            public Vector3 Position
            {
                get => Context.position;
                set => Context.position = value;
            }
            public Quaternion Rotation
            {
                get => Context.rotation;
                set => Context.rotation = value;
            }
            public Vector3 Scale
            {
                get => Context.localScale;
                set => Context.localScale = value;
            }

            public CoordinatesData Read() => new(Position, Rotation, Scale);
            public void Write(CoordinatesData data)
            {
                Position = data.Position;
                Rotation = data.Rotation;
                Scale = data.Scale;
            }

            NetworkTransform Transform;
            public CoordinatesProperty(NetworkTransform Transform)
            {
                this.Transform = Transform;
            }
        }
        public struct CoordinatesData
        {
            public Vector3 Position { get; }
            public Quaternion Rotation { get; }
            public Vector3 Scale { get; }

            public CoordinatesData(Vector3 Position, Quaternion Rotation, Vector3 Scale)
            {
                this.Position = Position;
                this.Rotation = Rotation;
                this.Scale = Scale;
            }

            public static CoordinatesData Lerp(CoordinatesData a, CoordinatesData b, float t)
            {
                var position = Vector3.Lerp(a.Position, b.Position, t);
                var rotation = Quaternion.Lerp(a.Rotation, b.Rotation, t);
                var scale = Vector3.Lerp(a.Scale, b.Scale, t);

                return new(position, rotation, scale);
            }
        }

        [SerializeField]
        QuantizationProperty Quantization;
        [Serializable]
        public struct QuantizationProperty
        {
            [field: SerializeField]
            public Vector3Fields<OptionalValue<FloatQuantizationParameters>> Position { get; private set; }

            [field: SerializeField]
            public bool Rotation { get; private set; }

            [field: SerializeField]
            public Vector3Fields<OptionalValue<FloatQuantizationParameters>> Scale { get; private set; }

            public static QuantizationProperty Default = new()
            {
                Rotation = true,
            };
        }

        [SerializeField]
        MotionDetectorProperty MotionDetector;
        [Serializable]
        public struct MotionDetectorProperty
        {
            [SerializeField]
            float Epsilon;

            MotionData LastSnapshot;

            ref MaskProperty Mask => ref Transform.Mask;

            public void Init()
            {
                var coordinates = Transform.Coordinates.Read();
                LastSnapshot = new(ChangeFlags.None, coordinates);
            }

            public MotionData Detect()
            {
                var change = ChangeFlags.None;

                var coordinates = Transform.Coordinates.Read();

                //Check Position
                {
                    if (Mask.Position.X && Mathf.Abs(LastSnapshot.Position.x - coordinates.Position.x) > Epsilon)
                        change |= ChangeFlags.PositionX;

                    if (Mask.Position.Y && Mathf.Abs(LastSnapshot.Position.y - coordinates.Position.y) > Epsilon)
                        change |= ChangeFlags.PositionY;

                    if (Mask.Position.Z && Mathf.Abs(LastSnapshot.Position.z - coordinates.Position.z) > Epsilon)
                        change |= ChangeFlags.PositionZ;
                }

                //Check Rotation
                {
                    if (Mask.Rotation && Quaternion.Angle(LastSnapshot.Rotation, coordinates.Rotation) > Epsilon)
                        change |= ChangeFlags.Rotation;
                }

                //Check Scale
                {
                    if (Mask.Scale && Vector3.Distance(LastSnapshot.Scale, coordinates.Scale) > Epsilon)
                        change |= ChangeFlags.Scale;
                }

                LastSnapshot = new MotionData(ChangeData.Calculate(LastSnapshot.Change.Current, change), coordinates);

                return LastSnapshot;
            }

            NetworkTransform Transform;
            public MotionDetectorProperty(NetworkTransform Transform)
            {
                this.Transform = Transform;

                LastSnapshot = default;

                Epsilon = 0.01f;
            }
        }
        public struct MotionData
        {
            public ChangeData Change { get; }

            public CoordinatesData Coordinates { get; }

            public Vector3 Position => Coordinates.Position;
            public Quaternion Rotation => Coordinates.Rotation;
            public Vector3 Scale => Coordinates.Scale;

            public MotionData(ChangeFlags Change, CoordinatesData Coordinates) : this(new ChangeData(Change), Coordinates) { }
            public MotionData(ChangeData Change, CoordinatesData Coordinates)
            {
                this.Change = Change;
                this.Coordinates = Coordinates;
            }
        }
        public struct ChangeData
        {
            public ChangeFlags Current { get; }
            public ChangeFlags Difference { get; }

            public bool Stopped { get; }

            public bool IsNone => Current is ChangeFlags.None && Difference is ChangeFlags.None;

            public ChangeData(ChangeFlags value) : this(value, value) { }
            public ChangeData(ChangeFlags Current, ChangeFlags Difference)
            {
                this.Current = Current;
                this.Difference = Difference;

                Stopped = Current == ChangeFlags.None && Difference != ChangeFlags.None;
            }

            public static ChangeData Calculate(ChangeFlags previous, ChangeFlags current)
            {
                var difference = (previous ^ current) & ~current;
                return new(current, difference);
            }
        }

        [SerializeField]
        SnapshotInterpolationProperty SnapshotInterpolation;
        [Serializable]
        public struct SnapshotInterpolationProperty
        {
            [SerializeField, Range(1, MaxBufferSize)]
            byte BufferSize;

            const int MaxBufferSize = 5;

            SnapshotInterpolator Interpolator;
            [Serializable]
            class SnapshotInterpolator : SnapshotInterpolator<SnapshotData, CoordinatesData>
            {
                public override CoordinatesData Lerp(SnapshotData a, SnapshotData b, float t) => CoordinatesData.Lerp(a.Value, b.Value, t);

                protected override void Alter(int index, SnapshotData replacement)
                {
                    var change = replacement.Change & ChangeFlags.Coordinates;

                    for (/* No Assignment*/ ; index < Collection.Count; index++)
                    {
                        //Remove duplicate changes
                        change &= ~Collection[index].Change;

                        if (change is ChangeFlags.None)
                            return;
                    }

                    //Modify Last Snapshot
                    {
                        ref var last = ref Collection[^1];

                        var position = last.Value.Position;
                        var rotation = last.Value.Rotation;
                        var scale = last.Value.Scale;

                        //Position
                        {
                            if (change.HasFlag(ChangeFlags.PositionX)) position.x = replacement.Value.Position.x;
                            if (change.HasFlag(ChangeFlags.PositionY)) position.y = replacement.Value.Position.y;
                            if (change.HasFlag(ChangeFlags.PositionZ)) position.z = replacement.Value.Position.z;
                        }

                        //Rotation
                        if (change.HasFlag(ChangeFlags.Rotation)) rotation = replacement.Value.Rotation;

                        //Scale
                        if (change.HasFlag(ChangeFlags.Scale)) scale = replacement.Value.Scale;

                        change |= last.Change;

                        last = new(last.Tick, last.Time, change, new(position, rotation, scale));
                    }
                }

                public override SnapshotData Fill(CoordinatesData value, NetworkTickID tick, double time)
                {
                    return new SnapshotData(tick, time, ChangeFlags.None, value);
                }
            }

            internal void Init()
            {
                Interpolator = new();
                Interpolator.Init(BufferSize, Transform.TickTimer);
            }

            internal CoordinatesData GetOrigin()
            {
                if (Interpolator != null && Interpolator.TryGetLast(out var snapshot))
                    return snapshot.Value;

                return Transform.Coordinates.Read();
            }

            internal void Submit(SnapshotData snapshot)
            {
                Interpolator.Submit(snapshot);
            }

            public void Step()
            {
                if (Interpolator.Step(Time.unscaledDeltaTime, out var coordinates) is false)
                    return;

                Transform.Coordinates.Write(coordinates);
            }

            NetworkTransform Transform;
            public SnapshotInterpolationProperty(NetworkTransform Transform)
            {
                this.Transform = Transform;

                BufferSize = 3;

                Interpolator = default;
            }
        }
        [Serializable]
        public struct SnapshotData : ISnapshot<SnapshotData, CoordinatesData>
        {
            public NetworkTickID Tick { get; set; }
            public double Time { get; set; }

            public ChangeFlags Change { get; set; }
            public bool Stop => Change.HasFlag(ChangeFlags.Stop);

            public bool IsPredicted => Change is ChangeFlags.None;

            public CoordinatesData Value { get; set; }

            public SnapshotData(NetworkTickID Tick, double Time, ChangeFlags Change, CoordinatesData Value)
            {
                this.Tick = Tick;
                this.Time = Time;
                this.Change = Change;
                this.Value = Value;
            }
        }

        public override void Set(NetworkEntity.Behaviour reference)
        {
            base.Set(reference);

            TickTimer = new NetworkTickTimer(TickSlice);

            Network.OnSpawn += SpawnCallback;
            Network.OnDespawn += DespawnCallback;
        }

        void SpawnCallback()
        {
            MotionDetector.Init();
            SnapshotInterpolation.Init();

            if (Network.Entity.IsMine)
            {
                TickTimer.Start();
                TickTimer.OnTick += TickCallback;
            }
        }
        void DespawnCallback()
        {
            if (TickTimer != null)
            {
                TickTimer.Stop();
                TickTimer.OnTick -= TickCallback;
            }
        }

        void TickCallback(NetworkTickInfo info)
        {
            if (Network.Entity.IsRemote)
                return;

            var motion = MotionDetector.Detect();

            if (motion.Change.IsNone)
                return;

            //Replicate
            {
                var invocation = Network.RPC.Invoke(nameof(Replicate))
                    .SetIgnoreLocal()
                    .GetPayloadWriter(out var writer);

                var tick = info.GetTick();
                WritePayload(writer, tick, motion);

                if (motion.Change.Stopped)
                {
                    invocation.SetBufferMode();
                    invocation.SetDelivery(RemoteSyncDelivery.ReliableUnordered);
                }
                else
                {
                    invocation.SetDelivery(RemoteSyncDelivery.Unreliable);
                }

                invocation.Broadcast();
            }
        }

        void WritePayload(INetworkStream stream, NetworkTickID tick, MotionData motion)
        {
            NetworkSerializer.WriteValue(tick, stream);

            var change = motion.Change.Current;

            if (motion.Change.Stopped) //Write Entire Transform When Movement Stops
                change |= Mask.Value | ChangeFlags.Stop;

            NetworkSerializer.WriteValue(change, stream);

            //Position
            {
                if (change.HasFlag(ChangeFlags.PositionX))
                {
                    if (Quantization.Position.X.Enabled)
                        Quantize.Float.Serialize(stream, motion.Position.x, Quantization.Position.X.Value);
                    else
                        NetworkSerializer.WriteValue(motion.Position.x, stream);
                }

                if (change.HasFlag(ChangeFlags.PositionY))
                {
                    if (Quantization.Position.Y.Enabled)
                        Quantize.Float.Serialize(stream, motion.Position.y, Quantization.Position.Y.Value);
                    else
                        NetworkSerializer.WriteValue(motion.Position.y, stream);
                }

                if (change.HasFlag(ChangeFlags.PositionZ))
                {
                    if (Quantization.Position.Z.Enabled)
                        Quantize.Float.Serialize(stream, motion.Position.z, Quantization.Position.Z.Value);
                    else
                        NetworkSerializer.WriteValue(motion.Position.z, stream);
                }
            }

            //Rotation
            if (change.HasFlag(ChangeFlags.Rotation))
            {
                if (Quantization.Rotation)
                    Quantize.Rotation.Serialize(stream, motion.Rotation);
                else
                    NetworkSerializer.WriteValue(motion.Rotation, stream);
            }

            //Scale
            if (change.HasFlag(ChangeFlags.Scale))
            {
                //X
                {
                    if (Quantization.Scale.X.Enabled)
                        Quantize.Float.Serialize(stream, motion.Scale.x, Quantization.Scale.X.Value);
                    else
                        NetworkSerializer.WriteValue(motion.Scale.x, stream);
                }

                //Y
                {
                    if (Quantization.Scale.Y.Enabled)
                        Quantize.Float.Serialize(stream, motion.Scale.y, Quantization.Scale.Y.Value);
                    else
                        NetworkSerializer.WriteValue(motion.Scale.y, stream);
                }

                //Z
                {
                    if (Quantization.Scale.Z.Enabled)
                        Quantize.Float.Serialize(stream, motion.Scale.z, Quantization.Scale.Z.Value);
                    else
                        NetworkSerializer.WriteValue(motion.Scale.z, stream);
                }
            }
        }
        SnapshotData ReadPayload(INetworkStream stream, CoordinatesData origin)
        {
            NetworkSerializer.ReadValue(stream, out NetworkTickID tick);

            NetworkSerializer.ReadValue(stream, out ChangeFlags change);

            var position = origin.Position;
            var rotation = origin.Rotation;
            var scale = origin.Scale;

            //Position
            {
                if (change.HasFlag(ChangeFlags.PositionX))
                {
                    if (Quantization.Position.X.Enabled)
                        position.x = Quantize.Float.Deserialize(stream, Quantization.Position.X.Value);
                    else
                        NetworkSerializer.ReadValue(stream, out position.x);
                }

                if (change.HasFlag(ChangeFlags.PositionY))
                {
                    if (Quantization.Position.Y.Enabled)
                        position.y = Quantize.Float.Deserialize(stream, Quantization.Position.Y.Value);
                    else
                        NetworkSerializer.ReadValue(stream, out position.y);
                }

                if (change.HasFlag(ChangeFlags.PositionZ))
                {
                    if (Quantization.Position.Z.Enabled)
                        position.z = Quantize.Float.Deserialize(stream, Quantization.Position.Z.Value);
                    else
                        NetworkSerializer.ReadValue(stream, out position.z);
                }
            }

            //Rotation
            if (change.HasFlag(ChangeFlags.Rotation))
            {
                if (Quantization.Rotation)
                    rotation = Quantize.Rotation.Deserialize(stream);
                else
                    NetworkSerializer.ReadValue(stream, out rotation);
            }

            //Scale
            if (change.HasFlag(ChangeFlags.Scale))
            {
                //X
                {
                    if (Quantization.Scale.X.Enabled)
                        scale.x = Quantize.Float.Deserialize(stream, Quantization.Scale.X.Value);
                    else
                        NetworkSerializer.ReadValue(stream, out scale.x);
                }

                //Y
                {
                    if (Quantization.Scale.Y.Enabled)
                        scale.y = Quantize.Float.Deserialize(stream, Quantization.Scale.Y.Value);
                    else
                        NetworkSerializer.ReadValue(stream, out scale.y);
                }

                //Z
                {
                    if (Quantization.Scale.Z.Enabled)
                        scale.z = Quantize.Float.Deserialize(stream, Quantization.Scale.Z.Value);
                    else
                        NetworkSerializer.ReadValue(stream, out scale.z);
                }
            }

            var time = TickTimer.CalculateTime(tick);

            return new(tick, time, change, new(position, rotation, scale));
        }

        void Update()
        {
            if (Network.Entity.IsRemote)
                SnapshotInterpolation.Step();
        }

        [RPC]
        void Replicate(INetworkStream stream, RpcInfo info)
        {
            var origin = SnapshotInterpolation.GetOrigin();

            var snapshot = ReadPayload(stream, origin);

            if (info.IsBuffered)
            {
                Coordinates.Write(snapshot.Value);
            }
            else
            {
                SnapshotInterpolation.Submit(snapshot);
            }
        }

        public NetworkTransform()
        {
            Mask = new MaskProperty(true);
            Coordinates = new CoordinatesProperty(this);
            MotionDetector = new MotionDetectorProperty(this);
            SnapshotInterpolation = new SnapshotInterpolationProperty(this);

            Quantization = QuantizationProperty.Default;
        }

        [Flags]
        public enum ChangeFlags : byte
        {
            None = 0,

            PositionX = 1 << 0,
            PositionY = 1 << 1,
            PositionZ = 1 << 2,
            Position = PositionX | PositionY | PositionZ,

            Rotation = 1 << 3,

            Scale = 1 << 4,

            Coordinates = Position | Rotation | Scale,

            Stop = 1 << 5,

            Teleport = 1 << 6,
        }
    }

    [Serializable]
    public struct Vector3Fields<T>
    {
        [field: SerializeField]
        public T X { get; private set; }

        [field: SerializeField]
        public T Y { get; private set; }

        [field: SerializeField]
        public T Z { get; private set; }

        public Vector3Fields(T value) : this(value, value, value) { }
        public Vector3Fields(T X, T Y, T Z)
        {
            this.X = X;
            this.Y = Y;
            this.Z = Z;
        }
    }
}
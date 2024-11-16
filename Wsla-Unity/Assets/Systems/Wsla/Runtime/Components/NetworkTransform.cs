using System;

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
            public bool PositionX { get; private set; }
            [field: SerializeField]
            public bool PositionY { get; private set; }
            [field: SerializeField]
            public bool PositionZ { get; private set; }

            [field: Space]
            [field: SerializeField]
            public bool Rotation { get; private set; }

            [field: Space]
            [field: SerializeField]
            public bool Scale { get; private set; }

            public ChangeFlags Value
            {
                get
                {
                    var change = ChangeFlags.None;

                    if (PositionX) change |= ChangeFlags.PositionX;
                    if (PositionY) change |= ChangeFlags.PositionY;
                    if (PositionZ) change |= ChangeFlags.PositionZ;

                    if (Rotation) change |= ChangeFlags.Rotation;

                    if (Scale) change |= ChangeFlags.Scale;

                    return change;
                }
            }

            public MaskProperty(bool value) : this(value, value, value) { }
            public MaskProperty(bool Position, bool Rotation, bool Scale)
            {
                PositionX = Position;
                PositionY = Position;
                PositionZ = Position;

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

            public CoordinatesData Lerp(CoordinatesData end, float t)
            {
                var position = Vector3.Lerp(this.Position, end.Position, t);
                var rotation = Quaternion.Lerp(this.Rotation, end.Rotation, t);
                var scale = Vector3.Lerp(this.Scale, end.Scale, t);

                return new(position, rotation, scale);
            }

            public CoordinatesData(Vector3 Position, Quaternion Rotation, Vector3 Scale)
            {
                this.Position = Position;
                this.Rotation = Rotation;
                this.Scale = Scale;
            }
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
                    if (Mask.PositionX && Mathf.Abs(LastSnapshot.Position.x - coordinates.Position.x) > Epsilon)
                        change |= ChangeFlags.PositionX;

                    if (Mask.PositionY && Mathf.Abs(LastSnapshot.Position.y - coordinates.Position.y) > Epsilon)
                        change |= ChangeFlags.PositionY;

                    if (Mask.PositionZ && Mathf.Abs(LastSnapshot.Position.z - coordinates.Position.z) > Epsilon)
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

            SnapshotInterpolator<SnapshotData> Interpolator;

            internal void Init()
            {
                Interpolator = new(BufferSize);
            }

            internal CoordinatesData GetOrigin()
            {
                if (Interpolator.TryGetLast(out var snapshot))
                    return snapshot.Coordinates;

                return Transform.Coordinates.Read();
            }

            internal void Submit(SnapshotData snapshot)
            {
                Interpolator.Submit(snapshot);
            }

            public void Step()
            {
                if (Interpolator.Step(out var snapshot) is false)
                    return;

                var source = snapshot.Coordinates;
                ref var destination = ref Transform.Coordinates;

                destination.Position = source.Position;
                destination.Rotation = source.Rotation;
                destination.Scale = source.Scale;
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
        public struct SnapshotData : ISnapshot<SnapshotData>
        {
            public NetworkTickID Tick { get; }
            public bool Stop => Change.HasFlag(ChangeFlags.Stop);

            public ChangeFlags Change { get; }
            public CoordinatesData Coordinates { get; }

            public SnapshotData Lerp(SnapshotData end, float t)
            {
                return new(Tick, Change, Coordinates.Lerp(end.Coordinates, t));
            }

            public SnapshotData(NetworkTickID Tick, ChangeFlags Change, CoordinatesData Coordinates)
            {
                this.Tick = Tick;
                this.Change = Change;
                this.Coordinates = Coordinates;
            }
        }

        public override void Set(NetworkEntity.Behaviour reference)
        {
            base.Set(reference);

            Network.OnSpawn += SpawnCallback;
        }

        void SpawnCallback()
        {
            TickTimer = Network.API.Tick.Register(TickSlice);
            TickTimer.OnTick += TickCallback;

            MotionDetector.Init();
            SnapshotInterpolation.Init();
        }

        void TickCallback(NetworkTickInfo info)
        {
            if (Network.Entity.IsMine)
            {
                var motion = MotionDetector.Detect();

                if (motion.Change.IsNone)
                    return;

                //Replicate
                {
                    var invocation = Network.RPC.Invoke(nameof(Replicate))
                        .GetPayloadWriter(out var writer);

                    var tick = info.GetTick();
                    WritePayload(writer, tick, motion);

                    if (motion.Change.Stopped)
                        invocation.SetBufferMode();

                    invocation.Broadcast();
                }
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
                if (change.HasFlag(ChangeFlags.PositionX)) NetworkSerializer.WriteValue(motion.Position.x, stream);
                if (change.HasFlag(ChangeFlags.PositionY)) NetworkSerializer.WriteValue(motion.Position.y, stream);
                if (change.HasFlag(ChangeFlags.PositionZ)) NetworkSerializer.WriteValue(motion.Position.z, stream);
            }

            //Rotation
            if (change.HasFlag(ChangeFlags.Rotation)) NetworkSerializer.WriteValue(motion.Rotation, stream);

            //Scale
            if (change.HasFlag(ChangeFlags.Scale)) NetworkSerializer.WriteValue(motion.Scale, stream);
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
                if (change.HasFlag(ChangeFlags.PositionX)) NetworkSerializer.ReadValue(stream, out position.x);
                if (change.HasFlag(ChangeFlags.PositionY)) NetworkSerializer.ReadValue(stream, out position.y);
                if (change.HasFlag(ChangeFlags.PositionZ)) NetworkSerializer.ReadValue(stream, out position.z);
            }

            //Rotation
            if (change.HasFlag(ChangeFlags.Rotation)) NetworkSerializer.ReadValue(stream, out rotation);

            //Scale
            if (change.HasFlag(ChangeFlags.Scale)) NetworkSerializer.ReadValue(stream, out scale);

            return new(tick, change, new(position, rotation, scale));
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

            SnapshotInterpolation.Submit(snapshot);
        }

        public NetworkTransform()
        {
            Mask = new MaskProperty(true);
            Coordinates = new CoordinatesProperty(this);
            MotionDetector = new MotionDetectorProperty(this);
            SnapshotInterpolation = new SnapshotInterpolationProperty(this);
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
}
using System;

using UnityEngine;

using Wsla.Serialization;

namespace Wsla.Unity
{
    [Serializable]
    public class TickAPI : NetworkAPI.Property
    {
        [field: SerializeField, Range(1, 120)]
        public int Rate { get; private set; } = 60;

        public double FixedTimeStep => 1.0d / Rate;

        public const double Epsilon = 1d / 360f;

        double Timer;

        public override void Set(NetworkAPI value)
        {
            base.Set(value);

            if (NetworkAPI.Runtime.ExecutionContext is not NetworkAPI.ExecutionModeSelection.Runtime)
                return;

            API.NetworkUpdate.OnEarlyUpdate += EarlyUpdate;
        }

        void EarlyUpdate()
        {
            Timer += Time.unscaledDeltaTime;

            int iterations;

            for (iterations = 0; Timer >= FixedTimeStep; iterations++)
                Timer -= FixedTimeStep;

            if (iterations is 0)
                return;

            //Should slightly help with floating point errors, needs to be verified?
            if (Epsilon >= Timer)
                Timer = 0;

            Tick(iterations);
        }

        public event TickDelegate OnTick;
        public delegate void TickDelegate(int iterations);
        void Tick(int iteration)
        {
            OnTick?.Invoke(iteration);
        }
    }

    [Serializable]
    public class NetworkTickTimer
    {
        public int Slice { get; }

        int Counter;
        NetworkTickID ID;

        public double Timestep => API.Tick.FixedTimeStep * Slice;

        NetworkAPI API => NetworkAPI.Instance;

        internal void Start()
        {
            API.Tick.OnTick += Tick;
        }
        internal void Stop()
        {
            API.Tick.OnTick -= Tick;
        }

        public event TickDelegate OnTick;
        public delegate void TickDelegate(NetworkTickInfo info);
        void Tick(int iterations)
        {
            Counter += iterations;

            if (Counter >= Slice)
            {
                var info = new NetworkTickInfo(ID, Counter / Slice);
                OnTick?.Invoke(info);

                Counter %= Slice;
                ID = NetworkTickID.Increment(ID);
            }
        }

        public double CalculateTime(NetworkTickID id) => id.Value * Timestep;

        internal NetworkTickTimer(TickSliceRate slice) : this(slice.Value) { }
        internal NetworkTickTimer(int Slice)
        {
            this.Slice = Slice;

            Counter = 0;
            ID = new NetworkTickID(0);

#if UNITY_EDITOR
            API.OnDispose += Stop;
#endif
        }
    }

    [Serializable]
    public struct NetworkTickInfo
    {
        NetworkTickID ID;

        public int Iterations { get; }

        public NetworkTickID GetTick() => GetTick(0);
        public NetworkTickID GetTick(int iteration)
        {
#if DEBUG
            if (iteration >= Iterations || iteration < 0)
                throw new ArgumentOutOfRangeException($"Argument must Be in Range of {nameof(Iterations)}");

#endif

            return ID + iteration;
        }

        public NetworkTickInfo(NetworkTickID ID, int Iterations)
        {
            this.ID = ID;
            this.Iterations = Iterations;
        }
    }

    [Serializable]
    [NetworkBlittable]
    public partial struct NetworkTickID : IEquatable<NetworkTickID>
    {
        public uint Value { get; private set; }

        public override bool Equals(object obj)
        {
            if (obj is NetworkTickID other)
                return Equals(other);

            return false;
        }
        public bool Equals(NetworkTickID other)
        {
            return Value == other.Value;
        }

        public override int GetHashCode() => (int)Value;

        public override string ToString() => Value.ToString();

        public NetworkTickID(uint value)
        {
            this.Value = value;
        }

        public static NetworkTickID Min = new(uint.MinValue);
        public static NetworkTickID Max = new(uint.MaxValue);

        public static bool operator ==(NetworkTickID left, NetworkTickID right) => left.Equals(right);
        public static bool operator !=(NetworkTickID left, NetworkTickID right) => !left.Equals(right);

        public static bool operator >(NetworkTickID left, NetworkTickID right) => left.Value > right.Value;
        public static bool operator <(NetworkTickID left, NetworkTickID right) => left.Value < right.Value;

        public static bool operator >=(NetworkTickID left, NetworkTickID right) => left.Value >= right.Value;
        public static bool operator <=(NetworkTickID left, NetworkTickID right) => left.Value <= right.Value;

        public static NetworkTickID operator +(NetworkTickID left, int increment) => new NetworkTickID((uint)(left.Value + increment));
        public static NetworkTickID operator -(NetworkTickID left, int decrement) => new NetworkTickID((uint)(left.Value - decrement));

        public static NetworkTickID Increment(NetworkTickID index) => index + 1;
    }
}
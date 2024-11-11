using System;

using UnityEngine;

using Wsla.Serialization;

namespace Wsla.Unity
{
    [Serializable]
    public class TickAPI : NetworkAPI.Property
    {
        [field: SerializeField, Range(1, 120)]
        public byte Rate { get; private set; } = 60;

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
            {
                NetworkLog.Error($"No Tick On Frame {Time.frameCount}");
                return;
            }

            Debug.LogError($"Iterations: {iterations} on Frame {Time.frameCount}");

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

        public NetworkTickTimer Register(byte Rate)
        {
            var instance = new NetworkTickTimer(Rate);
            instance.Start();
            return instance;
        }
        public void Unregister(NetworkTickTimer instance)
        {
            instance.Stop();
        }
    }

    [Serializable]
    public class NetworkTickTimer
    {
        public byte Rate { get; }

        int Counter;
        NetworkTickID ID;

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

            if (Counter >= Rate)
            {
                var info = new NetworkTickInfo(ID, Counter / Rate);
                OnTick?.Invoke(info);

                Counter %= Rate;
                ID = NetworkTickID.Increment(ID);
            }
        }

        public NetworkTickTimer(byte Rate)
        {
            this.Rate = Rate;

            Counter = 0;
            ID = new NetworkTickID(0);

#if UNITY_EDITOR
            Application.quitting += Stop;
#endif
        }
    }

    [Serializable]
    public struct NetworkTickInfo
    {
        NetworkTickID ID;

        public int Iterations { get; }

        public NetworkTickID GetID(int iteration)
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
        public byte Value { get; private set; }

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

        public override int GetHashCode() => Value;

        public override string ToString() => Value.ToString();

        public NetworkTickID(byte value)
        {
            this.Value = value;
        }

        public static bool operator ==(NetworkTickID left, NetworkTickID right) => left.Equals(right);
        public static bool operator !=(NetworkTickID left, NetworkTickID right) => !left.Equals(right);

        public static NetworkTickID operator +(NetworkTickID left, int increment) => new NetworkTickID((byte)(left.Value + increment));
        public static NetworkTickID operator -(NetworkTickID left, int decrement) => new NetworkTickID((byte)(left.Value - decrement));

        public static NetworkTickID Increment(NetworkTickID index) => index + 1;
    }
}
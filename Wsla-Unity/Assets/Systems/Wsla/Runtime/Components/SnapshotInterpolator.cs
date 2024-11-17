using System;

using Toolbox;

using UnityEngine;

namespace Wsla.Unity
{
    [Serializable]
    public abstract class SnapshotInterpolator<TSnapshot, TValue>
        where TSnapshot : ISnapshot<TSnapshot, TValue>
    {
        public int BufferSize { get; private set; }

        protected NetworkTickTimer TickTimer;

        public double BufferLatency => TickTimer.Timestep * BufferSize;

        protected double LocalTime;
        protected double RemoteTime;

        public double MaxTime;
        public double MinTime;

        protected RingBuffer<TSnapshot> Collection;
        public ref TSnapshot this[Index index] => ref Collection[index];

        public void Init(int BufferSize, NetworkTickTimer TickTimer)
        {
            this.BufferSize = BufferSize;
            this.TickTimer = TickTimer;

            Collection = new RingBuffer<TSnapshot>(BufferSize * 3);
        }

        public bool TryGetLast(out TSnapshot data)
        {
            if (Collection.Count is 0)
            {
                data = default;
                return false;
            }

            data = Collection[^1];
            return true;
        }

        public void Modify(Index index, TSnapshot data)
        {
            ref var snapshot = ref Collection[index];
            snapshot = data;
        }

        public void Submit(TSnapshot snapshot)
        {
            if (TryGetLast(out var last))
            {
                if (last.Tick >= snapshot.Tick)
                {
                    if (TryGetIndex(snapshot.Tick, out var index))
                    {
                        NetworkLog.Warning($"Alter Tick {snapshot.Tick}");
                        Alter(index, snapshot);
                    }
                    else
                    {
                        NetworkLog.Warning($"Ignore Tick {snapshot.Tick}");
                    }

                    return;
                }

                var difference = snapshot.Tick.Value - last.Tick.Value;

                //Fill Non-Existing Snapshots with Predicted Data
                for (int i = 1; i < difference; i++)
                {
                    var tick = last.Tick + i;
                    var time = TickTimer.CalculateTime(tick);

                    var rate = (i) / 1f / (difference);
                    var value = Lerp(last, snapshot, rate);

                    NetworkLog.Warning($"Fill Tick {tick}, Last: {last.Tick}, New: {snapshot.Tick.Value}");

                    var element = Fill(value, tick, time);

                    Collection.Push(element);
                }
            }

            NetworkLog.Trace($"Submit Tick {snapshot.Tick}");

            Collection.Push(snapshot);

            //Log Difference
            {
                var delta = snapshot.Time - RemoteTime;
                Debug.LogError($"Remote Time Predication Difference {delta}");
            }

            RemoteTime = snapshot.Time;
        }

        protected virtual void Alter(int index, TSnapshot replacement) { }

        public bool TryGetIndex(NetworkTickID tick, out int index)
        {
            index = (int)(tick.Value - Collection[0].Tick.Value);
            return index >= 0 && index < Collection.Count;
        }

        public bool Step(float delta, out TValue value)
        {
            if (Collection.Count < BufferSize)
            {
                value = default;
                return false;
            }

            LocalTime += CalculateAdjustedDelta(delta);
            RemoteTime += delta;

            MinTime = Collection[0].Time;
            MaxTime = Collection[^1].Time;

            //Clamp to Start
            {
                var snapshot = Collection[0];

                if (LocalTime < snapshot.Time)
                {
                    NetworkLog.Warning($"Time Clamped To Start from {LocalTime} to {snapshot.Time}");

                    LocalTime = snapshot.Time;
                    value = snapshot.Value;

                    return true;
                }
            }

            //Clamp to End
            {
                var snapshot = Collection[^1];

                if (LocalTime > snapshot.Time)
                {
                    NetworkLog.Warning($"Time Clamped To End from {LocalTime} to {snapshot.Time}");

                    LocalTime = snapshot.Time;
                    value = snapshot.Value;

                    return true;
                }
            }

            if (Sample(LocalTime, out value) is false)
            {
                return false;
                throw new NotImplementedException();
            }

            return true;
        }

        public double IdealTime;
        public float Speedup;
        public float Slowdown;

        public bool doSlow;
        public bool doSpeed;

        public double StepAllowance;

        public double LatencyRate;
        public double LatencyDiff;

        float CalculateAdjustedDelta(float delta)
        {
            const float MaxSpeedup = 2.0f;
            const float MaxSlowdown = 0.0f;

            if (Collection[^1].Stop)
                return delta;

            var prediction = LocalTime + delta;

            IdealTime = RemoteTime - BufferLatency;

            var difference = RemoteTime - (prediction + BufferLatency);
            LatencyDiff = difference;

            StepAllowance = (RemoteTime - prediction) / TickTimer.Timestep;

            var rate = InverseLerp(0, BufferLatency, Mathf.Abs(delta));
            LatencyRate = rate;

            if (difference > 0) //Speed Up
            {
                var factor = Mathf.Lerp(1f, MaxSpeedup, rate);

                Speedup = factor;
                doSpeed = true;
                doSlow = false;

                return delta * factor;
            }
            else //Slow down
            {
                var factor = Mathf.Lerp(1f, MaxSlowdown, rate);

                Slowdown = factor;
                doSlow = true;
                doSpeed = false;

                return delta * factor;
            }
        }

        bool Sample(double time, out TValue value)
        {
            for (int i = 0; i < Collection.Count - 1; i++)
            {
                (TSnapshot Previous, TSnapshot Incoming) snapshots = (Collection[i], Collection[i + 1]);

                if (time >= snapshots.Previous.Time && time < snapshots.Incoming.Time)
                {
                    ////Clean Old Values
                    //for (int j = 0; j < i; j++)
                    //    Collection.Dequeue();

                    var rate = InverseLerp(snapshots.Previous.Time, snapshots.Incoming.Time, time);
                    value = Lerp(snapshots.Previous, snapshots.Incoming, rate);
                    return true;
                }
            }

            value = default;
            return false;
        }

        public static float InverseLerp(double a, double b, double value)
        {
            if (a != b)
            {
                return Mathf.Clamp01((float)((value - a) / (b - a)));
            }

            return 0f;
        }

        public abstract TValue Lerp(TSnapshot a, TSnapshot b, float t);
        public abstract TSnapshot Fill(TValue value, NetworkTickID tick, double time);
    }

    public interface ISnapshot<TSnapshot, TValue>
    {
        NetworkTickID Tick { get; }

        double Time { get; }

        bool Stop { get; }

        TValue Value { get; }
    }
}
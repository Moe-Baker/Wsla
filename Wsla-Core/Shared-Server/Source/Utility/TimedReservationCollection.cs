using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Wsla
{
    public class TimedReservationCollection
    {
        public TimeSpan Lifetime { get; }

        List<Entry> Entries;
        public struct Entry
        {
            public int Capacity;
            public DateTime Timestamp;

            public TimeSpan CalculateTimeSpan(DateTime now) => (now - Timestamp).Duration();

            public Entry(int Capacity, DateTime Timestamp)
            {
                this.Capacity = Capacity;
                this.Timestamp = Timestamp;
            }
        }

        public int CalculateCapacity()
        {
            if (Entries.Count is 0)
                return 0;

            FreeExpired();

            var capacity = 0;

            foreach (var entry in Entries)
                capacity += entry.Capacity;

            return capacity;
        }
        void FreeExpired()
        {
            var now = GetNow();

            var count = 0;

            for (int i = 0; i < Entries.Count; i++)
            {
                var duration = Entries[i].CalculateTimeSpan(now);

                if (duration >= Lifetime)
                    count += 1;
                else
                    break;
            }

            Entries.RemoveRange(0, count);
        }

        public void ReserveCapacity(int value)
        {
            var timestamp = GetNow();
            var entry = new Entry(value, timestamp);
            Entries.Add(entry);
        }

        public void FreeCapacity(int value)
        {
            if (value is 0)
                return;

            var count = 0;
            var span = CollectionsMarshal.AsSpan(Entries);

            for (int i = 0; i < span.Length; i++)
            {
                ref var entry = ref span[i];

                var min = Math.Min(value, entry.Capacity);

                value -= min;
                entry.Capacity -= min;

                if (entry.Capacity is 0)
                    count += 1;

                if (value is 0)
                    break;
            }

            Entries.RemoveRange(0, count);
        }

        public TimedReservationCollection(TimeSpan Lifetime)
        {
            this.Lifetime = Lifetime;
            Entries = new();
        }

        static DateTime GetNow() => DateTime.UtcNow;
    }
}
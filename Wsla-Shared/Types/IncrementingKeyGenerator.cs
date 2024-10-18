using System;
using System.Collections.Generic;

namespace Wsla.Shared.Global
{
    public class IncrementingKeyGenerator<TKey>
        where TKey : struct
    {
        TKey Index;

        Queue<FreeEntry> Free;
        public struct FreeEntry
        {
            public TKey Key { get; }
            public DateTime Timestamp { get; }

            public bool IsReusable(DateTime time, TimeSpan lifetime) => (time - Timestamp) >= lifetime;

            public FreeEntry(TKey key, DateTime timestamp)
            {
                this.Key = key;
                this.Timestamp = timestamp;
            }
        }

        public TimeSpan Lifetime { get; }

        SourceDelegate Source;
        public delegate bool SourceDelegate(ref TKey index, out TKey key);

        public DateTime Time => DateTime.Now;

        public bool TryReserve(out TKey key)
        {
            if (TryDequeue(out key))
                return true;

            return Source(ref Index, out key);
        }
        bool TryDequeue(out TKey key)
        {
            if (Free.TryPeek(out var entry) && entry.IsReusable(Time, Lifetime))
            {
                Free.Dequeue();
                key = entry.Key;
                return true;
            }

            key = default;
            return false;
        }

        public bool TryReserve(Span<TKey> buffer, out Span<TKey> result)
        {
            result = buffer;

            var index = 0;

            while (index < buffer.Length)
            {
                if (TryDequeue(out buffer[index]) is false)
                    break;

                index += 1;
            }

            while (index < buffer.Length)
            {
                if (Source(ref Index, out buffer[index]) is false)
                {
                    var slice = buffer.Slice(0, index);
                    Return(slice);
                    return false;
                }

                index += 1;
            }

            return true;
        }

        public void Return(TKey key)
        {
            var entry = new FreeEntry(key, Time);
            Free.Enqueue(entry);
        }
        void Return(Span<TKey> keys)
        {
            for (int i = 0; i < keys.Length; i++)
                Return(keys[i]);
        }

        public IncrementingKeyGenerator(TKey index, int capacity, TimeSpan lifetime, SourceDelegate source)
        {
            Free = new Queue<FreeEntry>(capacity);

            this.Index = index;
            this.Lifetime = lifetime;
            this.Source = source;
        }
    }
}
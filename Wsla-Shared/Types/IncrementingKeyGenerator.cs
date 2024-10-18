using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            if (Free.TryPeek(out var entry) && entry.IsReusable(Time, Lifetime))
            {
                Free.Dequeue();
                key = entry.Key;
                return true;
            }

            return Source(ref Index, out key);
        }

        public void Return(TKey key)
        {
            var entry = new FreeEntry(key, Time);
            Free.Enqueue(entry);
        }

        public IncrementingKeyGenerator(int capacity, TimeSpan lifetime, SourceDelegate source)
        {
            Index = default;
            Free = new Queue<FreeEntry>(capacity);

            this.Lifetime = lifetime;
            this.Source = source;
        }
    }
}
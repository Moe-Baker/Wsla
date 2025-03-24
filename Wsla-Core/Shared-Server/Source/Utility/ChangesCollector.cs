using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Wsla
{
    public class ChangesCollector<TKey, TData>
    {
        Dictionary<TKey, TData> Changes;

        MergeDelegate Merger;

        public bool Add(TKey key, TData current)
        {
            ref var value = ref CollectionsMarshal.GetValueRefOrAddDefault(Changes, key, out var exists);

            if (exists)
                value = Merger(value, current);
            else
                value = current;

            return exists;
        }
        public void Clear() => Changes.Clear();
        public bool Remove(TKey key)
        {
            return Changes.Remove(key);
        }

        public bool TryRead(out IReadOnlyDictionary<TKey, TData> collection)
        {
            if (Changes.Count is 0)
            {
                collection = default;
                return false;
            }

            collection = Changes;
            return true;
        }

        public ChangesCollector(MergeDelegate Merger)
        {
            Changes = new();

            this.Merger = Merger;
        }

        public delegate TData MergeDelegate(TData previous, TData current);
    }
}
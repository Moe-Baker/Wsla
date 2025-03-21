using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Wsla
{
    public class ChangesCollector<TKey, TData>
    {
        Dictionary<TKey, TData> Changes;

        MergeDelegate Merger;

        public void Add(TKey key, TData current)
        {
            ref var value = ref CollectionsMarshal.GetValueRefOrAddDefault(Changes, key, out var exists);

            if (exists)
                value = Merger(value, current);
            else
                value = current;
        }
        public void Clear() => Changes.Clear();

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
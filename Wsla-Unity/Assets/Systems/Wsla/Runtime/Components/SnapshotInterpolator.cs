using System;

using Toolbox;

namespace Wsla.Unity
{
    [Serializable]
    public struct SnapshotInterpolator<T>
        where T : ISnapshot<T>
    {
        public int BufferSize { get; }

        RingBuffer<T> Collection;

        public ref T this[Index index] => ref Collection[index];

        public bool TryGetLast(out T data)
        {
            if (Collection.Count is 0)
            {
                data = default;
                return false;
            }

            data = Collection[^1];
            return true;
        }

        public void Modify(Index index, T data)
        {
            ref var snapshot = ref Collection[index];
            snapshot = data;
        }

        public void Init()
        {
            Collection = new RingBuffer<T>(BufferSize);
        }

        public bool Submit(T snapshot)
        {

        }

        public bool Step(out T snapshot)
        {

        }

        public SnapshotInterpolator(int BufferSize)
        {
            this.BufferSize = BufferSize * 3;

            Collection = default;
        }

        public static T Lerp(T start, T end, float t) => start.Lerp(end, t);
    }

    public interface ISnapshot<T>
    {
        NetworkTickID Tick { get; }

        bool Stop { get; }

        T Lerp(T end, float t);
    }
}
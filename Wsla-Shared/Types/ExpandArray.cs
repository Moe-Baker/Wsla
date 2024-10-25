using System;

namespace Wsla
{
    public class ExpandArray<T>
        where T : class
    {
        T[] Collection;

        /// <summary>
        /// Currently allocated available space
        /// </summary>
        public int Allocation => Collection.Length;

        /// <summary>
        /// Number of elements added
        /// </summary>
        public int Count { get; private set; }

        /// <summary>
        /// Maximum available capacity
        /// </summary>
        public int Capacity { get; }

        public Span<T> AsSpan() => new Span<T>(Collection);

        public int Step { get; }

        public T this[int index]
        {
            get
            {
                if (index >= Allocation || index < 0)
                    throw new IndexOutOfRangeException($"Index {index} must be Within Allocation of {Allocation}");

                var item = Collection[index];

                if (IsAssigned(item) is false)
                    throw new InvalidOperationException($"Item at Index {index} not Assigned");

                return item;
            }
        }

        public bool TryGet(int index, out T value)
        {
            if (index >= Allocation || index < 0)
            {
                value = default;
                return false;
            }

            value = Collection[index];

            if (IsAssigned(value) is false)
            {
                value = default;
                return false;
            }

            return true;
        }

        public void Add(int index, T item)
        {
            if (TryAdd(index, item) is false)
                throw new InvalidOperationException($"Item at Index {index} Already Assigned");
        }
        public bool TryAdd(int index, T item)
        {
            Fit(index);

            ref var element = ref Collection[index];
            if (IsAssigned(element))
                return false;

            element = item;
            Count += 1;
            return true;
        }

        public bool Remove(int index) => Remove(index, out _);
        public bool Remove(int index, out T item)
        {
            if (index >= Allocation || index < 0)
                throw new IndexOutOfRangeException($"Index {index} must be Within Allocation of {Allocation}");

            ref var element = ref Collection[index];
            item = element;

            if (IsAssigned(element) is false)
                return false;

            element = null;
            Count -= 1;
            return true;
        }

        public bool IsAssigned(int index)
        {
            if (index >= Allocation || index < 0)
                throw new IndexOutOfRangeException($"Index {index} must be Within Allocation of {Allocation}");

            return ReferenceEquals(Collection[index], null) is false;
        }
        public bool IsAssigned(T value) => ReferenceEquals(value, null) is false;

        void Fit(int index)
        {
            if (index >= Capacity)
                throw new ArgumentOutOfRangeException($"Max Array Capacity is {Capacity}");

            if (index < Allocation)
                return;

            var factor = (index / Step) + 1;

            var size = Step * factor;
            if (size > Capacity) size = Capacity;

            Array.Resize(ref Collection, size);
        }

        public ExpandArray(int allocation, int capacity, int step)
        {
            Collection = allocation == 0 ? Array.Empty<T>() : new T[allocation];

            Count = 0;

            this.Capacity = capacity;
            this.Step = step;
        }

        public Enumerator GetEnumerator() => new Enumerator(this);
        public struct Enumerator
        {
            ExpandArray<T> Array;
            int Index;

            public T Current { get; private set; }
            public bool MoveNext()
            {
                while (true)
                {
                    if (Index >= Array.Allocation)
                        break;

                    Current = Array.Collection[Index];

                    Index += 1;

                    if (Array.IsAssigned(Current))
                        return true;
                }

                return false;
            }

            public Enumerator(ExpandArray<T> Array)
            {
                this.Array = Array;
                Index = 0;
                Current = default;
            }
        }
    }
}
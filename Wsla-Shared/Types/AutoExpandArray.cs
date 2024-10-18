using System;

namespace Wsla.Shared.Global
{
    public class AutoExpandArray<T>
    {
        T[] Collection;

        public int Length => Collection.Length;
        public int Capacity { get; }

        public Span<T> AsSpan() => new Span<T>(Collection);

        public int Step { get; }

        public T this[int index]
        {
            get => Collection[index];
            set
            {
                Fit(index);
                Collection[index] = value;
            }
        }

        void Fit(int index)
        {
            if (index >= Capacity)
                throw new ArgumentOutOfRangeException($"Max Array Capacity is {Capacity}");

            if (index < Length)
                return;

            var factor = (index / Step) + 1;

            var size = Step * factor;
            if (size > Capacity) size = Capacity;

            Array.Resize(ref Collection, size);
        }

        public AutoExpandArray(int length, int capacity, int step)
        {
            Collection = length == 0 ? Array.Empty<T>() : new T[length];

            this.Capacity = capacity;
            this.Step = step;
        }
    }
}
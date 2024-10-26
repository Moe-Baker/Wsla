using System;
using System.Collections.Generic;

namespace Wsla
{
    public struct ExpandList<T>
        where T : class
    {
        List<T> Collection;
        Queue<int> Vacancies;

        /// <summary>
        /// Number of elements added
        /// </summary>
        public int Count => Collection.Count - Vacancies.Count;

        public T this[int index]
        {
            get
            {
                if (index >= Collection.Count || index < 0)
                    throw new IndexOutOfRangeException($"Index {index} must be Within Count of {Collection.Count}");

                var item = Collection[index];

                if (IsAssigned(item) is false)
                    throw new InvalidOperationException($"Item at Index {index} not Assigned");

                return item;
            }
        }

        public bool TryGet(int index, out T value)
        {
            if (index >= Collection.Count || index < 0)
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

        /// <summary>
        /// Adds the item to the list
        /// </summary>
        /// <param name="index"></param>
        /// <param name="item"></param>
        /// <returns>the index that the item was added at</returns>
        /// <exception cref="InvalidOperationException"></exception>
        public int Add(T item)
        {
#if DEBUG
            if (IsAssigned(item) is false)
                throw new ArgumentException($"Item Passed can't be null");
#endif

            if (Vacancies.Count > 0)
            {
                var slot = Vacancies.Dequeue();
                Collection[slot] = item;
                return slot;
            }
            else
            {
                var slot = Collection.Count;
                Collection.Add(item);
                return slot;
            }
        }

        /// <summary>
        /// Finds an item in the list
        /// </summary>
        /// <param name="item"></param>
        /// <param name="index"></param>
        /// <returns>true if found, false if not</returns>
        public bool TryIndex(T item, out int index)
        {
            for (index = 0; index < Collection.Count; index++)
                if (Collection[index] == item)
                    return true;

            return false;
        }

        public bool Remove(T item)
        {
#if DEBUG
            if (IsAssigned(item) is false)
                throw new ArgumentException($"Item Passed can't be null");
#endif

            if (TryIndex(item, out var index) is false)
                return false;

            RemoveAt(index);
            return true;
        }
        public void RemoveAt(int index)
        {
            if (index >= Collection.Count || index < 0)
                throw new IndexOutOfRangeException($"Index {index} must be Within Count of {Collection.Count}");

#if DEBUG
            if (Collection[index] is null)
                throw new InvalidOperationException($"Item at Index {index} Already Removed");
#endif

            Collection[index] = null;
            Vacancies.Enqueue(index);
        }

        public bool IsAssigned(int index)
        {
            if (index >= Collection.Count || index < 0)
                throw new IndexOutOfRangeException($"Index {index} must be Within Count of {Collection.Count}");

            return ReferenceEquals(Collection[index], null) is false;
        }
        public bool IsAssigned(T value) => ReferenceEquals(value, null) is false;

        public ExpandList(int capacity)
        {
            Collection = new List<T>(capacity);
            Vacancies = new Queue<int>(capacity);
        }

        public Enumerator GetEnumerator() => new Enumerator(this);
        public struct Enumerator
        {
            ExpandList<T> List;
            int Index;

            public T Current { get; private set; }
            public bool MoveNext()
            {
                while (true)
                {
                    if (Index >= List.Collection.Count)
                        break;

                    Current = List.Collection[Index];

                    Index += 1;

                    if (List.IsAssigned(Current))
                        return true;
                }

                return false;
            }

            public Enumerator(ExpandList<T> Array)
            {
                this.List = Array;
                Index = 0;
                Current = default;
            }
        }
    }
}
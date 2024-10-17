using System;

namespace Toolbox
{
    public class RingBuffer<T>
    {
        T[] Items;

        /// <summary>
        /// The ammount of items that can be possibly stored at a time
        /// </summary>
        public int Capacity => Items.Length;

        /// <summary>
        /// The ammount of items currently stored
        /// </summary>
        public int Count { get; private set; }
        public bool IsFull => Count >= Capacity;

        /// <summary>
        /// The position of the buffer's pointer
        /// </summary>
        int Position;

        public T this[Index index] => this[index.GetOffset(Count)];
        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= Count)
                    throw new ArgumentOutOfRangeException(nameof(index), index, $"Index must be between (0-{Count})");

                index = (Position - Count + index);
                if (index < 0) index += Count;

                return Items[index];
            }
        }

        /// <summary>
        /// Adds an item to the ring buffer, overwritting an old item if needed
        /// </summary>
        public void Push(T item)
        {
            Items[Position] = item;

            Position += 1;
            if (Position >= Capacity)
                Position = 0;

            Count += 1;
            if (Count >= Capacity)
                Count = Capacity;
        }
        /// <summary>
        /// Pushes an item to the ring buffer only if it's not full, so as to not override any old items
        /// </summary>
        /// <returns>true if successfull, else false</returns>
        public bool TryPush(T item)
        {
            if (IsFull)
                return false;

            Push(item);
            return true;
        }

        /// <summary>
        /// Removes the last added item to the ring buffer in a LIFO like operation
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public T Pop()
        {
            if (Count == 0)
                throw new InvalidOperationException($"No More Items to Pop");

            Count -= 1;

            Position -= 1;
            if (Position < 0)
                Position = Capacity - 1;

            return Items[Position];
        }
        /// <summary>
        /// Returns the last added item if any items are available
        /// </summary>
        /// <returns>true if an item was found, else false</returns>
        public bool TryPop(out T item)
        {
            if (Count <= 0)
            {
                item = default;
                return false;
            }

            item = Pop();
            return true;
        }

        /// <summary>
        /// Returns the last added item without removing it from the ring buffer
        /// </summary>
        /// <returns></returns>
        public T Peek() => this[^1];
        /// <summary>
        /// returns the last added item without removing it
        /// </summary>
        /// <returns>true if succesful, else false</returns>
        public bool TryPeek(out T item)
        {
            if (Count <= 0)
            {
                item = default;
                return false;
            }

            item = Peek();
            return true;
        }

        /// <summary>
        /// Resizes the ring buffer to a desired size, 
        /// if expanded; old items will not be impacted, 
        /// if downsized; old items will be removed as needed, with the older values remaining
        /// </summary>
        /// <param name="newSize"></param>
        public void Resize(int newSize)
        {
            var change = newSize - Capacity;
            if (change == 0)
                return;

            if (newSize == 0)
            {
                Items = Array.Empty<T>();
                Position = 0;
                Count = 0;
            }
            else
            {
                var destination = new T[newSize];

                for (int i = 0; i < Count && i < newSize; i++)
                    destination[i] = this[i];

                Items = destination;
                Position = Math.Min(Count, newSize);
                Count = Math.Min(Count, newSize);
            }
        }

        /// <summary>
        /// Clears all items from the ring buffer
        /// </summary>
        public void Clear()
        {
            Count = 0;
            Position = 0;
        }

        public RingBuffer(int capacity)
        {
            if (capacity == 0)
                Items = Array.Empty<T>();
            else
                Items = new T[capacity];

            Count = 0;
            Position = 0;
        }
        public RingBuffer(T[] items)
        {
            this.Items = items;

            Count = Capacity;
            Position = 0;
        }
    }
}
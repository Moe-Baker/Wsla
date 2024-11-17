using System.Collections;
using System.Collections.Generic;
using System.Numerics;

using Wsla;

NetworkLog.UseConsole();

var buffer = new RingBuffer<int>(4);

buffer.Push(1);
buffer.Push(2);
buffer.Push(3);

buffer.Push(4);
buffer.Push(5);
buffer.Push(6);

buffer.Resize(2);

if (false)
{
    for (int i = 0; i < 3; i++)
    {
        var item = buffer.Pop();
        Console.WriteLine($"Operated Item: {item}");
    }
}

for (int i = 0; i < buffer.Count; i++)
{
    Console.WriteLine(buffer[i]);
}

while (true)
    Console.ReadKey();

[Flags]
public enum ChangeFlags : byte
{
    None = 0,

    PositionX = 1 << 0,
    PositionY = 1 << 1,
    PositionZ = 1 << 2,
    Position = PositionX | PositionY | PositionZ,

    Rotation = 1 << 3,

    Scale = 1 << 4,

    Coordinates = Position | Rotation | Scale,

    Stop = 1 << 5,

    Teleport = 1 << 6,
}

public class RingBuffer<T>
{
    T[] Items;

    /// <summary>
    /// The amount of items that can be possibly stored at a time
    /// </summary>
    public int Capacity => Items.Length;

    /// <summary>
    /// The amount of items currently stored
    /// </summary>
    public int Count { get; private set; }
    public bool IsFull => Count >= Capacity;

    int Start;

    public ref T this[Index index] => ref this[index.GetOffset(Count)];
    public ref T this[int index]
    {
        get
        {
            if (index < 0 || index >= Count)
                throw new ArgumentOutOfRangeException(nameof(index), index, $"Index must be between (0-{Count})");

            index = Repeat(index + Start);

            return ref Items[index];
        }
    }

    int Repeat(int value)
    {
        while (value >= Capacity)
            value -= Capacity;

        while (value < 0)
            value += Capacity;

        return value;
    }

    /// <summary>
    /// Adds an item to the ring buffer, overwriting an old item if needed
    /// </summary>
    public void Push(T item)
    {
        var position = Repeat(Start + Count);

        if (Count == Capacity)
            Start = Repeat(Start + 1);

        Items[position] = item;

        Count += 1;
        if (Count >= Capacity)
            Count = Capacity;
    }

    /// <summary>
    /// Pushes an item to the ring buffer only if it's not full, so as to not override any old items
    /// </summary>
    /// <returns>true if successful, else false</returns>
    public bool TryPush(T item)
    {
        if (IsFull)
            return false;

        Push(item);
        return true;
    }

    /// <summary>
    /// Removes and returns the last added item to the ring buffer in a LIFO like operation
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public T Pop()
    {
        if (TryPop(out var item) is false)
            throw new InvalidOperationException($"No More Items to Pop");

        return item;
    }

    /// <summary>
    /// Removes and returns the last added item if any items are available
    /// </summary>
    /// <returns>true if an item was found, else false</returns>
    public bool TryPop(out T item)
    {
        if (Count is 0)
        {
            item = default;
            return false;
        }

        item = Items[Repeat(Start + Count - 1)];

        Count -= 1;

        return true;
    }

    /// <summary>
    /// Removes and returns the first added item to the ring buffer in a FIFO like operation
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public T Dequeue()
    {
        if (TryDequeue(out var item) is false)
            throw new InvalidOperationException($"No More Items to Dequeue");

        return item;
    }

    /// <summary>
    /// Removes and returns the first added item if any items are available
    /// </summary>
    /// <returns>true if an item was found, else false</returns>
    public bool TryDequeue(out T item)
    {
        if (Count is 0)
        {
            item = default;
            return false;
        }

        item = Items[Start];

        Start = Repeat(Start + 1);
        Count -= 1;

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
    /// <returns>true if successful, else false</returns>
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
    /// if expanded; items will not be impacted, 
    /// if shrunk; newer elements will stay, older values will be discarded
    /// </summary>
    public void Resize(int newSize)
    {
        if (newSize == Capacity)
            return;

        if (newSize == 0)
        {
            Items = Array.Empty<T>();
            Start = 0;
            Count = 0;

            return;
        }

        var destination = new T[newSize];

        if (newSize > Count) //Expand
        {
            for (int i = 0; i < Count; i++)
                destination[i] = this[i];
        }
        else //Shrunk
        {
            for (int i = 0; i < newSize; i++)
                destination[i] = this[i + Count - newSize];

            Count = newSize;
        }

        Items = destination;
        Start = 0;
    }

    /// <summary>
    /// Clears all items from the ring buffer
    /// </summary>
    public void Clear()
    {
        Count = 0;
        Start = 0;
    }

    public RingBuffer(int capacity)
    {
        if (capacity == 0)
            Items = Array.Empty<T>();
        else
            Items = new T[capacity];

        Count = Start = 0;
    }
    public RingBuffer(T[] items)
    {
        this.Items = items;

        Start = 0;
        Count = Capacity;
    }
}
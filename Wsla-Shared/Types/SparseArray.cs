using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Wsla.Serialization;

namespace Wsla
{
    /// <summary>
    /// A collection type optimized to carry (<see cref="MaxNonAllocatedSize"/>) elements without allocating an actual array,
    /// or up to (<see cref="MaxTotalSize"/>) elements in total,
    /// useful for collections you know will most likely have 1-3 elements in them at most times
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public static class SparseArray
    {
        /// <summary>
        /// The maximum number of elements that can be kept in this collection at all (byte.MaxValue | 255)
        /// </summary>
        public const int MaxTotalSize = byte.MaxValue;

        /// <summary>
        /// The maximum number of elements that can be kept in this collection without allocating an internal array
        /// </summary>
        public const int MaxNonAllocatedSize = 3;
        public static bool CheckAllocated(int length) => length > MaxNonAllocatedSize;

        public static SparseArray<T> Empty<T>() => default;

        public static SparseArray<T> From<T>(T Item0) => new SparseArray<T>(1, Item0);
        public static SparseArray<T> From<T>(T Item0, T Item1) => new SparseArray<T>(2, Item0, Item1);
        public static SparseArray<T> From<T>(T Item0, T Item1, T Item2) => new SparseArray<T>(3, Item0, Item1, Item2);

        /// <summary>
        /// Clones all the elements in the input span into a sparse list, allocating a new array only if input is bigger than the sparse limit
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static SparseArray<T> Clone<T>(ReadOnlySpan<T> input)
        {
            var value = Allocate<T>(input.Length);
            var span = value.GetUsedSpan();

            input.CopyTo(span);

            return value;
        }
        public static SparseArray<T> Clone<T>(List<T> list)
        {
            var array = Allocate<T>(list.Count);

            for (int i = 0; i < list.Count; i++)
                array[i] = list[i];

            return array;
        }

        /// <summary>
        /// Wrap this array as a sparse list without copying anything or creating another array
        /// </summary>
        /// <param name="array"></param>
        /// <returns></returns>
        public static SparseArray<T> Wrap<T>(T[] array) => new(array);

        /// <summary>
        /// Creates a new sparse list with a specified length and all default elements
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        public static SparseArray<T> Allocate<T>(int length) => new SparseArray<T>(length);
    }

    /// <summary>
    /// <inheritdoc cref="SparseArray"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct SparseArray<[NetworkSerializationMarker] T> : ISpannable<T>, IAssignableSpannable<T>
    {
        //Field order important, never change
        //Item[0, 1, 2] must always be the first fields in this struct and in the same order

        T Item0, Item1, Item2;
        internal T[] Items;

        public T this[int index]
        {
            get
            {
                var span = GetUsedSpan();
                return span[index];
            }
            set
            {
                var span = GetUsedSpan();
                span[index] = value;
            }
        }

        public byte Length { get; private set; }
        public void SetLength(int value)
        {
            if (value > SparseArray.MaxTotalSize)
                throw new ArgumentOutOfRangeException($"Sparse List can Contain Only a Maximum of {SparseArray.MaxTotalSize}");

            Length = (byte)value;
        }

        /// <summary>
        /// Is this sparse list backend by an internal array?
        /// </summary>
        public bool IsAllocated => SparseArray.CheckAllocated(Length);

        #region Span
        /// <summary>
        /// Get read/write span over the sparse list
        /// </summary>
        /// <returns></returns>
        public Span<T> GetUsedSpan()
        {
            if (IsAllocated)
                return Items.AsSpan();
            else
                return MemoryMarshal.CreateSpan(ref Item0, Length);
        }

        public void Assign(ReadOnlySpan<T> input)
        {
            SetLength(input.Length);

            if (SparseArray.CheckAllocated(Length))
            {
                if (Items?.Length != Length)
                    Items = new T[Length];
            }

            var destination = GetUsedSpan();

            input.CopyTo(destination);
        }
        #endregion

        #region Collection Helpers
        public void CopyTo(Span<T> destination)
        {
            var source = GetUsedSpan();
            source.CopyTo(destination);
        }

        public T[] ToArray()
        {
            var destination = new T[Length];
            CopyTo(destination);
            return destination;
        }

        public List<T> ToList()
        {
            var destination = new List<T>(Length);
            var source = GetUsedSpan();

            for (int i = 0; i < source.Length; i++)
                destination.Add(source[i]);

            return destination;
        }
        #endregion

        #region Enumerator
        public Enumerator GetEnumerator() => new Enumerator(in this);
        public struct Enumerator
        {
            SparseArray<T> Array;

            int Index;

            public T Current { get; private set; }

            public bool MoveNext()
            {
                Index += 1;

                if (Index >= Array.Length)
                    return false;

                Current = Array[Index];
                return true;
            }

            public Enumerator(in SparseArray<T> Array)
            {
                this.Array = Array;
                Current = default;
                Index = -1;
            }
        }
        #endregion

        public SparseArray<T> Clone()
        {
            var span = GetUsedSpan();
            return SparseArray.Clone<T>(span);
        }

        internal SparseArray(byte Length, T Item0 = default, T Item1 = default, T Item2 = default)
        {
            this.Length = Length;

            this.Item0 = Item0;
            this.Item1 = Item1;
            this.Item2 = Item2;

            Items = null;
        }
        internal SparseArray(T[] array)
        {
            if (array.Length > SparseArray.MaxTotalSize)
                throw new ArgumentOutOfRangeException($"Sparse List can Contain Only a Maximum of {SparseArray.MaxTotalSize}");

            Length = (byte)array.Length;

            if (SparseArray.CheckAllocated(Length))
            {
                Items = array;

                Item0 = Item1 = Item2 = default;
            }
            else
            {
                Items = default;

                Item0 = GetElementOrDefault(array, 0);
                Item1 = GetElementOrDefault(array, 1);
                Item2 = GetElementOrDefault(array, 2);
                static T GetElementOrDefault(T[] array, int index) => array.Length > index ? array[index] : default;
            }
        }
        internal SparseArray(int Length)
        {
            this.Length = (byte)Length;

            if (Length > SparseArray.MaxTotalSize)
                throw new ArgumentOutOfRangeException($"Sparse List can Contain Only a Maximum of {SparseArray.MaxTotalSize}");

            if (SparseArray.CheckAllocated(Length))
                Items = new T[Length];
            else
                Items = null;

            Item0 = Item1 = Item2 = default;
        }

        public SparseArray(T Item0) : this(1, Item0) { }
        public SparseArray(T Item0, T Item1) : this(2, Item0, Item1) { }
        public SparseArray(T Item0, T Item1, T Item2) : this(3, Item0, Item1, Item2) { }

        public static implicit operator SparseArray<T>(T Item0) => SparseArray.From(Item0);
        public static implicit operator SparseArray<T>((T, T) tuple) => SparseArray.From(tuple.Item1, tuple.Item2);
        public static implicit operator SparseArray<T>((T, T, T) tuple) => SparseArray.From(tuple.Item1, tuple.Item2, tuple.Item3);

        public static implicit operator ReadOnlySpan<T>(SparseArray<T> array) => array.GetUsedSpan();
        public static implicit operator Span<T>(SparseArray<T> array) => array.GetUsedSpan();
    }

    [SourceGenerator]
    [SourceGenerator.Condition.ConstructedFrom(typeof(SparseArray<>))]
    [SourceGenerator.Builder.FromSourceArguments]
    public class SparseArrayNetworkResolver<T> : NetworkSerializationResolver<SparseArray<T>>
    {
        public override void Write(in SparseArray<T> value, ref BinarySource stream)
        {
            NetworkSerializer.WriteValue(value.Length, ref stream);

            var span = value.GetUsedSpan();

            for (int i = 0; i < value.Length; i++)
                NetworkSerializer.WriteValue(in span[i], ref stream);
        }
        public override void Read(ref SparseArray<T> value, ref BinarySource stream)
        {
            var length = NetworkSerializer.ReadValue<byte>(ref stream);
            value.SetLength(length);

            //Prepare Container
            if (SparseArray.CheckAllocated(length) && value.Items?.Length != length)
                value.Items = new T[length];

            for (int i = 0; i < length; i++)
                value[i] = NetworkSerializer.ReadValue<T>(ref stream);
        }
    }
}
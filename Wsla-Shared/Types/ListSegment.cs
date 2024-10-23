using System.Collections.Generic;
using System;

namespace Wsla
{
    public readonly struct ListSegment<T>
    {
        public List<T> List { get; }
        public int Offset { get; }
        public int Count { get; }

        public bool IsEmpty => Count is 0;

        public T this[int index]
        {
            get
            {
                ValidateIndex(index);

                return List[index + Offset];
            }
            set
            {
                ValidateIndex(index);

                List[index + Offset] = value;
            }
        }

        void ValidateIndex(int index)
        {
            if (index < 0 || index > Count)
                throw new IndexOutOfRangeException($"Index {index} not in range of ({Offset} to {Count})");
        }

        public ListSegment(List<T> List) : this(List, 0, List.Count) { }
        public ListSegment(List<T> List, int Offset, int Count)
        {
            if (List is null)
                throw new ArgumentNullException(nameof(List));

            if (Offset + Count > List.Count)
                throw new ArgumentOutOfRangeException($"Offset and Count Out of Range of List");

            this.List = List;
            this.Offset = Offset;
            this.Count = Count;
        }
    }
}
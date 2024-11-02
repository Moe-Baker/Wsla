using System;
using System.Collections;
using System.Collections.Generic;

namespace Wsla
{
    public class GenericPool<T>
    {
        Stack<T> Stack;
        Func<T> Creator;
        Action<T> Reseter;

        public struct Handle : IDisposable
        {
            public GenericPool<T> Pool { get; }
            public T Item { get; }

            public void Dispose()
            {
                Pool.Return(Item);
            }

            public Handle(GenericPool<T> Pool, T Item)
            {
                this.Pool = Pool;
                this.Item = Item;
            }
        }

        public Handle Rent(out T item)
        {
            item = Retrieve();

            return new Handle(this, item);
        }

        public T Retrieve()
        {
            if (Stack.TryPop(out var item) is false)
                item = Creator();

            return item;
        }

        public void Return(T item)
        {
            Reseter(item);
            Stack.Push(item);
        }

        public GenericPool(Func<T> Creator, Action<T> Reseter)
        {
            this.Creator = Creator;
            this.Reseter = Reseter;

            Stack = new Stack<T>(5);
        }
    }
}
using System;
using System.Collections.Generic;

namespace Wsla
{
    public class ObjectPool<T>
        where T : class
    {
        Stack<T> Stack;

        public CreateDelegate Create { get; }
        public delegate T CreateDelegate();

        public ResetDelegate Reset { get; set; }
        public delegate void ResetDelegate(T instance);

        public T Take()
        {
            if (Stack.TryPop(out var instance) is false)
                instance = Create();

            return instance;
        }

        public Handle Lease(out T instance)
        {
            instance = Take();

            return new Handle(this, instance);
        }
        public struct Handle : IDisposable
        {
            ObjectPool<T> Pool { get; }
            T Instance { get; }

            public void Dispose()
            {
                Pool.Return(Instance);
            }

            public Handle(ObjectPool<T> Pool, T Instance)
            {
                this.Pool = Pool;
                this.Instance = Instance;
            }
        }

        public void Return(T instance)
        {
            Reset(instance);
            Stack.Push(instance);
        }

        public ObjectPool(CreateDelegate Creator)
        {
            Stack = new Stack<T>();

            this.Create = Creator;
        }
    }
}
using System;
using System.Collections.Generic;

namespace Wsla
{
    public class ObjectPool<T>
        where T : class
    {
        Stack<T> Stack;

        public CreateDelegate Creator { get; }
        public delegate T CreateDelegate();

        public ResetDelegate Resetter { get; set; }
        public delegate void ResetDelegate(T instance);

        public T Rent()
        {
            if (Stack.TryPop(out var instance))
            {
                Resetter?.Invoke(instance);
            }
            else
            {
                instance = Creator();
            }

            return instance;
        }

        public Handle Lease(out T instance)
        {
            instance = Rent();

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
            Stack.Push(instance);
        }

        public ObjectPool(CreateDelegate Creator)
        {
            Stack = new Stack<T>();

            this.Creator = Creator;
        }
    }
}
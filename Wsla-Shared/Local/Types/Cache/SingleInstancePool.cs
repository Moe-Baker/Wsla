using System;

namespace Wsla
{
    public struct SingleInstancePool<T>
    {
        T Instance;
        Action<T> Reset;

        public T Take()
        {
            Reset(Instance);
            return Instance;
        }

        public SingleInstancePool(T Instance, Action<T> Reset)
        {
            this.Instance = Instance;
            this.Reset = Reset;
        }
    }
}
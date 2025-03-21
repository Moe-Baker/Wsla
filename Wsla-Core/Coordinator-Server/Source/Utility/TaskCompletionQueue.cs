using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Wsla.Server
{
    public class TaskCompletionQueue<TKey, TValue>
    {
        Dictionary<TKey, TaskCompletionSource<TValue>> Dictionary;

        public TaskCompletionSource<TValue> Create(TKey key, TimeSpan timeout)
        {
            var source = new CancellationTokenSource(timeout);

            return Create(key, cancellation: source.Token);
        }
        public TaskCompletionSource<TValue> Create(TKey key, CancellationToken cancellation = default)
        {
            lock (Dictionary)
            {
                var operation = new TaskCompletionSource<TValue>();

                if (cancellation.CanBeCanceled)
                    cancellation.Register(Callback, key);

                Dictionary.Add(key, operation);

                return operation;
            }
        }

        public bool Fulfill(TKey key, TValue value)
        {
            if (TryRemove(key, out var operation) is false)
                return false;

            return operation.TrySetResult(value);
        }

        void Callback(object state)
        {
            if (state is not TKey key)
                throw new ArgumentException($"Excepted a ({typeof(TKey)}) Key, Got ({state?.GetType()})");

            Cancel(key);
        }
        public bool Cancel(TKey key)
        {
            if (TryRemove(key, out var operation) is false)
                return false;

            operation.TrySetCanceled();
            return true;
        }

        bool TryRemove(TKey key, out TaskCompletionSource<TValue> operation)
        {
            lock (Dictionary)
            {
                return Dictionary.Remove(key, out operation);
            }
        }

        public TaskCompletionQueue()
        {
            Dictionary = new();
        }
    }
}
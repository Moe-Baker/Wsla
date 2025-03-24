using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Wsla.Server
{
    public class TaskCompletionQueue<TKey, TValue>
    {
        Dictionary<TKey, Entry> Dictionary;
        record struct Entry(TaskCompletionSource<TValue> Operation, CancellationTokenRegistration Cancellation);

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

                CancellationTokenRegistration registration = default;

                if (cancellation.CanBeCanceled)
                    registration = cancellation.Register(Cancel, key);

                var entry = new Entry(operation, registration);

                if (cancellation.IsCancellationRequested)
                    Cancel(entry);
                else
                    Dictionary.Add(key, entry);

                return operation;
            }
        }

        public bool Fulfill(TKey key, TValue value)
        {
            if (TryRemove(key, out var entry) is false)
                return false;

            entry.Cancellation.Unregister();

            return entry.Operation.TrySetResult(value);
        }

        void Cancel(object state)
        {
            if (state is not TKey key)
                throw new ArgumentException($"Excepted a ({typeof(TKey)}) Key, Got ({state?.GetType()})");

            Cancel(key);
        }
        public bool Cancel(TKey key)
        {
            if (TryRemove(key, out var entry) is false)
                return false;

            return Cancel(entry);
        }
        bool Cancel(Entry entry)
        {
            entry.Cancellation.Unregister();

            return entry.Operation.TrySetCanceled();
        }

        bool TryRemove(TKey key, out Entry entry)
        {
            lock (Dictionary)
            {
                return Dictionary.Remove(key, out entry);
            }
        }

        public TaskCompletionQueue(int capacity)
        {
            Dictionary = new(capacity);
        }
    }
}
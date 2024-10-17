using System.Collections.Generic;

namespace Toolbox
{
    /// <summary>
    /// A two way dictionary
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class Glossary<TKey, TValue>
    {
        public Dictionary<TKey, TValue> KeysToValues { get; }
        public Dictionary<TValue, TKey> ValuesToKeys { get; }

        public int Count => KeysToValues.Count;

        public bool ContainsKey(TKey key) => KeysToValues.ContainsKey(key);
        public bool ContainsValue(TValue value) => ValuesToKeys.ContainsKey(value);

        public bool TryGetValue(TKey key, out TValue value) => KeysToValues.TryGetValue(key, out value);
        public bool TryGetValue(TValue value, out TKey key) => ValuesToKeys.TryGetValue(value, out key);

        public void Add(TKey key, TValue value)
        {
            KeysToValues.Add(key, value);
            ValuesToKeys.Add(value, key);
        }
        public bool TryAdd(TKey key, TValue value)
        {
            if (KeysToValues.TryAdd(key, value) == false)
                return false;

            ValuesToKeys.Add(value, key);

            return true;
        }

        public bool Remove(TKey key)
        {
            if (KeysToValues.Remove(key, out var value) == false)
                return false;

            ValuesToKeys.Remove(value);

            return true;
        }
        public bool Remove(TValue value)
        {
            if (ValuesToKeys.Remove(value, out var key) == false)
                return false;

            KeysToValues.Remove(key);

            return true;
        }

        public void Clear()
        {
            KeysToValues.Clear();
            ValuesToKeys.Clear();
        }

        public int EnsureCapacity(int capacity)
        {
            ValuesToKeys.EnsureCapacity(capacity);
            return KeysToValues.EnsureCapacity(capacity);
        }

        public Glossary() : this(0) { }
        public Glossary(int capacity)
        {
            KeysToValues = new(capacity);
            ValuesToKeys = new(capacity);
        }
        public Glossary(int capacity, (IEqualityComparer<TKey> Key, IEqualityComparer<TValue> Value) comparer)
        {
            this.KeysToValues = new(capacity, comparer.Key);
            this.ValuesToKeys = new(capacity, comparer.Value);
        }
    }
}
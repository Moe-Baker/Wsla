using System;

namespace Wsla.Unity
{
    public class AutoCyclingValue<T>
    {
        TimeSpan Lifetime;
        Func<T> Creator;

        DateTime Timestamp;
        T Instance;

        public T Fetch()
        {
            //Check Expiration
            {
                var duration = (DateTime.UtcNow - Timestamp).Duration();

                if (duration >= Lifetime)
                    Refresh();
            }

            return Instance;
        }

        T Refresh()
        {
            if (Instance is IDisposable disposable)
                disposable.Dispose();

            Timestamp = DateTime.UtcNow;

            Instance = Creator();

            return Instance;
        }

        public AutoCyclingValue(TimeSpan Lifetime, Func<T> Creator)
        {
            this.Creator = Creator;
            this.Lifetime = Lifetime;

            Timestamp = DateTime.MinValue;
        }
    }
}
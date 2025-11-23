using System;

namespace Wsla.Server
{
    public class MatchMakingApplication
    {
        readonly ApplicationData Configuration;

        public ApplicationID ID { get; }

        public MatchMakingPool[] Pools;
        public bool TryFindPool(in FixedString<FS20> Name, out MatchMakingPool pool)
        {
            for (byte i = 0; i < Pools.Length; i++)
            {
                pool = Pools[i];

                if (Name.Equals(pool.Name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            pool = default;
            return false;
        }

        public void Refresh()
        {
            foreach (var pool in Pools)
                pool.Refresh();
        }

        public MatchMakingApplication(ApplicationData Configuration, ApplicationID ID)
        {
            this.Configuration = Configuration;
            this.ID = ID;

            Pools = new MatchMakingPool[Configuration.Pools.Length];

            for (int i = 0; i < Pools.Length; i++)
                Pools[i] = new MatchMakingPool(this, Configuration.Pools[i]);
        }
    }
}
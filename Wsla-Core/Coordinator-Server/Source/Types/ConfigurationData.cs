using System.Threading.Tasks;
using System;
using System.Text.Json.Serialization;

namespace Wsla.Server
{
    public class ConfigurationProperty
    {
        public ApplicationData[] Applications;

        public bool TryGetApplicationID(in FixedString<FS20> name, out ApplicationID id)
        {
            for (byte i = 0; i < Applications.Length; i++)
            {
                if (name.Equals(Applications[i].Name, StringComparison.OrdinalIgnoreCase))
                {
                    id = new ApplicationID(i);
                    return true;
                }
            }

            id = default;
            return false;
        }

        public static async Task<ConfigurationProperty> Create(Data data)
        {
            return new ConfigurationProperty()
            {
                Applications = data.Applications,
            };
        }

        public class Data : ServerConfigurationData
        {
            public ApplicationData[] Applications;
        }
    }

    public struct ApplicationData
    {
        public string Name;

        public MatchMakingPoolData[] Pools;
    }

    public struct MatchMakingPoolData
    {
        [JsonRequired]
        public string Name;

        [JsonRequired]
        public CapacityData Capacity;
        public struct CapacityData
        {
            public byte Min;
            public byte Max;
        }

        public bool Backfill;

        [JsonRequired]
        public float Duration;

        /// <summary>
        /// Pool is made to be split into two teams
        /// </summary>
        public bool Balanced;

        public MatchMakingRule[] Rules;
    }
}
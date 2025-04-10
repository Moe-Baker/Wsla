using System.Threading.Tasks;
using System;
using System.Text.Json.Serialization;
using System.ComponentModel;

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
            [Description("List of All Applications To Register To Server")]
            public ApplicationData[] Applications;
        }
    }

    public struct ApplicationData
    {
        [Description("Application Name, Used as Identifier by Clients")]
        public string Name;

        [Description("Match Making Pools")]
        public MatchMakingPoolData[] Pools;
    }

    public struct MatchMakingPoolData
    {
        [JsonRequired, Description("Pool Name, Used as Identifier by Clients")]
        public string Name;

        [JsonRequired, Description("Min & Max Capacity of Pool, Min Capacity Will be Allowed after a Certain Ticket Age Has Passed")]
        public CapacityData Capacity;
        public struct CapacityData
        {
            [JsonRequired]
            public byte Min;

            [JsonRequired]
            public byte Max;
        }

        [Description("Enabling Backfill Will Allow The Room to Be Joined by Clients After it's Created, but it disables Balance & Rules for Late Clients")]
        public bool Backfill;

        [JsonRequired, Description("Duration of Match Making Process, If no Match is Found During Duration, Then the Ticket will Fail")]
        public float Duration;

        [Description("A Balanced Pool Will be Of a Size Divisible by 2, Disabled if Backfill is Enabled")]
        public bool Balanced;

        [Description("Rules for Match Making")]
        public MatchMakingRule[] Rules;
    }
}
using System;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Wsla.Server
{
    public struct MatchMakingRule
    {
        [JsonRequired, Description("Property to Check on Tickets")]
        public string Property;

        [Description("Type of Operation to Perform")]
        [JsonRequired, JsonConverter(typeof(JsonStringEnumConverter<OperationType>))]
        public OperationType Type;
        public enum OperationType
        {
            Equal, NotEqual, Difference
        }

        [Description("Invert the Rule's Check, True = Rule Should Pass, False = Rule Should not Pass!")]
        public bool Invert;

        [Description("Reference Value to Compare Against, Or Null Depending on Rule Type & Intended Usage")]
        public MatchMakingValue Value;

        [Description("Relaxations to Apply Depending on Age of Oldest Ticket in Batch")]
        public Relaxation[] Relaxations;
        public struct Relaxation
        {
            [JsonRequired, Description("Apply Relaxation After this Duration, Not Stackable with Other Relaxations, Each Value is Standalone")]
            public float Delay;

            [Description("Set to True to Disable the Rule")]
            public bool Disable;

            [Description("Assign to Modify the Reference Value of the Rule")]
            public MatchMakingValue? Value;
        }

        /// <summary>
        /// Calculates Relaxation Value Modifications
        /// </summary>
        /// <returns>true if rule is still enabled, false if disabled</returns>
        public bool CalculateRelaxation(MatchMakingPoolBatch batch, out MatchMakingValue target)
        {
            var age = batch.GetOldestTicket().CalculateAge();

            target = Value;

            if (Relaxations is null)
                return true;

            for (int i = 0; i < Relaxations.Length; i++)
            {
                ref var entry = ref Relaxations[i];

                if (entry.Delay > age.TotalSeconds)
                    continue;

                if (entry.Value.HasValue)
                    target = entry.Value.Value;

                if (entry.Disable)
                    return false;
            }

            return true;
        }

        public bool Validate(MatchMakingPoolBatch batch, MatchMakingTicket ticket)
        {
            //Get relaxed value
            var enabled = CalculateRelaxation(batch, out var local);
            if (enabled is false)
                return true;

            if (ticket.Parameters.TryGet(Property, out var remote) is false)
                return false;

            var response = Type switch
            {
                OperationType.Equal => ValidateEqual(batch, ticket, local, remote),
                OperationType.NotEqual => ValidateNotEqual(batch, ticket, local, remote),
                OperationType.Difference => ValidateDifference(batch, ticket, local, remote),

                _ => throw new NotImplementedException(),
            };

            if (Invert)
                return response is false;
            else
                return response is true;
        }
        public bool ValidateEqual(MatchMakingPoolBatch batch, MatchMakingTicket ticket, MatchMakingValue local, MatchMakingValue remote)
        {
            //If local value is null, comparison is against all tickets
            //But we compare only against a single ticket since they are all equal
            if (local.IsNull && batch.GetOldestTicket().Parameters.TryGet(Property, out local) is false)
                return false;

            return remote == local;
        }
        public bool ValidateNotEqual(MatchMakingPoolBatch batch, MatchMakingTicket ticket, MatchMakingValue local, MatchMakingValue remote)
        {
            if (local.IsNull) //Compare All Tickets
            {
                foreach (var entry in batch.Entries)
                {
                    if (entry.Ticket.Parameters.TryGet(Property, out local) is false)
                        return false;

                    if (remote == local)
                        return false;
                }

                return true;
            }
            else //Compare to Local
            {
                return remote != local;
            }
        }
        public bool ValidateDifference(MatchMakingPoolBatch batch, MatchMakingTicket ticket, MatchMakingValue local, MatchMakingValue remote)
        {
            if (ValidateNumber(in local) is false)
                return false;

            if (ValidateNumber(in remote) is false)
                return false;

            foreach (var entry in batch.Entries)
            {
                if (entry.Ticket.Parameters.TryGet(Property, out var value) is false)
                    return false;

                if (ValidateNumber(in value) is false)
                    return false;

                var difference = MathF.Abs(remote.Number - value.Number);

                if (difference > (local.Number + MatchMakingValue.Epsilon))
                    return false;
            }

            return true;
        }

        bool ValidateNumber(in MatchMakingValue value)
        {
            if (value.Type is not MatchMakingValue.ValueType.Number)
            {
                NetworkLog.Error($"Expected Match Making Number, Got {value.Type}");
                return false;
            }

            return true;
        }
    }
}
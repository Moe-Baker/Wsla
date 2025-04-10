using System;
using System.Text.Json.Serialization;

namespace Wsla.Server
{
    public struct MatchMakingRule
    {
        [JsonRequired]
        public string Property;

        [JsonRequired, JsonConverter(typeof(JsonStringEnumConverter<OperationType>))]
        public OperationType Type;
        public enum OperationType
        {
            Equal, NotEqual, Difference
        }

        public bool Invert;

        public MatchMakingValue Value;

        public Relaxation[] Relaxations;
        public struct Relaxation
        {
            [JsonRequired]
            public float Delay;

            public bool Disable;

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
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

        public MatchMakingValue Value;

        public Relaxation[] Relaxations;
        public struct Relaxation
        {
            [JsonRequired]
            public float Delay;

            public bool Disable;

            public MatchMakingValue? Value;
        }
        public void CalculateRelaxation(TimeSpan age, out MatchMakingValue target, out bool disable)
        {
            target = Value;
            disable = false;

            if (Relaxations is null)
                return;

            for (int i = 0; i < Relaxations.Length; i++)
            {
                ref var entry = ref Relaxations[i];

                if (entry.Delay > age.TotalSeconds)
                    continue;

                if (entry.Value.HasValue)
                    target = entry.Value.Value;

                if (entry.Disable)
                    disable = true;
            }
        }

        public bool Validate(MatchMakingPoolBatch batch, MatchMakingTicket ticket)
        {
            var age = batch.GetOldestTicket().CalculateAge();

            if (ticket.Parameters.TryGet(Property, out var value1) is false)
                return false;

            CalculateRelaxation(age, out var Value, out var disable);

            if (disable) return true;

            switch (Type)
            {
                case OperationType.Equal:
                {
                    if (Value.Type is MatchMakingValue.ValueType.Null)
                    {
                        if (batch.GetOldestTicket().Parameters.TryGet(Property, out var value2) is false)
                            return false;

                        return value1 == value2;
                    }
                    else
                    {
                        return value1 == Value;
                    }
                }

                case OperationType.NotEqual:
                {
                    if (Value.Type is MatchMakingValue.ValueType.Null)
                    {
                        foreach (var entry in batch.Entries)
                        {
                            if (entry.Ticket.Parameters.TryGet(Property, out var value2) is false)
                                return false;

                            if (value1 == value2)
                                return false;
                        }

                        return true;
                    }
                    else
                    {
                        return value1 != Value;
                    }
                }

                case OperationType.Difference:
                {
                    if (Value.Type is not MatchMakingValue.ValueType.Number)
                    {
                        NetworkLog.Error($"Invalid Value Type for {Type} Calculation");
                        return false;
                    }

                    throw new NotImplementedException();
                }

                default: throw new NotImplementedException();
            }
        }
    }
}
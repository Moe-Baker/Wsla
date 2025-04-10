using System;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Wsla.Server
{
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "Type")]
    //Equality
    [JsonDerivedType(typeof(EqualRule), EqualRule.ID)]
    [JsonDerivedType(typeof(NotEqualRule), NotEqualRule.ID)]
    //Agreement
    [JsonDerivedType(typeof(AgreeRule), AgreeRule.ID)]
    [JsonDerivedType(typeof(DisagreeRule), DisagreeRule.ID)]
    //Misc
    [JsonDerivedType(typeof(DeltaRule), DeltaRule.ID)]
    [JsonDerivedType(typeof(OddOneIn), OddOneIn.ID)]
    public abstract partial class MatchMakingRule : IJsonOnDeserialized
    {
        [JsonRequired, Description("Property to Check on Tickets")]
        public string Property;

        [Description("Invert the Rule's Check, True = Rule Should Pass, False = Rule Should not Pass!")]
        public bool Invert;

        /// <summary>
        /// Duration of Time after which to disable the rule, infinity if rule is never disabled
        /// </summary>
        [JsonIgnore]
        public float DisableDuration { get; protected set; }

        /// <summary>
        /// Check if the rule can be disabled after this timespan
        /// </summary>
        /// <param name="age"></param>
        /// <returns>true if rule disabled, false if not</returns>
        public bool CheckDisable(TimeSpan age) => age.TotalSeconds >= DisableDuration;

        public virtual void OnDeserialized() { }

        public bool TryReadParameter(MatchMakingTicket ticket, out MatchMakingValue value)
        {
            return ticket.Parameters.TryGet(Property, out value);
        }

        public virtual bool ValidateParameters(in MatchMakingParameters parameters)
        {
            if (parameters.TryGet(Property, out var value) is false)
                return false;

            return ValidateValue(in value);
        }
        public virtual bool ValidateValue(in MatchMakingValue value) => true;

        public virtual bool ValidateJoin(MatchMakingPoolBatch batch, MatchMakingTicket ticket)
        {
            if (CheckDisable(batch.Age))
                return true;

            if (TryReadParameter(ticket, out var remote) is false)
                return false;

            return CheckJoin(batch, ticket, remote);
        }
        protected abstract bool CheckJoin(MatchMakingPoolBatch batch, MatchMakingTicket ticket, MatchMakingValue remote);

        public virtual bool ValidateDispatch(MatchMakingPoolBatch batch)
        {
            if (CheckDisable(batch.Age))
                return true;

            return CheckDispatch(batch);
        }
        protected abstract bool CheckDispatch(MatchMakingPoolBatch batch);

        public static bool ValidateNumber(in MatchMakingValue value)
        {
            if (value.Type is not MatchMakingValue.ValueType.Number)
            {
                NetworkLog.Error($"Expected Match Making Number, Got ({value.Type})");
                return false;
            }

            return true;
        }
    }
    partial class MatchMakingRule
    {
        public abstract class GenericBase<TRelaxation> : MatchMakingRule
            where TRelaxation : MatchMakingRuleRelaxation
        {
            [Description("Relaxations to Apply Depending on Age of Oldest Ticket in Batch")]
            public TRelaxation[] Relaxations;

            public override void OnDeserialized()
            {
                base.OnDeserialized();

                if (Relaxations is not null)
                {
                    //Sort Relaxations by Delay
                    Array.Sort(Relaxations, (x, y) => x.Delay.CompareTo(y.Delay));

                    //Calculate Disable Duration
                    {
                        DisableDuration = float.PositiveInfinity;

                        for (int i = Relaxations.Length - 1; i >= 0; i--)
                        {
                            ref var entry = ref Relaxations[i];

                            if (entry.Disable)
                            {
                                DisableDuration = entry.Delay;
                                break;
                            }
                        }
                    }
                }
            }
        }

        [Description("Checks for Equality Against Reference")]
        public class EqualRule : GenericBase<MatchMakingRuleRelaxation.Value>
        {
            public const string ID = "Equal";

            [JsonRequired, Description("Reference Value to Compare Against")]
            public MatchMakingValue Reference;

            public override bool ValidateValue(in MatchMakingValue value)
            {
                if (Reference.Type != value.Type)
                {
                    NetworkLog.Info($"Mis-Matched Value Types, Expecting {Reference.Type} Got {value.Type}");
                    return false;
                }

                return base.ValidateValue(value);
            }

            protected override bool CheckJoin(MatchMakingPoolBatch batch, MatchMakingTicket ticket, MatchMakingValue remote)
            {
                MatchMakingRuleRelaxation.CalculateRelaxation(this, batch.Age, Reference, out var local);

                return remote == local;
            }

            protected override bool CheckDispatch(MatchMakingPoolBatch batch) => true;
        }
        [Description("Checks for In-Equality Against Reference")]
        public class NotEqualRule : GenericBase<MatchMakingRuleRelaxation.Value>
        {
            public const string ID = "NotEqual";

            [JsonRequired, Description("Reference Value to Compare Against")]
            public MatchMakingValue Reference;

            public override bool ValidateValue(in MatchMakingValue value)
            {
                if (Reference.Type != value.Type)
                {
                    NetworkLog.Info($"Mis-Matched Value Types, Expecting {Reference.Type} Got {value.Type}");
                    return false;
                }

                return base.ValidateValue(value);
            }

            protected override bool CheckJoin(MatchMakingPoolBatch batch, MatchMakingTicket ticket, MatchMakingValue remote)
            {
                MatchMakingRuleRelaxation.CalculateRelaxation(this, batch.Age, Reference, out var local);

                return remote != local;
            }

            protected override bool CheckDispatch(MatchMakingPoolBatch batch) => true;
        }

        [Description("Checks that All Tickets Agree on Parameter")]
        public class AgreeRule : GenericBase<MatchMakingRuleRelaxation>
        {
            public const string ID = "Agree";

            protected override bool CheckJoin(MatchMakingPoolBatch batch, MatchMakingTicket ticket, MatchMakingValue remote)
            {
                if (batch.Count is 0)
                    return true;

                if (TryReadParameter(batch[0], out var local) is false)
                    return false;

                return remote == local;
            }

            protected override bool CheckDispatch(MatchMakingPoolBatch batch) => true;
        }
        [Description("Checks that All Tickets Disagree on Parameter")]
        public class DisagreeRule : GenericBase<MatchMakingRuleRelaxation>
        {
            public const string ID = "Disagree";

            protected override bool CheckJoin(MatchMakingPoolBatch batch, MatchMakingTicket ticket, MatchMakingValue remote)
            {
                if (batch.Count is 0)
                    return true;

                foreach (var entry in batch.Entries)
                {
                    if (TryReadParameter(entry.Ticket, out var local) is false)
                        return false;

                    if (remote == local)
                        return false;
                }

                return true;
            }

            protected override bool CheckDispatch(MatchMakingPoolBatch batch) => true;
        }

        [Description("Checks that the Delta (difference) Between all the Tickets is Smaller than Or Equal to the Reference")]
        public class DeltaRule : GenericBase<MatchMakingRuleRelaxation.Float>
        {
            public const string ID = "Delta";

            [JsonRequired, Description("Reference Number to Compare Against")]
            public float Reference;

            public override bool ValidateValue(in MatchMakingValue value)
            {
                if (value.Type is not MatchMakingValue.ValueType.Number)
                {
                    NetworkLog.Info($"Mis-Matched Value Types, Expecting Number, Got {value.Type}");
                    return false;
                }

                return base.ValidateValue(value);
            }

            protected override bool CheckJoin(MatchMakingPoolBatch batch, MatchMakingTicket ticket, MatchMakingValue remote)
            {
                MatchMakingRuleRelaxation.CalculateRelaxation(this, batch.Age, this.Reference, out var Reference);

                foreach (var entry in batch.Entries)
                {
                    if (TryReadParameter(entry.Ticket, out var local) is false || ValidateNumber(local) is false)
                        return false;

                    var delta = MathF.Abs(remote.Number - local.Number);

                    if (delta > (Reference + MatchMakingValue.Epsilon))
                        return false;
                }

                return true;
            }

            protected override bool CheckDispatch(MatchMakingPoolBatch batch) => true;
        }

        [Description("Checks for a Required Count of OddOnes in Batch")]
        public class OddOneIn : GenericBase<OddOneIn.Relaxation>
        {
            public const string ID = "OddOneIn";

            [JsonRequired]
            public ReferenceData Reference;
            public struct ReferenceData
            {
                [JsonRequired, Description("Value Marking Ordinary Ticket")]
                public MatchMakingValue Ordinary;

                [JsonRequired, Description("Value Marking OddOne Ticket")]
                public MatchMakingValue OddOne;
            }

            [JsonRequired, Description("Number of Odd Ones Required")]
            public int Require;

            public override bool ValidateValue(in MatchMakingValue value)
            {
                if (value != Reference.Ordinary && value != Reference.OddOne)
                {
                    NetworkLog.Info($"Mis-Matched Value, Expecting [{Reference.Ordinary} or {Reference.OddOne}], Got ({value})");
                    return false;
                }

                return base.ValidateValue(value);
            }

            protected override bool CheckJoin(MatchMakingPoolBatch batch, MatchMakingTicket ticket, MatchMakingValue remote)
            {
                var counter = new Counter();

                //Collect all Existing Values
                foreach (var entry in batch.Entries)
                {
                    if (Collect(ref counter, entry.Ticket) is false)
                        return false;
                }

                //Collect Remote Value
                Collect(ref counter, remote);

                var capacity = batch.Pool.Configuration.Capacity.Max;
                CalculateRelaxation(batch.Age, out var Require);

                if (counter.OddOne > Require)
                    return false;

                if (counter.Ordinary > (capacity - Require))
                    return false;

                return true;
            }

            protected override bool CheckDispatch(MatchMakingPoolBatch batch)
            {
                var counter = new Counter();

                //Collect all Existing Values
                foreach (var entry in batch.Entries)
                {
                    if (Collect(ref counter, entry.Ticket) is false)
                        return false;
                }

                CalculateRelaxation(batch.Age, out var Require);

                if (counter.OddOne != Require)
                    return false;

                return true;
            }

            record struct Counter(int Ordinary, int OddOne);
            bool Collect(ref Counter counter, MatchMakingTicket ticket)
            {
                if (TryReadParameter(ticket, out var value) is false)
                    throw new NotImplementedException();

                return Collect(ref counter, value);
            }
            bool Collect(ref Counter counter, MatchMakingValue value)
            {
                if (value == Reference.Ordinary)
                {
                    counter.Ordinary += 1;
                    return true;
                }

                if (value == Reference.OddOne)
                {
                    counter.OddOne += 1;
                    return true;
                }

                return false;
            }

            public class Relaxation : MatchMakingRuleRelaxation
            {
                [Description("Assign to Modify the Required OddOnes")]
                public int? Require;
            }
            void CalculateRelaxation(TimeSpan age, out int output)
            {
                output = Require;

                ref var relaxations = ref Relaxations;

                for (int i = 0; i < relaxations.Length; i++)
                {
                    ref var entry = ref relaxations[i];

                    if (entry.Delay > age.TotalSeconds)
                        return;

                    if (entry.Require.HasValue)
                        output = entry.Require.Value;
                }
            }
        }
    }

    public abstract partial class MatchMakingRuleRelaxation
    {
        [JsonRequired, Description("Apply Relaxation After this Duration, Not Stackable with Other Relaxations, Each Value is Standalone")]
        public float Delay;

        [Description("Set to True to Disable the Rule")]
        public bool Disable;
    }
    partial class MatchMakingRuleRelaxation
    {
        public class Empty : MatchMakingRuleRelaxation { }

        public class Value : MatchMakingRuleRelaxation
        {
            [Description("Assign to Modify the Reference Value of the Rule")]
            public MatchMakingValue? Reference;
        }
        public static void CalculateRelaxation<T>(MatchMakingRule.GenericBase<T> rule, TimeSpan age, in MatchMakingValue input, out MatchMakingValue output)
            where T : Value
        {
            output = input;

            ref var relaxations = ref rule.Relaxations;

            for (int i = 0; i < relaxations.Length; i++)
            {
                ref var entry = ref relaxations[i];

                if (entry.Delay > age.TotalSeconds)
                    return;

                if (entry.Reference.HasValue)
                    output = entry.Reference.Value;
            }
        }

        public class Float : MatchMakingRuleRelaxation
        {
            [Description("Assign to Modify the Reference Value of the Rule")]
            public float? Reference;
        }
        public static void CalculateRelaxation<T>(MatchMakingRule.GenericBase<T> rule, TimeSpan age, in float input, out float output)
            where T : Float
        {
            output = input;

            ref var relaxations = ref rule.Relaxations;

            for (int i = 0; i < relaxations.Length; i++)
            {
                ref var entry = ref relaxations[i];

                if (entry.Delay > age.TotalSeconds)
                    return;

                if (entry.Reference.HasValue)
                    output = entry.Reference.Value;
            }
        }

        public class Int : MatchMakingRuleRelaxation
        {
            [Description("Assign to Modify the Reference Value of the Rule")]
            public int? Reference;
        }
        public static void CalculateRelaxation<T>(MatchMakingRule.GenericBase<T> rule, TimeSpan age, in int input, out int output)
            where T : Int
        {
            output = input;

            ref var relaxations = ref rule.Relaxations;

            for (int i = 0; i < relaxations.Length; i++)
            {
                ref var entry = ref relaxations[i];

                if (entry.Delay > age.TotalSeconds)
                    return;

                if (entry.Reference.HasValue)
                    output = entry.Reference.Value;
            }
        }
    }
}
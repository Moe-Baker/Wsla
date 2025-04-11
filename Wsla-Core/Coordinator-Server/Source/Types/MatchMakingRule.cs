using System;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Wsla.Server
{
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "Type")]
    //Equality
    [JsonDerivedType(typeof(EqualRule), EqualRule.ID)]
    [JsonDerivedType(typeof(NotEqualRule), NotEqualRule.ID)]
    //Comparison
    [JsonDerivedType(typeof(BiggerRule), BiggerRule.ID)]
    [JsonDerivedType(typeof(BiggerOrEqualRule), BiggerOrEqualRule.ID)]
    [JsonDerivedType(typeof(SmallerRule), SmallerRule.ID)]
    [JsonDerivedType(typeof(SmallerOrEqualRule), SmallerOrEqualRule.ID)]
    //Agreement
    [JsonDerivedType(typeof(AgreeRule), AgreeRule.ID)]
    [JsonDerivedType(typeof(DisagreeRule), DisagreeRule.ID)]
    //Misc
    [JsonDerivedType(typeof(DeltaRule), DeltaRule.ID)]
    [JsonDerivedType(typeof(OddOneIn), OddOneIn.ID)]
    public abstract partial class MatchMakingRule : IMatchMakingRule, IJsonOnDeserialized
    {
        [Description("Assign to Disable Rule Without Removing It")]
        public bool Disable { get; set; }

        [JsonRequired, Description("Property to Check on Tickets")]
        public string Property { get; set; }

        [Description("Invert the Rule's Check, True = Rule Should Pass, False = Rule Should not Pass!")]
        public bool Invert { get; set; }

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

        public bool TryReadParameter(MatchMakingTicket ticket, out MatchMakingValue value) => TryReadParameter(ticket.Parameters, out value);
        public bool TryReadParameter(in MatchMakingParameters parameters, out MatchMakingValue value) => parameters.TryGet(Property, out value);

        public static class Validator
        {
            public static bool ValidateNumber(in MatchMakingValue value)
            {
                if (value.Type is not MatchMakingValue.ValueType.Number)
                {
                    NetworkLog.Warning($"Expected Match Making Number, Got ({value.Type})");
                    return false;
                }

                return true;
            }

            public static bool ValidateInput(MatchMakingPool pool, in MatchMakingParameters parameters)
            {
                foreach (var rule in pool.Configuration.IterateRules())
                    if (ValidateInput(rule, in parameters) is false)
                        return false;

                return true;
            }
            public static bool ValidateInput(IMatchMakingRule rule, in MatchMakingParameters parameters)
            {
                if (rule.TryReadParameter(in parameters, out var value) is false)
                {
                    NetworkLog.Warning($"Expected Match Making Parameter {rule.Property} not Found");
                    return false;
                }

                if (rule is not IInputCheck contract)
                    return true;

                return contract.CheckInput(in value);
            }

            public static bool ValidateCreate(MatchMakingTicket ticket)
            {
                foreach (var rule in ticket.Pool.Configuration.IterateRules<ICreateCheck>())
                    if (ValidateCreate(rule, ticket) is false)
                        return false;

                return true;
            }
            public static bool ValidateCreate(ICreateCheck rule, MatchMakingTicket ticket)
            {
                var age = ticket.CalculateAge();
                if (rule.CheckDisable(age))
                    return true;

                if (rule.TryReadParameter(ticket, out var remote) is false)
                    return false;

                return rule.CheckCreate(ticket, age, remote);
            }

            public static bool ValidateJoin(MatchMakingPoolBatch batch, MatchMakingTicket ticket)
            {
                foreach (var rule in batch.Pool.Configuration.IterateRules<IJoinCheck>())
                    if (ValidateJoin(rule, batch, ticket) is false)
                        return false;

                return true;
            }
            public static bool ValidateJoin(IJoinCheck rule, MatchMakingPoolBatch batch, MatchMakingTicket ticket)
            {
                if (rule.CheckDisable(batch.Age))
                    return true;

                if (rule.TryReadParameter(ticket, out var remote) is false)
                    return false;

                return rule.CheckJoin(batch, ticket, remote);
            }

            public static bool ValidateDispatch(MatchMakingPoolBatch batch)
            {
                foreach (var rule in batch.Pool.Configuration.IterateRules<IDispatchCheck>())
                    if (ValidateDispatch(rule, batch) is false)
                        return false;

                return true;
            }
            public static bool ValidateDispatch(IDispatchCheck rule, MatchMakingPoolBatch batch)
            {
                if (rule.CheckDisable(batch.Age))
                    return true;

                return rule.CheckDispatch(batch);
            }
        }

        /// <summary>
        /// Validate Match Making Ticket Input
        /// </summary>
        public interface IInputCheck : IMatchMakingRule
        {
            /// <summary>
            /// <inheritdoc cref="IInputCheck"/>
            /// </summary>
            /// <returns>True if valid operation</returns>
            bool CheckInput(in MatchMakingValue value);
        }

        /// <summary>
        /// Validate Batch Creation for this Single Ticket
        /// </summary>
        public interface ICreateCheck : IMatchMakingRule
        {
            /// <summary>
            /// <inheritdoc cref="ICreateCheck"/>
            /// </summary>
            /// <returns>True if valid operation</returns>
            bool CheckCreate(MatchMakingTicket ticket, TimeSpan age, MatchMakingValue remote);
        }

        /// <summary>
        /// Validate Ticket Ability Join To Batch
        /// </summary>
        public interface IJoinCheck : IMatchMakingRule
        {
            /// <summary>
            /// <inheritdoc cref="IJoinCheck"/>
            /// </summary>
            /// <returns>True if valid operation</returns>
            bool CheckJoin(MatchMakingPoolBatch batch, MatchMakingTicket ticket, MatchMakingValue remote);
        }

        /// <summary>
        /// Validate Batch Dispatch after All Tickets are Finished
        /// </summary>
        public interface IDispatchCheck : IMatchMakingRule
        {
            /// <summary>
            /// <inheritdoc cref="IDispatchCheck"/>
            /// </summary>
            /// <returns>True if valid operation</returns>
            bool CheckDispatch(MatchMakingPoolBatch batch);
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

        #region Equality
        [Description("Checks for Equality Against Reference")]
        public class EqualRule : GenericBase<MatchMakingRuleRelaxation.Value>, IInputCheck, IJoinCheck
        {
            public const string ID = "Equal";

            [JsonRequired, Description("Reference Value to Compare Against")]
            public MatchMakingValue Reference;

            public bool CheckInput(in MatchMakingValue value)
            {
                if (Reference.Type != value.Type)
                {
                    NetworkLog.Warning($"Mis-Matched Value Types, Expecting {Reference.Type} Got {value.Type}");
                    return false;
                }

                return true;
            }

            public bool CheckJoin(MatchMakingPoolBatch batch, MatchMakingTicket ticket, MatchMakingValue remote)
            {
                MatchMakingRuleRelaxation.CalculateRelaxation(this, batch.Age, Reference, out var local);

                return remote == local;
            }
        }
        [Description("Checks for In-Equality Against Reference")]
        public class NotEqualRule : GenericBase<MatchMakingRuleRelaxation.Value>, IInputCheck, IJoinCheck
        {
            public const string ID = "NotEqual";

            [JsonRequired, Description("Reference Value to Compare Against")]
            public MatchMakingValue Reference;

            public bool CheckInput(in MatchMakingValue value)
            {
                if (Reference.Type != value.Type)
                {
                    NetworkLog.Warning($"Mis-Matched Value Types, Expecting {Reference.Type} Got {value.Type}");
                    return false;
                }

                return true;
            }

            public bool CheckJoin(MatchMakingPoolBatch batch, MatchMakingTicket ticket, MatchMakingValue remote)
            {
                MatchMakingRuleRelaxation.CalculateRelaxation(this, batch.Age, Reference, out var local);

                return remote != local;
            }
        }
        #endregion

        #region Agreement
        [Description("Checks that All Tickets Agree on Parameter")]
        public class AgreeRule : GenericBase<MatchMakingRuleRelaxation>, IJoinCheck
        {
            public const string ID = "Agree";

            public bool CheckJoin(MatchMakingPoolBatch batch, MatchMakingTicket ticket, MatchMakingValue remote)
            {
                if (batch.Count is 0)
                    return true;

                if (TryReadParameter(batch[0], out var local) is false)
                    return false;

                return remote == local;
            }
        }
        [Description("Checks that All Tickets Disagree on Parameter")]
        public class DisagreeRule : GenericBase<MatchMakingRuleRelaxation>, IJoinCheck
        {
            public const string ID = "Disagree";

            public bool CheckJoin(MatchMakingPoolBatch batch, MatchMakingTicket ticket, MatchMakingValue remote)
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
        }
        #endregion

        #region Comparison
        public abstract class ComparisonRule : GenericBase<MatchMakingRuleRelaxation.Float>, ICreateCheck, IInputCheck, IJoinCheck
        {
            [Description("Reference Value to Compare Against")]
            public float Reference;

            public abstract bool Compare(float remote, float local);

            public bool CheckCreate(MatchMakingTicket ticket, TimeSpan age, MatchMakingValue remote)
            {
                MatchMakingRuleRelaxation.CalculateRelaxation(this, age, this.Reference, out var Reference);
                return Compare(remote.Number, Reference);
            }
            public bool CheckInput(in MatchMakingValue value)
            {
                if (value.Type is not MatchMakingValue.ValueType.Number)
                {
                    NetworkLog.Warning($"Expected Match Making Number, Got ({value.Type})");
                    return false;
                }

                return true;
            }
            public bool CheckJoin(MatchMakingPoolBatch batch, MatchMakingTicket ticket, MatchMakingValue remote)
            {
                MatchMakingRuleRelaxation.CalculateRelaxation(this, batch.Age, this.Reference, out var Reference);

                return Compare(remote.Number, Reference);
            }
        }

        [Description("Checks that the Remote (Incoming) Value is Bigger than the Reference Value")]
        public class BiggerRule : ComparisonRule
        {
            public const string ID = "Bigger";

            public override bool Compare(float remote, float local) => remote > local;
        }

        [Description("Checks that the Remote (Incoming) Value is Bigger than or Equal the Reference Value")]
        public class BiggerOrEqualRule : ComparisonRule
        {
            public const string ID = "BiggerOrEqual";

            public override bool Compare(float remote, float local)
            {
                if (remote >= local)
                    return true;

                if (MatchMakingValue.CompareNumbers(remote, local))
                    return true;

                return false;
            }
        }

        [Description("Checks that the Remote (Incoming) Value is Smaller than the Reference Value")]
        public class SmallerRule : ComparisonRule
        {
            public const string ID = "Smaller";

            public override bool Compare(float remote, float local) => remote < local;
        }

        [Description("Checks that the Remote (Incoming) Value is Smaller than or Equal the Reference Value")]
        public class SmallerOrEqualRule : ComparisonRule
        {
            public const string ID = "SmallerOrEqual";

            public override bool Compare(float remote, float local)
            {
                if (remote <= local)
                    return true;

                if (MatchMakingValue.CompareNumbers(remote, local))
                    return true;

                return false;
            }
        }
        #endregion

        [Description("Checks that the Delta (difference) Between all the Tickets is Smaller than Or Equal to the Reference")]
        public class DeltaRule : GenericBase<MatchMakingRuleRelaxation.Float>, IInputCheck, IJoinCheck
        {
            public const string ID = "Delta";

            [JsonRequired, Description("Reference Number to Compare Against")]
            public float Reference;

            public bool CheckInput(in MatchMakingValue value)
            {
                if (value.Type is not MatchMakingValue.ValueType.Number)
                {
                    NetworkLog.Warning($"Mis-Matched Value Types, Expecting Number, Got {value.Type}");
                    return false;
                }

                return true;
            }

            public bool CheckJoin(MatchMakingPoolBatch batch, MatchMakingTicket ticket, MatchMakingValue remote)
            {
                MatchMakingRuleRelaxation.CalculateRelaxation(this, batch.Age, this.Reference, out var Reference);

                foreach (var entry in batch.Entries)
                {
                    if (TryReadParameter(entry.Ticket, out var local) is false || Validator.ValidateNumber(local) is false)
                        return false;

                    var delta = MathF.Abs(remote.Number - local.Number);

                    if (delta > (Reference + MatchMakingValue.Epsilon))
                        return false;
                }

                return true;
            }
        }

        [Description("Checks for a Required Count of OddOnes in Batch")]
        public class OddOneIn : GenericBase<OddOneIn.Relaxation>, IInputCheck, IJoinCheck, IDispatchCheck
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

            public bool CheckInput(in MatchMakingValue value)
            {
                if (value != Reference.Ordinary && value != Reference.OddOne)
                {
                    NetworkLog.Warning($"Mis-Matched Value, Expecting [{Reference.Ordinary} or {Reference.OddOne}], Got ({value})");
                    return false;
                }

                return true;
            }

            public bool CheckJoin(MatchMakingPoolBatch batch, MatchMakingTicket ticket, MatchMakingValue remote)
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

            public bool CheckDispatch(MatchMakingPoolBatch batch)
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

    public interface IMatchMakingRule
    {
        bool Disable { get; }

        string Property { get; }

        bool Invert { get; }

        bool CheckDisable(TimeSpan age);

        bool TryReadParameter(in MatchMakingParameters parameters, out MatchMakingValue value);
        bool TryReadParameter(MatchMakingTicket ticket, out MatchMakingValue value);
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

    public ref struct MatchMakingRuleNumerator<T>
        where T : class, IMatchMakingRule
    {
        readonly MatchMakingRule[] Array;
        readonly int Count;
        int Index;

        public T Current { get; private set; }
        public bool MoveNext()
        {
            while (true)
            {
                Index += 1;

                if (Index >= Count)
                    return false;

                var entry = Array[Index];

                if (entry.Disable) continue;
                if (entry is not T) continue;

                Current = entry as T;
                return true;
            }
        }

        public MatchMakingRuleNumerator<T> GetEnumerator() => this;

        public MatchMakingRuleNumerator(MatchMakingRule[] Array)
        {
            this.Array = Array;

            if (Array == null)
                Count = 0;
            else
                Count = Array.Length;

            Index = -1;
        }
    }
}
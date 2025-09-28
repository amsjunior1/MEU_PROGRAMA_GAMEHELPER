using System;
using System.ComponentModel;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace AutoHotKeyTrigger.ProfileManager
{
    /// <summary>
    /// Defines the variable or game state to be checked in a condition.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum Factor
    {
        /// <summary>
        /// Checks the player's current health percentage.
        /// </summary>
        [Description("Player Health (%)")]
        PlayerHealthPercent,

        /// <summary>
        /// Checks the player's current mana percentage.
        /// </summary>
        [Description("Player Mana (%)")]
        PlayerManaPercent,

        /// <summary>
        /// Checks if the flask in slot 1 is usable (has charges).
        /// </summary>
        [Description("Flask 1 is Usable")]
        Flask1IsUsable,

        /// <summary>
        /// Checks if the flask in slot 2 is usable (has charges).
        /// </summary>
        [Description("Flask 2 is Usable")]
        Flask2IsUsable,

        /// <summary>
        /// Checks if the flask in slot 3 is usable (has charges).
        /// </summary>
        [Description("Flask 3 is Usable")]
        Flask3IsUsable,

        /// <summary>
        /// Checks if the flask in slot 4 is usable (has charges).
        /// </summary>
        [Description("Flask 4 is Usable")]
        Flask4IsUsable,

        /// <summary>
        /// Checks if the flask in slot 5 is usable (has charges).
        /// </summary>
        [Description("Flask 5 is Usable")]
        Flask5IsUsable,

        /// <summary>
        /// Checks if the player has a specific buff or debuff by name.
        /// </summary>
        [Description("Has Effect (Buff/Debuff)")]
        HasBuff,

        /// <summary>
        /// Checks if the player does NOT have a specific buff or debuff by name.
        /// </summary>
        [Description("Does NOT Have Effect (Buff/Debuff)")]
        NotHasBuff,

        /// <summary>
        /// Checks if the effect of the flask in slot 1 is currently active.
        /// </summary>
        [Description("Flask 1 Effect is Active")]
        Flask1EffectActive,

        /// <summary>
        /// Checks if the effect of the flask in slot 2 is currently active.
        /// </summary>
        [Description("Flask 2 Effect is Active")]
        Flask2EffectActive,

        /// <summary>
        /// Checks if the effect of the flask in slot 3 is currently active.
        /// </summary>
        [Description("Flask 3 Effect is Active")]
        Flask3EffectActive,

        /// <summary>
        /// Checks if the effect of the flask in slot 4 is currently active.
        /// </summary>
        [Description("Flask 4 Effect is Active")]
        Flask4EffectActive,

        /// <summary>
        /// Checks if the effect of the flask in slot 5 is currently active.
        /// </summary>
        [Description("Flask 5 Effect is Active")]
        Flask5EffectActive
    }

    /// <summary>
    /// Defines the comparison operator to be used in a condition.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum Operator
    {
        /// <summary>
        /// Represents the less than or equal to (<=) operator.
        /// </summary>
        [Description("<=")]
        LessThanOrEqual,

        /// <summary>
        /// Represents the greater than or equal to (>=) operator.
        /// </summary>
        [Description(">=")]
        GreaterThanOrEqual,

        /// <summary>
        /// Represents the is equal to (==) operator.
        /// </summary>
        [Description("==")]
        IsEqualTo,

        /// <summary>
        /// Checks if a boolean condition is true.
        /// </summary>
        [Description("is True")]
        IsTrue,

        /// <summary>
        /// Checks if a boolean condition is false.
        /// </summary>
        [Description("is False")]
        IsFalse,
    }

    /// <summary>
    /// Represents a single, user-configurable condition created via the Simple Editor UI.
    /// </summary>
    public class SimpleCondition
    {
        /// <summary>
        /// Gets or sets the factor to be evaluated.
        /// </summary>
        public Factor SelectedFactor { get; set; } = Factor.PlayerHealthPercent;

        /// <summary>
        /// Gets or sets the operator for the comparison.
        /// </summary>
        public Operator SelectedOperator { get; set; } = Operator.LessThanOrEqual;

        /// <summary>
        /// Gets or sets the value to compare against.
        /// </summary>
        public object Value { get; set; } = 50;

        /// <summary>
        /// Initializes a new instance of the <see cref="SimpleCondition"/> class.
        /// </summary>
        public SimpleCondition() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="SimpleCondition"/> class by cloning another instance.
        /// </summary>
        /// <param name="other">The SimpleCondition to clone.</param>
        public SimpleCondition(SimpleCondition other)
        {
            this.SelectedFactor = other.SelectedFactor;
            this.SelectedOperator = other.SelectedOperator;
            this.Value = other.Value;
        }

        /// <summary>
        /// Converts the user's selections into a script string that DynamicCondition can evaluate.
        /// </summary>
        /// <returns>A script string representing the condition.</returns>
        public string ToScriptString()
        {
            string factorScript = "";
            switch (this.SelectedFactor)
            {
                case Factor.PlayerHealthPercent: factorScript = "PlayerVitals.HP.Percent"; break;
                case Factor.PlayerManaPercent: factorScript = "PlayerVitals.MANA.Percent"; break;
                case Factor.Flask1IsUsable: factorScript = "Flasks.Flask1.IsUsable"; break;
                case Factor.Flask2IsUsable: factorScript = "Flasks.Flask2.IsUsable"; break;
                case Factor.Flask3IsUsable: factorScript = "Flasks.Flask3.IsUsable"; break;
                case Factor.Flask4IsUsable: factorScript = "Flasks.Flask4.IsUsable"; break;
                case Factor.Flask5IsUsable: factorScript = "Flasks.Flask5.IsUsable"; break;
                case Factor.HasBuff: return $"PlayerBuffs.Has(\"{this.Value}\")";
                case Factor.NotHasBuff: return $"!PlayerBuffs.Has(\"{this.Value}\")";
                case Factor.Flask1EffectActive: factorScript = "Flasks.Flask1.Active"; break;
                case Factor.Flask2EffectActive: factorScript = "Flasks.Flask2.Active"; break;
                case Factor.Flask3EffectActive: factorScript = "Flasks.Flask3.Active"; break;
                case Factor.Flask4EffectActive: factorScript = "Flasks.Flask4.Active"; break;
                case Factor.Flask5EffectActive: factorScript = "Flasks.Flask5.Active"; break;
            }

            string operatorScript = "";
            switch (this.SelectedOperator)
            {
                case Operator.LessThanOrEqual: operatorScript = "<="; break;
                case Operator.GreaterThanOrEqual: operatorScript = ">="; break;
                case Operator.IsEqualTo: operatorScript = "=="; break;
                case Operator.IsTrue: return factorScript;
                case Operator.IsFalse: return $"!{factorScript}";
            }

            return $"{factorScript} {operatorScript} {this.Value}";
        }

        /// <summary>
        /// Gets the friendly description text from an enum value's Description attribute.
        /// </summary>
        /// <param name="value">The enum value.</param>
        /// <returns>The description string.</returns>
        public static string GetEnumDescription(Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = (System.ComponentModel.DescriptionAttribute)Attribute.GetCustomAttribute(field, typeof(System.ComponentModel.DescriptionAttribute));
            return attribute == null ? value.ToString() : attribute.Description;
        }

        /// <summary>
        /// Caches the array of all Factor enum values for the UI.
        /// </summary>
        [JsonIgnore]
        public static readonly Array Factors = Enum.GetValues(typeof(Factor));

        /// <summary>
        /// Caches the display names of all Factor enum values for the UI.
        /// </summary>
        [JsonIgnore]
        public static readonly string[] FactorNames = Factors.Cast<Enum>().Select(GetEnumDescription).ToArray();

        /// <summary>
        /// Caches the array of all Operator enum values for the UI.
        /// </summary>
        [JsonIgnore]
        public static readonly Array Operators = Enum.GetValues(typeof(Operator));

        /// <summary>
        /// Caches the display names of all Operator enum values for the UI.
        /// </summary>
        [JsonIgnore]
        public static readonly string[] OperatorNames = Operators.Cast<Enum>().Select(GetEnumDescription).ToArray();
    }
}
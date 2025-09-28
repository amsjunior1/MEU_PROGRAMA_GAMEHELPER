// <copyright file="Rule.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace AutoHotKeyTrigger.ProfileManager
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Numerics;
    using GameHelper;
    using GameHelper.Controller;
    using GameHelper.Utils;
    using ImGuiNET;
    using Newtonsoft.Json;
    using AutoHotKeyTrigger.ProfileManager.Enums;
    using AutoHotKeyTrigger.ProfileManager.Component;
    using ClickableTransparentOverlay.Win32;
    using AutoHotKeyTrigger.ProfileManager.DynamicConditions;
    using AutoHotKeyTrigger.ProfileManager.Templates;
    using Nefarius.ViGEm.Client.Targets.Xbox360;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Defines the type of action a rule will execute.
    /// </summary>
    public enum ActionType { Keyboard, Controller }

    /// <summary>
    /// A helper class for providing controller button information.
    /// </summary>
    public static class ControllerButtonHelper
    {
        public static readonly List<Xbox360Button> AllButtons = new()
        {
            Xbox360Button.A, Xbox360Button.B, Xbox360Button.X, Xbox360Button.Y,
            Xbox360Button.LeftShoulder, Xbox360Button.RightShoulder,
            Xbox360Button.LeftThumb, Xbox360Button.RightThumb,
            Xbox360Button.Start, Xbox360Button.Back,
            Xbox360Button.Up, Xbox360Button.Down, Xbox360Button.Left, Xbox360Button.Right,
        };
        public static readonly string[] ButtonNames = AllButtons.Select(b => b.Name).ToArray();
    }

    /// <summary>
    /// Abstraction for a rule, its conditions, and its action.
    /// </summary>
    public class Rule
    {
        // Properties for the Simple Editor UI state
        [JsonProperty]
        public List<SimpleCondition> SimpleConditions { get; private set; } = new();
        [JsonProperty]
        public string AdvancedConditionScript { get; set; } = string.Empty;
        [JsonProperty]
        public bool UseSimpleEditor { get; set; } = true;

        // This field is for backward compatibility with old settings files.
        [JsonProperty("Conditions", NullValueHandling = NullValueHandling.Ignore)]
        private readonly List<DynamicCondition> oldConditions = new();

        private readonly Stopwatch cooldownStopwatch = Stopwatch.StartNew();
        [JsonProperty] private float delayBetweenRuns = 0;

        /// <summary>
        /// Enable/Disable the rule.
        /// </summary>
        public bool Enabled;

        /// <summary>
        /// User friendly name given to a rule.
        /// </summary>
        public string Name;

        /// <summary>
        /// Rule key to press on success (for Keyboard actions).
        /// </summary>
        public VK Key;

        /// <summary>
        /// The type of action to perform (Keyboard or Controller).
        /// </summary>
        [JsonProperty(ItemConverterType = typeof(StringEnumConverter))]
        public ActionType TypeOfAction { get; set; } = ActionType.Keyboard;

        /// <summary>
        /// The controller button to press (for Controller actions). Not saved in JSON.
        /// </summary>
        [JsonIgnore]
        public Xbox360Button ControllerButton { get; set; }

        /// <summary>
        /// The name of the controller button to press. Used for saving/loading the setting.
        /// </summary>
        public string ControllerButtonName { get; set; }

        [JsonConstructor]
        public Rule(string name)
        {
            this.Name = name;
        }

        public Rule(Rule other)
        {
            this.delayBetweenRuns = other.delayBetweenRuns;
            this.Enabled = false;
            this.Name = $"{other.Name}1";
            this.Key = other.Key;
            this.TypeOfAction = other.TypeOfAction;
            this.ControllerButton = other.ControllerButton;
            this.ControllerButtonName = other.ControllerButtonName;
            this.UseSimpleEditor = other.UseSimpleEditor;
            this.AdvancedConditionScript = other.AdvancedConditionScript;
            this.SimpleConditions = new List<SimpleCondition>();
            foreach (var condition in other.SimpleConditions)
            {
                this.SimpleConditions.Add(new SimpleCondition(condition));
            }
        }

        public static Rule[] CreateDefaultRules()
        {
            var rules = new Rule[2];

            rules[0] = new("LifeFlask") { Enabled = true, Key = VK.KEY_1, TypeOfAction = ActionType.Keyboard };
            rules[0].SimpleConditions.Add(new SimpleCondition { SelectedFactor = Factor.PlayerHealthPercent, SelectedOperator = Operator.LessThanOrEqual, Value = 80 });
            rules[0].SimpleConditions.Add(new SimpleCondition { SelectedFactor = Factor.Flask1IsUsable, SelectedOperator = Operator.IsTrue });
            rules[0].SimpleConditions.Add(new SimpleCondition { SelectedFactor = Factor.Flask1EffectActive, SelectedOperator = Operator.IsFalse });

            rules[1] = new("ManaFlask") { Enabled = true, Key = VK.KEY_2, TypeOfAction = ActionType.Keyboard };
            rules[1].SimpleConditions.Add(new SimpleCondition { SelectedFactor = Factor.PlayerManaPercent, SelectedOperator = Operator.LessThanOrEqual, Value = 30 });
            rules[1].SimpleConditions.Add(new SimpleCondition { SelectedFactor = Factor.Flask2IsUsable, SelectedOperator = Operator.IsTrue });
            rules[1].SimpleConditions.Add(new SimpleCondition { SelectedFactor = Factor.Flask2EffectActive, SelectedOperator = Operator.IsFalse });

            foreach (var rule in rules) { rule.SyncAdvancedScript(); }

            return rules;
        }

        private string GenerateScript()
        {
            if (this.UseSimpleEditor)
            {
                if (this.SimpleConditions.Any()) { return string.Join(" && ", this.SimpleConditions.Select(c => c.ToScriptString())); }
                return "false";
            }
            else { return this.AdvancedConditionScript; }
        }

        private void SyncAdvancedScript()
        {
            if (this.SimpleConditions.Any()) { this.AdvancedConditionScript = string.Join(" && ", this.SimpleConditions.Select(c => c.ToScriptString())); }
            else { this.AdvancedConditionScript = string.Empty; }
        }

        public void DrawSettings()
        {
            ImGui.Checkbox("Enable", ref this.Enabled);
            ImGui.InputText("Name", ref this.Name, 100);

            var actionType = this.TypeOfAction;
            if (ImGuiHelper.EnumComboBox("Action Type", ref actionType)) { this.TypeOfAction = actionType; }

            if (this.TypeOfAction == ActionType.Keyboard)
            {
                var tmpKey = this.Key; if (ImGuiHelper.NonContinuousEnumComboBox("Key", ref tmpKey)) { this.Key = tmpKey; }
            }
            else
            {
                int currentIndex = this.ControllerButton == null ? -1 : ControllerButtonHelper.AllButtons.FindIndex(b => b.Name == this.ControllerButton.Name);
                if (ImGui.Combo("Controller Button", ref currentIndex, ControllerButtonHelper.ButtonNames, ControllerButtonHelper.ButtonNames.Length))
                {
                    if (currentIndex > -1) { this.ControllerButton = ControllerButtonHelper.AllButtons[currentIndex]; this.ControllerButtonName = this.ControllerButton.Name; }
                }
            }

            this.DrawCooldownWidget();
            DrawConditionEditor();
        }

        private void DrawConditionEditor()
        {
            if (ImGui.TreeNodeEx("Conditions (ALL must be true)", ImGuiTreeNodeFlags.DefaultOpen))
            {
                var useSimpleEditor = this.UseSimpleEditor;
                if (ImGui.Checkbox("Use Simple Editor", ref useSimpleEditor))
                {
                    this.UseSimpleEditor = useSimpleEditor;
                    if (!this.UseSimpleEditor) { SyncAdvancedScript(); }
                }

                ImGui.SameLine();
                ImGuiHelper.ToolTip("Check to use the graphical interface to build rules easily.\n" + "Uncheck to view and edit the script manually (advanced).");
                ImGui.Separator();

                if (this.UseSimpleEditor) { DrawSimpleEditor(); }
                else
                {
                    var advancedScript = this.AdvancedConditionScript;
                    if (ImGui.InputTextMultiline("Advanced Script", ref advancedScript, 1000, new Vector2(ImGui.GetContentRegionAvail().X, 100))) { this.AdvancedConditionScript = advancedScript; }
                }

                ImGui.SameLine();
                var tempCondition = new DynamicCondition(this.GenerateScript());
                var evaluationResult = tempCondition.Evaluate();
                var resultColor = evaluationResult ? new Vector4(0, 1, 0, 1) : new Vector4(1, 0, 0, 1);
                ImGui.TextColored(resultColor, evaluationResult ? "(true)" : "(false)");

                ImGui.TreePop();
            }
        }

        private void DrawSimpleEditor()
        {
            bool changed = false;
            for (int i = 0; i < this.SimpleConditions.Count; i++)
            {
                var condition = this.SimpleConditions[i];
                ImGui.PushID($"Condition_{i}");

                if (ImGui.Button("X")) { this.SimpleConditions.RemoveAt(i); changed = true; ImGui.PopID(); break; }
                ImGui.SameLine();

                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X * 0.4f);
                int factorCurrentIndex = (int)condition.SelectedFactor;
                if (ImGui.Combo("##Factor", ref factorCurrentIndex, SimpleCondition.FactorNames, SimpleCondition.FactorNames.Length)) { condition.SelectedFactor = (Factor)SimpleCondition.Factors.GetValue(factorCurrentIndex); changed = true; }
                ImGui.SameLine();

                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X * 0.2f);
                int operatorCurrentIndex = (int)condition.SelectedOperator;
                if (ImGui.Combo("##Operator", ref operatorCurrentIndex, SimpleCondition.OperatorNames, SimpleCondition.OperatorNames.Length)) { condition.SelectedOperator = (Operator)SimpleCondition.Operators.GetValue(operatorCurrentIndex); changed = true; }
                ImGui.SameLine();

                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                switch (condition.SelectedFactor)
                {
                    case Factor.PlayerHealthPercent:
                    case Factor.PlayerManaPercent:
                        int valInt = Convert.ToInt32(condition.Value);
                        if (ImGui.SliderInt("##Value", ref valInt, 0, 100, "%d%%")) { condition.Value = valInt; changed = true; }
                        break;
                    case Factor.HasBuff:
                    case Factor.NotHasBuff:
                        string valStr = condition.Value as string ?? string.Empty;
                        if (ImGui.InputText("##Value", ref valStr, 100)) { condition.Value = valStr; changed = true; }
                        break;
                    default: ImGui.TextDisabled("..."); break;
                }
                ImGui.PopID();
            }
            if (ImGui.Button("+ Add Condition")) { this.SimpleConditions.Add(new SimpleCondition()); changed = true; }
            if (changed) { SyncAdvancedScript(); }
        }

        public void Execute(Action<string> logger, VirtualControllerManager vcm)
        {
            if (this.Enabled && this.Evaluate())
            {
                bool actionTaken = false;
                string actionMessage = string.Empty;
                switch (this.TypeOfAction)
                {
                    case ActionType.Keyboard:
                        if (MiscHelper.KeyUp(this.Key)) { actionTaken = true; actionMessage = $"Rule '{this.Name}' triggered KEYBOARD action: {this.Key}."; }
                        break;
                    case ActionType.Controller:
                        if (this.ControllerButton != null && vcm != null)
                        {
                            vcm.PressButton(this.ControllerButton);
                            actionTaken = true;
                            actionMessage = $"Rule '{this.Name}' triggered VIRTUAL button: {this.ControllerButton.Name}.";
                        }
                        break;
                }
                if (actionTaken) { logger(actionMessage); this.cooldownStopwatch.Restart(); }
            }
        }

        private bool Evaluate()
        {
            if (this.cooldownStopwatch.Elapsed.TotalSeconds > this.delayBetweenRuns)
            {
                // Migrate old conditions format to new format if needed.
                if (this.oldConditions.Any())
                {
                    // This is the line that caused the error.
                    // The 'Source' property is not public. We need to handle this differently.
                    // The logic is moved to generate the script from the Simple/Advanced editor.
                    this.AdvancedConditionScript = string.Join(" && ", this.oldConditions.Select(c => c.ToString())); // Fallback to ToString() if Source is inaccessible
                    this.UseSimpleEditor = false;
                    this.oldConditions.Clear();
                }

                var script = this.GenerateScript();
                if (string.IsNullOrEmpty(script))
                {
                    return false;
                }

                var dynamicCondition = new DynamicCondition(script);
                if (dynamicCondition.Evaluate())
                {
                    return true;
                }
            }
            return false;
        }

        private void DrawCooldownWidget()
        {
            ImGui.DragFloat("Cooldown time (seconds)##DelayTimerConditionDelay", ref this.delayBetweenRuns, 0.1f, 0.0f, 30.0f);
            if (this.delayBetweenRuns > 0)
            {
                var cooldownTimeFraction = this.delayBetweenRuns <= 0f ? 1f :
                    MathF.Min((float)this.cooldownStopwatch.Elapsed.TotalSeconds, this.delayBetweenRuns) / this.delayBetweenRuns;
                ImGui.PushStyleColor(ImGuiCol.Text, ImGuiHelper.Color(200, 0, 200, 255));
                ImGui.ProgressBar(
                    (float)cooldownTimeFraction,
                    Vector2.Zero,
                    cooldownTimeFraction < 1f ? $"Cooling {(cooldownTimeFraction * 100f):0}%" : "Ready");
                ImGui.PopStyleColor();
            }
        }
    }
}
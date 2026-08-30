#nullable enable

using System;
using System.Collections.Generic;

namespace DHHFLastChanceMode.Modules.Config
{
    internal static class ConfigMetadata
    {
        internal sealed class AliasDefinition
        {
            public AliasDefinition(string section, string key)
            {
                Section = section ?? string.Empty;
                Key = key ?? string.Empty;
            }

            public string Section { get; }
            public string Key { get; }
        }

        internal abstract class EntryDefinition
        {
            protected EntryDefinition(string section, string key, string description, bool hostControlled, IReadOnlyList<AliasDefinition>? aliases = null)
            {
                Section = section ?? string.Empty;
                Key = key ?? string.Empty;
                Description = description ?? string.Empty;
                HostControlled = hostControlled;
                Aliases = aliases ?? Array.Empty<AliasDefinition>();
            }

            public string Section { get; }
            public string Key { get; }
            public string Description { get; }
            public bool HostControlled { get; }
            public IReadOnlyList<AliasDefinition> Aliases { get; }
            public abstract Type ValueType { get; }
            public abstract object? GetValue();
            public abstract void SetValue(object? value);
        }

        internal sealed class BoolEntryDefinition : EntryDefinition
        {
            public BoolEntryDefinition(string section, string key, string description, Func<bool> getter, Action<bool> setter, bool hostControlled = true, IReadOnlyList<AliasDefinition>? aliases = null)
                : base(section, key, description, hostControlled, aliases)
            {
                Getter = getter;
                Setter = setter;
            }

            public Func<bool> Getter { get; }
            public Action<bool> Setter { get; }
            public override Type ValueType => typeof(bool);
            public override object GetValue() => Getter();
            public override void SetValue(object? value) => Setter(value as bool? ?? false);
        }

        internal sealed class IntEntryDefinition : EntryDefinition
        {
            public IntEntryDefinition(string section, string key, string description, int min, int max, Func<int> getter, Action<int> setter, bool hostControlled = true, IReadOnlyList<AliasDefinition>? aliases = null)
                : base(section, key, description, hostControlled, aliases)
            {
                Min = min;
                Max = max;
                Getter = getter;
                Setter = setter;
            }

            public int Min { get; }
            public int Max { get; }
            public Func<int> Getter { get; }
            public Action<int> Setter { get; }
            public override Type ValueType => typeof(int);
            public override object GetValue() => Getter();
            public override void SetValue(object? value) => Setter(value as int? ?? 0);
        }

        internal sealed class FloatEntryDefinition : EntryDefinition
        {
            public FloatEntryDefinition(string section, string key, string description, float min, float max, Func<float> getter, Action<float> setter, bool hostControlled = true, IReadOnlyList<AliasDefinition>? aliases = null)
                : base(section, key, description, hostControlled, aliases)
            {
                Min = min;
                Max = max;
                Getter = getter;
                Setter = setter;
            }

            public float Min { get; }
            public float Max { get; }
            public Func<float> Getter { get; }
            public Action<float> Setter { get; }
            public override Type ValueType => typeof(float);
            public override object GetValue() => Getter();
            public override void SetValue(object? value) => Setter(value as float? ?? 0f);
        }

        internal sealed class StringEntryDefinition : EntryDefinition
        {
            public StringEntryDefinition(string section, string key, string description, string[]? options, Func<string> getter, Action<string> setter, bool hostControlled = true, IReadOnlyList<AliasDefinition>? aliases = null)
                : base(section, key, description, hostControlled, aliases)
            {
                Options = options ?? Array.Empty<string>();
                Getter = getter;
                Setter = setter;
            }

            public IReadOnlyList<string> Options { get; }
            public Func<string> Getter { get; }
            public Action<string> Setter { get; }
            public override Type ValueType => typeof(string);
            public override object GetValue() => Getter() ?? string.Empty;
            public override void SetValue(object? value) => Setter(value as string ?? string.Empty);
        }

        internal static readonly IReadOnlyList<EntryDefinition> FeatureFlagEntries = new EntryDefinition[]
        {
            new BoolEntryDefinition(FeatureFlags.Sections.LastChanceQuick, nameof(FeatureFlags.LastChangeMode), FeatureFlags.Descriptions.LastChanceMode, () => FeatureFlags.LastChangeMode, value => FeatureFlags.LastChangeMode = value),
            new IntEntryDefinition(FeatureFlags.Sections.LastChanceQuick, nameof(FeatureFlags.LastChanceTimerSeconds), FeatureFlags.Descriptions.LastChanceTimerSeconds, 30, 600, () => FeatureFlags.LastChanceTimerSeconds, value => FeatureFlags.LastChanceTimerSeconds = value),
            new BoolEntryDefinition(FeatureFlags.Sections.LastChanceQuick, nameof(FeatureFlags.LastChanceDynamicTimerEnabled), FeatureFlags.Descriptions.LastChanceDynamicTimerEnabled, () => FeatureFlags.LastChanceDynamicTimerEnabled, value => FeatureFlags.LastChanceDynamicTimerEnabled = value),
            new IntEntryDefinition(FeatureFlags.Sections.LastChanceQuick, nameof(FeatureFlags.LastChanceMissingPlayers), FeatureFlags.Descriptions.LastChanceMissingPlayers, 0, 32, () => FeatureFlags.LastChanceMissingPlayers, value => FeatureFlags.LastChanceMissingPlayers = value),
            new IntEntryDefinition(FeatureFlags.Sections.LastChanceQuick, nameof(FeatureFlags.LastChanceTimerBonusPerMonsterDeathSeconds), FeatureFlags.Descriptions.LastChanceTimerBonusPerMonsterDeathSeconds, 0, 60, () => FeatureFlags.LastChanceTimerBonusPerMonsterDeathSeconds, value => FeatureFlags.LastChanceTimerBonusPerMonsterDeathSeconds = value),
            new BoolEntryDefinition(FeatureFlags.Sections.LastChanceQuick, nameof(FeatureFlags.LastChanceMonstersSearchEnabled), FeatureFlags.Descriptions.LastChanceMonstersSearchEnabled, () => FeatureFlags.LastChanceMonstersSearchEnabled, value => FeatureFlags.LastChanceMonstersSearchEnabled = value),
            new BoolEntryDefinition(FeatureFlags.Sections.LastChanceQuick, nameof(FeatureFlags.LastChanceMonstersVoiceEnemyOnlyEnabled), FeatureFlags.Descriptions.LastChanceMonstersVoiceEnemyOnlyEnabled, () => FeatureFlags.LastChanceMonstersVoiceEnemyOnlyEnabled, value => FeatureFlags.LastChanceMonstersVoiceEnemyOnlyEnabled = value),
            new IntEntryDefinition(FeatureFlags.Sections.LastChanceExtraction, nameof(FeatureFlags.ConsolationMoneyPercent), FeatureFlags.Descriptions.ConsolationMoneyPercent, 0, 500, () => FeatureFlags.ConsolationMoneyPercent, value => FeatureFlags.ConsolationMoneyPercent = value),
            new BoolEntryDefinition(FeatureFlags.Sections.LastChanceExtraction, nameof(FeatureFlags.LastChancePreserveExtractedCosmeticTokens), FeatureFlags.Descriptions.LastChancePreserveExtractedCosmeticTokens, getter: () => FeatureFlags.LastChancePreserveExtractedCosmeticTokens, setter: value => FeatureFlags.LastChancePreserveExtractedCosmeticTokens = value),
            new BoolEntryDefinition(FeatureFlags.Sections.LastChanceExtraction, nameof(FeatureFlags.LastChancePreserveExtractedMoney), FeatureFlags.Descriptions.LastChancePreserveExtractedMoney, getter: () => FeatureFlags.LastChancePreserveExtractedMoney, setter: value => FeatureFlags.LastChancePreserveExtractedMoney = value),

            new IntEntryDefinition(FeatureFlags.Sections.LastChanceTimer, nameof(FeatureFlags.LastChanceDifficulty1FloorBonusSeconds), FeatureFlags.Descriptions.LastChanceDifficulty1FloorBonusSeconds, 0, 180, () => FeatureFlags.LastChanceDifficulty1FloorBonusSeconds, value => FeatureFlags.LastChanceDifficulty1FloorBonusSeconds = value),
            new IntEntryDefinition(FeatureFlags.Sections.LastChanceTimer, nameof(FeatureFlags.LastChanceDifficulty2FloorBonusSeconds), FeatureFlags.Descriptions.LastChanceDifficulty2FloorBonusSeconds, 0, 180, () => FeatureFlags.LastChanceDifficulty2FloorBonusSeconds, value => FeatureFlags.LastChanceDifficulty2FloorBonusSeconds = value),
            new IntEntryDefinition(FeatureFlags.Sections.LastChanceTimer, nameof(FeatureFlags.LastChanceDifficulty3FloorBonusSeconds), FeatureFlags.Descriptions.LastChanceDifficulty3FloorBonusSeconds, 0, 180, () => FeatureFlags.LastChanceDifficulty3FloorBonusSeconds, value => FeatureFlags.LastChanceDifficulty3FloorBonusSeconds = value),
            new FloatEntryDefinition(FeatureFlags.Sections.LastChanceTimer, nameof(FeatureFlags.LastChanceTimerPerMonsterSeconds), FeatureFlags.Descriptions.LastChanceTimerPerMonsterSeconds, 0f, 15f, () => FeatureFlags.LastChanceTimerPerMonsterSeconds, value => FeatureFlags.LastChanceTimerPerMonsterSeconds = value),
            new FloatEntryDefinition(FeatureFlags.Sections.LastChanceTimer, nameof(FeatureFlags.LastChanceTimerPerRequiredPlayerSeconds), FeatureFlags.Descriptions.LastChanceTimerPerRequiredPlayerSeconds, 0f, 30f, () => FeatureFlags.LastChanceTimerPerRequiredPlayerSeconds, value => FeatureFlags.LastChanceTimerPerRequiredPlayerSeconds = value),
            new FloatEntryDefinition(FeatureFlags.Sections.LastChanceTimer, nameof(FeatureFlags.LastChanceLevelContextRoomWeight), FeatureFlags.Descriptions.LastChanceLevelContextRoomWeight, 0f, 1f, () => FeatureFlags.LastChanceLevelContextRoomWeight, value => FeatureFlags.LastChanceLevelContextRoomWeight = value),
            new FloatEntryDefinition(FeatureFlags.Sections.LastChanceTimer, nameof(FeatureFlags.LastChanceLevelContextMonsterWeight), FeatureFlags.Descriptions.LastChanceLevelContextMonsterWeight, 0f, 1f, () => FeatureFlags.LastChanceLevelContextMonsterWeight, value => FeatureFlags.LastChanceLevelContextMonsterWeight = value),
            new FloatEntryDefinition(FeatureFlags.Sections.LastChanceTimer, nameof(FeatureFlags.LastChanceTimerPerFarthestMeterSeconds), FeatureFlags.Descriptions.LastChanceTimerPerFarthestMeterSeconds, 0f, 3f, () => FeatureFlags.LastChanceTimerPerFarthestMeterSeconds, value => FeatureFlags.LastChanceTimerPerFarthestMeterSeconds = value),
            new FloatEntryDefinition(FeatureFlags.Sections.LastChanceTimer, nameof(FeatureFlags.LastChanceTimerPerRoomStepSeconds), FeatureFlags.Descriptions.LastChanceTimerPerRoomStepSeconds, 0f, 15f, () => FeatureFlags.LastChanceTimerPerRoomStepSeconds, value => FeatureFlags.LastChanceTimerPerRoomStepSeconds = value),
            new FloatEntryDefinition(FeatureFlags.Sections.LastChanceTimer, nameof(FeatureFlags.LastChanceTimerPerBelowTruckPlayerSeconds), FeatureFlags.Descriptions.LastChanceTimerPerBelowTruckPlayerSeconds, 0f, 60f, () => FeatureFlags.LastChanceTimerPerBelowTruckPlayerSeconds, value => FeatureFlags.LastChanceTimerPerBelowTruckPlayerSeconds = value),
            new FloatEntryDefinition(FeatureFlags.Sections.LastChanceTimer, nameof(FeatureFlags.LastChanceTimerPerBelowTruckMeterSeconds), FeatureFlags.Descriptions.LastChanceTimerPerBelowTruckMeterSeconds, 0f, 60f, () => FeatureFlags.LastChanceTimerPerBelowTruckMeterSeconds, value => FeatureFlags.LastChanceTimerPerBelowTruckMeterSeconds = value),
            new FloatEntryDefinition(FeatureFlags.Sections.LastChanceTimer, nameof(FeatureFlags.LastChanceBelowTruckThresholdMeters), FeatureFlags.Descriptions.LastChanceBelowTruckThresholdMeters, -5f, 0f, () => FeatureFlags.LastChanceBelowTruckThresholdMeters, value => FeatureFlags.LastChanceBelowTruckThresholdMeters = value),

            new IntEntryDefinition(FeatureFlags.Sections.LastChanceGameplay, nameof(FeatureFlags.LastChanceSurrenderSeconds), FeatureFlags.Descriptions.LastChanceSurrenderSeconds, 2, 10, () => FeatureFlags.LastChanceSurrenderSeconds, value => FeatureFlags.LastChanceSurrenderSeconds = value),
            new StringEntryDefinition(FeatureFlags.Sections.LastChanceGameplay, nameof(FeatureFlags.LastChanceIndicators), FeatureFlags.Descriptions.LastChanceIndicators, new[] { "None", "Direction" }, () => FeatureFlags.LastChanceIndicators, value => FeatureFlags.LastChanceIndicators = value),
            new FloatEntryDefinition(FeatureFlags.Sections.LastChanceGameplay, nameof(FeatureFlags.LastChanceIndicatorHoldSeconds), FeatureFlags.Descriptions.LastChanceIndicatorHoldSeconds, 0.2f, 5f, () => FeatureFlags.LastChanceIndicatorHoldSeconds, value => FeatureFlags.LastChanceIndicatorHoldSeconds = value),
            new FloatEntryDefinition(FeatureFlags.Sections.LastChanceGameplay, nameof(FeatureFlags.LastChanceIndicatorDirectionDurationSeconds), FeatureFlags.Descriptions.LastChanceIndicatorDirectionDurationSeconds, 0.5f, 20f, () => FeatureFlags.LastChanceIndicatorDirectionDurationSeconds, value => FeatureFlags.LastChanceIndicatorDirectionDurationSeconds = value),
            new FloatEntryDefinition(FeatureFlags.Sections.LastChanceGameplay, nameof(FeatureFlags.LastChanceIndicatorDirectionCooldownSeconds), FeatureFlags.Descriptions.LastChanceIndicatorDirectionCooldownSeconds, 1f, 60f, () => FeatureFlags.LastChanceIndicatorDirectionCooldownSeconds, value => FeatureFlags.LastChanceIndicatorDirectionCooldownSeconds = value),
            new FloatEntryDefinition(FeatureFlags.Sections.LastChanceGameplay, nameof(FeatureFlags.LastChanceIndicatorDirectionPenaltyMaxSeconds), FeatureFlags.Descriptions.LastChanceIndicatorDirectionPenaltyMaxSeconds, 0f, 30f, () => FeatureFlags.LastChanceIndicatorDirectionPenaltyMaxSeconds, value => FeatureFlags.LastChanceIndicatorDirectionPenaltyMaxSeconds = value),
            new FloatEntryDefinition(FeatureFlags.Sections.LastChanceGameplay, nameof(FeatureFlags.LastChanceIndicatorDirectionPenaltyMinSeconds), FeatureFlags.Descriptions.LastChanceIndicatorDirectionPenaltyMinSeconds, 0f, 30f, () => FeatureFlags.LastChanceIndicatorDirectionPenaltyMinSeconds, value => FeatureFlags.LastChanceIndicatorDirectionPenaltyMinSeconds = value),
            new BoolEntryDefinition(FeatureFlags.Sections.LastChanceGameplay, nameof(FeatureFlags.LastChancePupilVisualsEnabled), FeatureFlags.Descriptions.LastChancePupilVisualsEnabled, () => FeatureFlags.LastChancePupilVisualsEnabled, value => FeatureFlags.LastChancePupilVisualsEnabled = value),

            new BoolEntryDefinition(FeatureFlags.Sections.Spectate, nameof(FeatureFlags.SpectateDeadPlayers), FeatureFlags.Descriptions.SpectateDeadPlayers, () => FeatureFlags.SpectateDeadPlayers, value => FeatureFlags.SpectateDeadPlayers = value),
            new StringEntryDefinition(FeatureFlags.Sections.Spectate, nameof(FeatureFlags.SpectateDeadPlayersMode), FeatureFlags.Descriptions.SpectateDeadPlayersMode, new[] { "Always", "LastChanceOnly", "Disabled" }, () => FeatureFlags.SpectateDeadPlayersMode, value => FeatureFlags.SpectateDeadPlayersMode = value),
            new IntEntryDefinition(FeatureFlags.Sections.Spectate, nameof(FeatureFlags.LastChanceSpectateDefaultFov), FeatureFlags.Descriptions.LastChanceSpectateDefaultFov, 0, 120, () => FeatureFlags.LastChanceSpectateDefaultFov, value => FeatureFlags.LastChanceSpectateDefaultFov = value, hostControlled: false),

            new BoolEntryDefinition(FeatureFlags.Sections.Debug, nameof(FeatureFlags.DebugLogging), FeatureFlags.Descriptions.DebugLogging, () => FeatureFlags.DebugLogging, value => FeatureFlags.DebugLogging = value, hostControlled: false),
        };
    }
}

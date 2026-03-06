#nullable enable

namespace DHHFLastChanceMode.Modules.Config
{
    internal static class FeatureFlags
    {
        internal static class Sections
        {
            public const string LastChanceQuick = "1a. LastChance: Quick Setup";
            public const string LastChanceTimer = "1b. LastChance: Timer Calculation";
            public const string LastChanceGameplay = "1c. LastChance: Gameplay & UI";
            public const string Spectate = "2. Spectate";
            public const string Debug = "3. Debug";
        }

        internal static class Descriptions
        {
            public const string LastChanceMode = "When true, prevent the vanilla run manager from switching to the dump level when all players die.";
            public const string LastChanceTimerSeconds = "LastChance timer duration in seconds (integer, 30s steps).";
            public const string LastChanceDynamicTimerEnabled = "Enable dynamic LastChance timer scaling from base timer and run context metrics.";
            public const string LastChanceTimerPerRequiredPlayerSeconds = "Extra seconds added per required player that must reach the truck.";
            public const string LastChanceLevelContextRoomWeight = "Extra multiplier weight applied to level contribution from room-path difficulty (0 disables room context).";
            public const string LastChanceLevelContextMonsterWeight = "Extra multiplier weight applied to level contribution from active search monsters (0 disables monster context).";
            public const string LastChanceTimerPerFarthestMeterSeconds = "Extra seconds added per meter of total required-player distance to truck.";
            public const string LastChanceTimerPerBelowTruckPlayerSeconds = "Extra seconds added per required player below the truck threshold height.";
            public const string LastChanceTimerPerBelowTruckMeterSeconds = "Extra seconds added per meter below threshold (only when height delta <= threshold).";
            public const string LastChanceBelowTruckThresholdMeters = "Height delta threshold (playerY - truckY) below which low-altitude penalties apply. -0.5 means at least half meter below.";
            public const string LastChanceTimerPerRoomStepSeconds = "Extra seconds added per total room-step count (sum of required players shortest paths to truck).";
            public const string LastChanceDynamicMaxMinutesAtLevel = "Level where level-based growth stops increasing (timer still only capped by MaxMinutes).";
            public const string LastChanceDynamicMaxMinutes = "Hard cap (minutes) for final LastChance timer after dynamic scaling.";
            public const string LastChanceConsolationMoney = "LastChance consolation money added on success (integer).";
            public const string LastChanceMissingPlayers = "Number of players allowed to stay outside the truck before LastChance success triggers (0 = all players required).";
            public const string LastChanceTimerBonusPerMonsterDeathSeconds = "Seconds added to LastChance timer whenever a monster dies during active LastChance.";
            public const string LastChanceSurrenderSeconds = "Seconds the player must hold Crouch to surrender during LastChance.";
            public const string LastChanceIndicators = "LastChance indicators mode: None, Direction.";
            public const string LastChanceIndicatorHoldSeconds = "Seconds to hold Tumble before triggering the selected indicator.";
            public const string LastChanceIndicatorDirectionDurationSeconds = "Seconds the Direction indicator stays active once triggered.";
            public const string LastChanceIndicatorDirectionCooldownSeconds = "Cooldown seconds before Direction can be triggered again.";
            public const string LastChanceIndicatorDirectionPenaltyMaxSeconds = "Maximum timer penalty per Direction trigger (low difficulty, and always used when dynamic timer is disabled).";
            public const string LastChanceIndicatorDirectionPenaltyMinSeconds = "Minimum timer penalty per Direction trigger (high difficulty).";
            public const string LastChancePupilVisualsEnabled = "When true, LastChance keeps death-head pupils visible and unlocks eye look-at behavior for head proxy players. When false, eyes/pupils stay vanilla during LastChance.";
            public const string LastChanceMonstersSearchEnabled = "During LastChance, monsters treat disabled players as valid targets (harder return to truck).";
            public const string LastChanceMonstersVoiceEnemyOnlyEnabled = "During LastChance, disabled death-head voice keeps enemy reactions/talk animation but mutes playback to players (enemy-only voice aggro).";
            public const string LastChanceTimerPerMonsterSeconds = "Extra seconds added per active spawned monster when LastChanceMonstersSearch is enabled.";
            public const string SpectateDeadPlayers = "Allow SpectateCamera to cycle through disabled players (dead bodies) when toggling targets.";
            public const string SpectateDeadPlayersMode = "Mode for dead-player spectate switch: Always, LastChanceOnly, Disabled.";
            public const string DebugLogging = "Dump extra log lines that help trace LastChance logic.";
        }

        [FeatureConfigEntry(Sections.LastChanceQuick, Descriptions.LastChanceMode)]
        public static bool LastChangeMode = true;

        [FeatureConfigEntry(Sections.LastChanceQuick, Descriptions.LastChanceTimerSeconds, Min = 30f, Max = 600f)]
        public static int LastChanceTimerSeconds = 60;

        [FeatureConfigEntry(Sections.LastChanceQuick, Descriptions.LastChanceDynamicTimerEnabled)]
        public static bool LastChanceDynamicTimerEnabled = true;

        [FeatureConfigEntry(Sections.LastChanceQuick, Descriptions.LastChanceDynamicMaxMinutes, Min = 5f, Max = 20f)]
        public static int LastChanceDynamicMaxMinutes = 10;

        [FeatureConfigEntry(Sections.LastChanceQuick, Descriptions.LastChanceDynamicMaxMinutesAtLevel, Min = 5f, Max = 60f)]
        public static int LastChanceDynamicMaxMinutesAtLevel = 25;

        [FeatureConfigEntry(Sections.LastChanceQuick, Descriptions.LastChanceMissingPlayers, Min = 0f, Max = 32f)]
        public static int LastChanceMissingPlayers = 0;

        [FeatureConfigEntry(Sections.LastChanceQuick, Descriptions.LastChanceTimerBonusPerMonsterDeathSeconds, Min = 0f, Max = 60f)]
        public static int LastChanceTimerBonusPerMonsterDeathSeconds = 15;

        [FeatureConfigEntry(Sections.LastChanceQuick, Descriptions.LastChanceMonstersSearchEnabled)]
        public static bool LastChanceMonstersSearchEnabled = true;

        [FeatureConfigEntry(Sections.LastChanceQuick, Descriptions.LastChanceMonstersVoiceEnemyOnlyEnabled)]
        public static bool LastChanceMonstersVoiceEnemyOnlyEnabled = true;

        [FeatureConfigEntry(Sections.LastChanceQuick, Descriptions.LastChanceConsolationMoney, Min = 0f, Max = 5f)]
        public static int LastChanceConsolationMoney = 1;

        [FeatureConfigEntry(Sections.LastChanceTimer, Descriptions.LastChanceTimerPerMonsterSeconds, Min = 0f, Max = 60f)]
        public static float LastChanceTimerPerMonsterSeconds = 3f;

        [FeatureConfigEntry(Sections.LastChanceTimer, Descriptions.LastChanceTimerPerRequiredPlayerSeconds, Min = 0f, Max = 120f)]
        public static float LastChanceTimerPerRequiredPlayerSeconds = 8f;

        [FeatureConfigEntry(Sections.LastChanceTimer, Descriptions.LastChanceLevelContextRoomWeight, Min = 0f, Max = 3f)]
        public static float LastChanceLevelContextRoomWeight = 0.5f;

        [FeatureConfigEntry(Sections.LastChanceTimer, Descriptions.LastChanceLevelContextMonsterWeight, Min = 0f, Max = 3f)]
        public static float LastChanceLevelContextMonsterWeight = 0.3f;

        [FeatureConfigEntry(Sections.LastChanceTimer, Descriptions.LastChanceTimerPerFarthestMeterSeconds, Min = 0f, Max = 20f)]
        public static float LastChanceTimerPerFarthestMeterSeconds = 0.6f;

        [FeatureConfigEntry(Sections.LastChanceTimer, Descriptions.LastChanceTimerPerRoomStepSeconds, Min = 0f, Max = 60f)]
        public static float LastChanceTimerPerRoomStepSeconds = 3f;

        [FeatureConfigEntry(Sections.LastChanceTimer, Descriptions.LastChanceTimerPerBelowTruckPlayerSeconds, Min = 0f, Max = 120f)]
        public static float LastChanceTimerPerBelowTruckPlayerSeconds = 15f;

        [FeatureConfigEntry(Sections.LastChanceTimer, Descriptions.LastChanceTimerPerBelowTruckMeterSeconds, Min = 0f, Max = 30f)]
        public static float LastChanceTimerPerBelowTruckMeterSeconds = 15f;

        [FeatureConfigEntry(Sections.LastChanceTimer, Descriptions.LastChanceBelowTruckThresholdMeters, Min = -5f, Max = 0f)]
        public static float LastChanceBelowTruckThresholdMeters = -0.5f;

        [FeatureConfigEntry(Sections.LastChanceGameplay, Descriptions.LastChanceSurrenderSeconds, Min = 2f, Max = 10f)]
        public static int LastChanceSurrenderSeconds = 5;

        [FeatureConfigEntry(Sections.LastChanceGameplay, Descriptions.LastChanceIndicators, Options = new[] { "None", "Direction" })]
        public static string LastChanceIndicators = "Direction";

        [FeatureConfigEntry(Sections.LastChanceGameplay, Descriptions.LastChanceIndicatorHoldSeconds, Min = 0.2f, Max = 5f)]
        public static float LastChanceIndicatorHoldSeconds = 2f;

        [FeatureConfigEntry(Sections.LastChanceGameplay, Descriptions.LastChanceIndicatorDirectionDurationSeconds, Min = 0.5f, Max = 20f)]
        public static float LastChanceIndicatorDirectionDurationSeconds = 5f;

        [FeatureConfigEntry(Sections.LastChanceGameplay, Descriptions.LastChanceIndicatorDirectionCooldownSeconds, Min = 1f, Max = 60f)]
        public static float LastChanceIndicatorDirectionCooldownSeconds = 15f;

        [FeatureConfigEntry(Sections.LastChanceGameplay, Descriptions.LastChanceIndicatorDirectionPenaltyMaxSeconds, Min = 0f, Max = 30f)]
        public static float LastChanceIndicatorDirectionPenaltyMaxSeconds = 8f;

        [FeatureConfigEntry(Sections.LastChanceGameplay, Descriptions.LastChanceIndicatorDirectionPenaltyMinSeconds, Min = 0f, Max = 60f)]
        public static float LastChanceIndicatorDirectionPenaltyMinSeconds = 4f;

        [FeatureConfigEntry(Sections.LastChanceGameplay, Descriptions.LastChancePupilVisualsEnabled)]
        public static bool LastChancePupilVisualsEnabled = true;

        [FeatureConfigEntry(Sections.Spectate, Descriptions.SpectateDeadPlayers)]
        public static bool SpectateDeadPlayers = true;

        [FeatureConfigEntry(Sections.Spectate, Descriptions.SpectateDeadPlayersMode, Options = new[] { "Always", "LastChanceOnly", "Disabled" })]
        public static string SpectateDeadPlayersMode = "Always";

        [FeatureConfigEntry(Sections.Debug, Descriptions.DebugLogging, HostControlled = false)]
        public static bool DebugLogging = false;
    }
}

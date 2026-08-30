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
            public const string LastChanceExtraction = "1d. LastChance: Extraction";
            public const string Spectate = "2. Spectate";
            public const string Debug = "3. Debug";
        }

        internal static class Descriptions
        {
            public const string LastChanceMode = "When true, prevent the vanilla run manager from switching to the dump level when all players die.";
            public const string LastChanceTimerSeconds = "Static LastChance timer duration and base seconds for the dynamic difficulty floor (integer, 30s steps).";
            public const string LastChanceDynamicTimerEnabled = "Enable dynamic LastChance timer scaling from the critical Death Head return route and R.E.P.O. run difficulty.";
            public const string LastChanceTimerPerRequiredPlayerSeconds = "Extra seconds added per required player that must reach the truck.";
            public const string LastChanceLevelContextRoomWeight = "Extra multiplier weight from the critical Death Head room-path difficulty, progressively applied by R.E.P.O. run difficulty (0 disables room context).";
            public const string LastChanceLevelContextMonsterWeight = "Extra multiplier weight from active search monsters, progressively applied by R.E.P.O. run difficulty (0 disables monster context).";
            public const string LastChanceTimerPerFarthestMeterSeconds = "Extra seconds added per meter of the critical Death Head effective route to the truck.";
            public const string LastChanceTimerPerBelowTruckPlayerSeconds = "Fixed extra seconds added when the critical Death Head is below the truck threshold height.";
            public const string LastChanceTimerPerBelowTruckMeterSeconds = "Extra seconds added per meter the critical Death Head is below the configured threshold.";
            public const string LastChanceBelowTruckThresholdMeters = "Height delta threshold (DeathHeadY - truckY) below which the critical route receives vertical recovery time. -0.5 means at least half a meter below.";
            public const string LastChanceTimerPerRoomStepSeconds = "Extra seconds added per room step on the critical Death Head route to the truck.";
            public const string LastChanceDifficulty1FloorBonusSeconds = "Extra minimum timer seconds progressively granted by R.E.P.O. Difficulty 1 (levels 1-10).";
            public const string LastChanceDifficulty2FloorBonusSeconds = "Extra minimum timer seconds progressively granted by R.E.P.O. Difficulty 2 (levels 11-20).";
            public const string LastChanceDifficulty3FloorBonusSeconds = "Extra minimum timer seconds progressively granted by R.E.P.O. Difficulty 3 (levels 21-30).";
            public const string ConsolationMoneyPercent = "Percentage of the minimum first-extraction reward used as the minimum currency threshold when LastChance succeeds before any extraction is completed.";
            public const string LastChancePreserveExtractedMoney = "When true, run currency already extracted before LastChance success is preserved; consolation money remains independent.";
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
            public const string LastChanceTimerPerMonsterSeconds = "Literal extra seconds added per active spawned monster when LastChanceMonstersSearch is enabled.";
            public const string SpectateDeadPlayers = "Allow SpectateCamera to cycle through disabled players (dead bodies) when toggling targets.";
            public const string SpectateDeadPlayersMode = "Dead-player spectate during active LastChance: LastChanceOnly enables cycling, Disabled forces local DeathHead spectate, Always is a legacy alias now scoped to active LastChance only.";
            public const string LastChanceSpectateDefaultFov = "Field of view enforced only during active LastChance spectate. Set 0 to disable. Replaces DeathHeadHopperFix [8. Camera] DHHSpectateDefaultFov; copy legacy values manually if needed.";
            public const string DebugLogging = "Dump extra log lines that help trace LastChance logic.";
        }

        [FeatureConfigEntry(Sections.LastChanceQuick, Descriptions.LastChanceMode)]
        public static bool LastChangeMode = true;

        [FeatureConfigEntry(Sections.LastChanceQuick, Descriptions.LastChanceTimerSeconds, Min = 30f, Max = 600f)]
        public static int LastChanceTimerSeconds = 60;

        [FeatureConfigEntry(Sections.LastChanceQuick, Descriptions.LastChanceDynamicTimerEnabled)]
        public static bool LastChanceDynamicTimerEnabled = true;

        [FeatureConfigEntry(Sections.LastChanceQuick, Descriptions.LastChanceMissingPlayers, Min = 0f, Max = 32f)]
        public static int LastChanceMissingPlayers = 0;

        [FeatureConfigEntry(Sections.LastChanceQuick, Descriptions.LastChanceTimerBonusPerMonsterDeathSeconds, Min = 0f, Max = 60f)]
        public static int LastChanceTimerBonusPerMonsterDeathSeconds = 15;

        [FeatureConfigEntry(Sections.LastChanceQuick, Descriptions.LastChanceMonstersSearchEnabled)]
        public static bool LastChanceMonstersSearchEnabled = true;

        [FeatureConfigEntry(Sections.LastChanceQuick, Descriptions.LastChanceMonstersVoiceEnemyOnlyEnabled)]
        public static bool LastChanceMonstersVoiceEnemyOnlyEnabled = true;

        [FeatureConfigEntry(Sections.LastChanceExtraction, Descriptions.ConsolationMoneyPercent, Min = 0f, Max = 500f)]
        public static int ConsolationMoneyPercent = 100;

        [FeatureConfigEntry(Sections.LastChanceExtraction, Descriptions.LastChancePreserveExtractedMoney)]
        public static bool LastChancePreserveExtractedMoney = true;

        [FeatureConfigEntry(Sections.LastChanceTimer, Descriptions.LastChanceDifficulty1FloorBonusSeconds, Min = 0f, Max = 180f)]
        public static int LastChanceDifficulty1FloorBonusSeconds = 60;

        [FeatureConfigEntry(Sections.LastChanceTimer, Descriptions.LastChanceDifficulty2FloorBonusSeconds, Min = 0f, Max = 180f)]
        public static int LastChanceDifficulty2FloorBonusSeconds = 45;

        [FeatureConfigEntry(Sections.LastChanceTimer, Descriptions.LastChanceDifficulty3FloorBonusSeconds, Min = 0f, Max = 180f)]
        public static int LastChanceDifficulty3FloorBonusSeconds = 45;

        [FeatureConfigEntry(Sections.LastChanceTimer, Descriptions.LastChanceTimerPerMonsterSeconds, Min = 0f, Max = 15f)]
        public static float LastChanceTimerPerMonsterSeconds = 3f;

        [FeatureConfigEntry(Sections.LastChanceTimer, Descriptions.LastChanceTimerPerRequiredPlayerSeconds, Min = 0f, Max = 30f)]
        public static float LastChanceTimerPerRequiredPlayerSeconds = 8f;

        [FeatureConfigEntry(Sections.LastChanceTimer, Descriptions.LastChanceLevelContextRoomWeight, Min = 0f, Max = 1f)]
        public static float LastChanceLevelContextRoomWeight = 0.5f;

        [FeatureConfigEntry(Sections.LastChanceTimer, Descriptions.LastChanceLevelContextMonsterWeight, Min = 0f, Max = 1f)]
        public static float LastChanceLevelContextMonsterWeight = 0.3f;

        [FeatureConfigEntry(Sections.LastChanceTimer, Descriptions.LastChanceTimerPerFarthestMeterSeconds, Min = 0f, Max = 3f)]
        public static float LastChanceTimerPerFarthestMeterSeconds = 0.6f;

        [FeatureConfigEntry(Sections.LastChanceTimer, Descriptions.LastChanceTimerPerRoomStepSeconds, Min = 0f, Max = 15f)]
        public static float LastChanceTimerPerRoomStepSeconds = 3f;

        [FeatureConfigEntry(Sections.LastChanceTimer, Descriptions.LastChanceTimerPerBelowTruckPlayerSeconds, Min = 0f, Max = 60f)]
        public static float LastChanceTimerPerBelowTruckPlayerSeconds = 15f;

        [FeatureConfigEntry(Sections.LastChanceTimer, Descriptions.LastChanceTimerPerBelowTruckMeterSeconds, Min = 0f, Max = 60f)]
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

        [FeatureConfigEntry(Sections.LastChanceGameplay, Descriptions.LastChanceIndicatorDirectionPenaltyMinSeconds, Min = 0f, Max = 30f)]
        public static float LastChanceIndicatorDirectionPenaltyMinSeconds = 4f;

        [FeatureConfigEntry(Sections.LastChanceGameplay, Descriptions.LastChancePupilVisualsEnabled)]
        public static bool LastChancePupilVisualsEnabled = true;

        [FeatureConfigEntry(Sections.Spectate, Descriptions.SpectateDeadPlayers)]
        public static bool SpectateDeadPlayers = true;

        [FeatureConfigEntry(Sections.Spectate, Descriptions.SpectateDeadPlayersMode, Options = new[] { "Always", "LastChanceOnly", "Disabled" })]
        public static string SpectateDeadPlayersMode = "Always";

        [FeatureConfigEntry(Sections.Spectate, Descriptions.LastChanceSpectateDefaultFov, Min = 0f, Max = 120f, HostControlled = false)]
        public static int LastChanceSpectateDefaultFov = 70;

        [FeatureConfigEntry(Sections.Debug, Descriptions.DebugLogging, HostControlled = false)]
        public static bool DebugLogging = false;
    }
}

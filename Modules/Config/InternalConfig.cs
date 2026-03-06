#nullable enable

namespace DHHFLastChanceMode.Modules.Config
{
    // Internal runtime tunables (not exposed via BepInEx).
    internal static class InternalConfig
    {
        internal static bool LastChanceMonstersForceCameraOnLock = true;
        internal static float LastChanceMonstersCameraLockMaxSeconds = 3f;
        internal static float LastChanceMonstersCameraLockCooldownSeconds = 10f;
        internal static float LastChanceMonstersCameraLockKeepAliveGraceSeconds = 0.6f;
        internal static float LastChanceMonstersVisionLockSourceBucketSize = 1f;
        internal static bool LastChanceMonstersVisionProxyEnabled = true;
        internal static float LastChanceMonstersVisionProxyTickSeconds = 0.25f;
        internal static float LastChanceMonstersHeadmanDeathHeadFocusMaxSeconds = 3f;
        internal static float LastChanceMonstersHeadmanDeathHeadFocusCooldownSeconds = 5f;

        internal static float CompatibilityGatePresenceRetrySeconds = 1f;
        internal static float CompatibilityGatePresenceTimeoutSeconds = 5f;
        internal static bool CompatibilityGatePresencePollingEnabled = true;

        // LastChance timer pulse duration (seconds) when timer changes by a discrete event (+/-).
        internal static float LastChanceTimerChangePulseDurationSeconds = 0.4f;
        // LastChance timer pulse scale boost (0.2 => +20% at peak).
        internal static float LastChanceTimerChangePulseScaleBoost = 0.22f;
        // LastChance floating delta text duration (seconds). Increase to keep text visible longer.
        internal static float LastChanceTimerChangeFloatingDurationSeconds = 1.1f;
        // Floating delta vertical travel distance as multiplier of timer font size.
        internal static float LastChanceTimerChangeFloatingDropFontMultiplier = 2f;
        // Floating delta font size multiplier relative to timer font size (2 => double size).
        internal static float LastChanceTimerChangeFloatingFontSizeMultiplier = 2f;
        // Minimum absolute delta (seconds) to show a local delta effect.
        internal static float LastChanceTimerChangeLocalDeltaMinSeconds = 0.05f;
        // Minimum absolute delta (seconds) to show a network-synced delta effect.
        internal static float LastChanceTimerChangeNetworkDeltaMinSeconds = 0.75f;
    }
}

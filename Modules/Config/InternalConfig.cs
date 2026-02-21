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
    }
}

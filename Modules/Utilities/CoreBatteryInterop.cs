#nullable enable

using DeathHeadHopperFix.Modules.Config;

namespace DHHFLastChanceMode.Modules.Utilities
{
    internal static class CoreBatteryInterop
    {
        private const string BatteryJumpEnabledKey = "BatteryJumpEnabled";

        internal static bool TryGetBatteryJumpEnabled(out bool value)
        {
            value = FeatureFlags.BatteryJumpEnabled;
            return true;
        }

        internal static bool TrySetBatteryJumpEnabled(bool value)
        {
            FeatureFlags.BatteryJumpEnabled = value;
            return true;
        }

        internal static void SetCoreHostRuntimeOverride(bool enabled)
        {
            ConfigManager.SetHostRuntimeOverride(BatteryJumpEnabledKey, enabled ? bool.TrueString : bool.FalseString);
        }

        internal static void ClearCoreHostRuntimeOverride()
        {
            ConfigManager.ClearHostRuntimeOverride(BatteryJumpEnabledKey);
        }

        internal static void RequestCoreConfigSyncBroadcast()
        {
            ConfigSyncManager.RequestHostSnapshotBroadcast();
        }
    }
}

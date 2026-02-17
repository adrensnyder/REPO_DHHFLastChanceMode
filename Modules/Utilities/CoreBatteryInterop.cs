#nullable enable

using System;
using System.Reflection;

namespace DHHFLastChanceMode.Modules.Utilities
{
    internal static class CoreBatteryInterop
    {
        private const BindingFlags StaticAny = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const string BatteryJumpEnabledKey = "BatteryJumpEnabled";

        private static Type? s_coreFeatureFlagsType;
        private static Type? s_coreConfigManagerType;
        private static Type? s_coreConfigSyncManagerType;

        private static FieldInfo? s_coreBatteryJumpEnabledField;
        private static MethodInfo? s_setHostRuntimeOverrideMethod;
        private static MethodInfo? s_clearHostRuntimeOverrideMethod;
        private static MethodInfo? s_requestHostSnapshotBroadcastMethod;

        internal static bool TryGetBatteryJumpEnabled(out bool value)
        {
            value = false;
            ResolveMembers();
            if (s_coreBatteryJumpEnabledField == null)
            {
                return false;
            }

            value = s_coreBatteryJumpEnabledField.GetValue(null) as bool? ?? false;
            return true;
        }

        internal static bool TrySetBatteryJumpEnabled(bool value)
        {
            ResolveMembers();
            if (s_coreBatteryJumpEnabledField == null)
            {
                return false;
            }

            s_coreBatteryJumpEnabledField.SetValue(null, value);
            return true;
        }

        internal static void SetCoreHostRuntimeOverride(bool enabled)
        {
            ResolveMembers();
            s_setHostRuntimeOverrideMethod?.Invoke(null, new object[] { BatteryJumpEnabledKey, enabled ? bool.TrueString : bool.FalseString });
        }

        internal static void ClearCoreHostRuntimeOverride()
        {
            ResolveMembers();
            s_clearHostRuntimeOverrideMethod?.Invoke(null, new object[] { BatteryJumpEnabledKey });
        }

        internal static void RequestCoreConfigSyncBroadcast()
        {
            ResolveMembers();
            s_requestHostSnapshotBroadcastMethod?.Invoke(null, null);
        }

        private static void ResolveMembers()
        {
            s_coreFeatureFlagsType ??= ResolveType("DeathHeadHopperFix.Modules.Config.FeatureFlags");
            s_coreConfigManagerType ??= ResolveType("DeathHeadHopperFix.Modules.Config.ConfigManager");
            s_coreConfigSyncManagerType ??= ResolveType("DeathHeadHopperFix.Modules.Config.ConfigSyncManager");

            s_coreBatteryJumpEnabledField ??= s_coreFeatureFlagsType?.GetField(BatteryJumpEnabledKey, StaticAny);
            s_setHostRuntimeOverrideMethod ??= s_coreConfigManagerType?.GetMethod("SetHostRuntimeOverride", StaticAny);
            s_clearHostRuntimeOverrideMethod ??= s_coreConfigManagerType?.GetMethod("ClearHostRuntimeOverride", StaticAny);
            s_requestHostSnapshotBroadcastMethod ??= s_coreConfigSyncManagerType?.GetMethod("RequestHostSnapshotBroadcast", StaticAny);
        }

        private static Type? ResolveType(string fullName)
        {
            var type = Type.GetType(fullName, throwOnError: false);
            if (type != null)
            {
                return type;
            }

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = asm.GetType(fullName, throwOnError: false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}

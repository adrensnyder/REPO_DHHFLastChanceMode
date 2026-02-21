#nullable enable

using DHHFLastChanceMode.Modules.Config;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Interactions.Debugging
{
    internal static class LastChanceMonstersDebugGate
    {
        internal static bool IsEnabled(bool flowFlag)
        {
            return InternalDebugFlags.DebugLastChanceMonsterDebugPatchesEnabled && flowFlag;
        }

        internal static bool IsVerbose(bool verboseFlag)
        {
            return InternalDebugFlags.DebugLastChanceMonsterDebugPatchesEnabled && verboseFlag;
        }
    }
}

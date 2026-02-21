#nullable enable

using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Adapters;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Support
{
    internal static class LastChanceMonstersDisabledGateHelper
    {
        internal static bool ShouldTreatDisabledAsActive(PlayerAvatar? player)
        {
            return player != null &&
                   LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled() &&
                   LastChanceMonstersTargetProxyHelper.IsHeadProxyActive(player);
        }
    }
}

#nullable enable

using System.Reflection;
using DHHFLastChanceMode.Modules.Config;
using HarmonyLib;

namespace DeathHeadHopperFix.Modules.Gameplay.LastChance.Runtime
{
    internal static class LastChancePupilGate
    {
        private static readonly FieldInfo? HeadTriggeredField = AccessTools.Field(typeof(PlayerDeathHead), "triggered");

        internal static bool IsEnabled()
        {
            return FeatureFlags.LastChancePupilVisualsEnabled && LastChanceTimerController.IsActive;
        }

        internal static bool TryGetEligibleHead(PlayerAvatar? player, out PlayerDeathHead? head, out string reason)
        {
            head = null;

            if (player == null)
            {
                reason = "NoPlayer";
                return false;
            }

            if (!IsEnabled())
            {
                reason = "GateDisabled";
                return false;
            }

            if (!LastChanceMonstersTargetProxyHelper.IsHeadProxyActive(player))
            {
                reason = "NoHeadProxy";
                return false;
            }

            head = player.playerDeathHead;
            if (head == null)
            {
                reason = "NoHead";
                return false;
            }

            if (!(HeadTriggeredField?.GetValue(head) as bool? ?? false))
            {
                reason = "NotTriggered";
                return false;
            }

            reason = "Allowed";
            return true;
        }
    }
}

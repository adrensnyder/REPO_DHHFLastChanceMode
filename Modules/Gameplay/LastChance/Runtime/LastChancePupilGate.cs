#nullable enable

using DHHFLastChanceMode.Modules.Config;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Runtime
{
    internal static class LastChancePupilGate
    {
        internal static bool IsEnabled()
        {
            return FeatureFlags.LastChancePupilVisualsEnabled && LastChanceRuntimeOrchestrator.IsRuntimeActive;
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

            if (!head.triggered)
            {
                reason = "NotTriggered";
                return false;
            }

            reason = "Allowed";
            return true;
        }
    }
}

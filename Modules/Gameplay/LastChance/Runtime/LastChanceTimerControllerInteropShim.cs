#nullable enable

using DHHFLastChanceTimerController = DHHFLastChanceMode.Modules.Gameplay.LastChance.Runtime.LastChanceTimerController;

namespace DeathHeadHopperFix.Modules.Gameplay.LastChance.Runtime
{
    internal static class LastChanceTimerController
    {
        internal static bool IsActive => DHHFLastChanceTimerController.IsActive;

        internal static bool IsDirectionIndicatorUiVisible => DHHFLastChanceTimerController.IsDirectionIndicatorUiVisible;

        internal static float GetDirectionIndicatorPenaltySecondsPreview() =>
            DHHFLastChanceTimerController.GetDirectionIndicatorPenaltySecondsPreview();

        internal static bool IsDirectionIndicatorEnergySufficientPreview() =>
            DHHFLastChanceTimerController.IsDirectionIndicatorEnergySufficientPreview();

        internal static void GetDirectionIndicatorEnergyDebugSnapshot(
            out bool visible,
            out float timerRemaining,
            out float penaltyPreview,
            out bool hasEnoughEnergy) =>
            DHHFLastChanceTimerController.GetDirectionIndicatorEnergyDebugSnapshot(
                out visible,
                out timerRemaining,
                out penaltyPreview,
                out hasEnoughEnergy);

        internal static bool IsPlayerSurrenderedForData(PlayerAvatar? player) =>
            DHHFLastChanceTimerController.IsPlayerSurrenderedForData(player);
    }
}

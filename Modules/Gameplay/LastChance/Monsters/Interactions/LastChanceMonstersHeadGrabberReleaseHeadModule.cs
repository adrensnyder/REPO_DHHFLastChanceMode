#nullable enable

using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Adapters;
using HarmonyLib;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Interactions
{
    [HarmonyPatch(typeof(EnemyHeadGrabber), nameof(EnemyHeadGrabber.StateGrabHead))]
    internal static class LastChanceMonstersHeadGrabberReleaseHeadModule
    {
        [HarmonyPostfix]
        private static void Postfix(EnemyHeadGrabber __instance)
        {
            if (__instance == null || !LastChanceMonstersTargetProxyHelper.IsRuntimeMasterContextEnabled())
            {
                return;
            }

            var headTarget = __instance.headTarget;
            if (!__instance.headTargetActive || headTarget == null)
            {
                return;
            }

            var player = headTarget.playerAvatar;
            if (player == null || !LastChanceMonstersTargetProxyHelper.IsHeadProxyActive(player))
            {
                return;
            }

            __instance.DeathHeadRelease();
            __instance.nearbyHeadLogic = false;

            if (__instance.playerTarget == null)
            {
                __instance.UpdatePlayerTarget(player);
            }

            if (__instance.currentState == EnemyHeadGrabber.State.GrabHead ||
                __instance.currentState == EnemyHeadGrabber.State.GotoHead ||
                __instance.currentState == EnemyHeadGrabber.State.GotoHeadOver)
            {
                __instance.UpdateState(EnemyHeadGrabber.State.GotoPlayer);
            }
        }
    }
}

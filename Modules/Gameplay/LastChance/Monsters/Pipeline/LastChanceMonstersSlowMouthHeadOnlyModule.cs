#nullable enable

using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Support;
using HarmonyLib;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Pipeline
{
    [HarmonyPatch]
    internal static class LastChanceMonstersSlowMouthHeadOnlyModule
    {
        [HarmonyPatch(typeof(EnemySlowMouth), nameof(EnemySlowMouth.UpdateState))]
        [HarmonyPrefix]
        private static void EnemySlowMouthUpdateStatePrefix(EnemySlowMouth __instance, ref EnemySlowMouth.State newState)
        {
            if (!ShouldUseHeadOnlyBehavior(__instance))
            {
                return;
            }

            if (newState == EnemySlowMouth.State.Attack ||
                newState == EnemySlowMouth.State.Attached ||
                newState == EnemySlowMouth.State.Puke ||
                newState == EnemySlowMouth.State.Detach ||
                newState == EnemySlowMouth.State.IdlePuke)
            {
                newState = EnemySlowMouth.State.GoToPlayer;
            }
        }

        [HarmonyPatch(typeof(EnemySlowMouth), nameof(EnemySlowMouth.UpdateStateRPC))]
        [HarmonyPrefix]
        private static void EnemySlowMouthUpdateStateRpcPrefix(EnemySlowMouth __instance, ref EnemySlowMouth.State newState)
        {
            if (!ShouldUseHeadOnlyBehavior(__instance))
            {
                return;
            }

            if (newState == EnemySlowMouth.State.Attack ||
                newState == EnemySlowMouth.State.Attached ||
                newState == EnemySlowMouth.State.Puke ||
                newState == EnemySlowMouth.State.Detach ||
                newState == EnemySlowMouth.State.IdlePuke)
            {
                newState = EnemySlowMouth.State.GoToPlayer;
            }
        }

        [HarmonyPatch(typeof(EnemySlowMouth), nameof(EnemySlowMouth.StateAttack), new[] { typeof(bool) })]
        [HarmonyPrefix]
        private static bool EnemySlowMouthStateAttackPrefix(EnemySlowMouth __instance)
        {
            if (!ShouldUseHeadOnlyBehavior(__instance))
            {
                return true;
            }

            __instance.UpdateState(EnemySlowMouth.State.GoToPlayer);
            return false;
        }

        [HarmonyPatch(typeof(EnemySlowMouth), nameof(EnemySlowMouth.StateAttached), new[] { typeof(bool) })]
        [HarmonyPrefix]
        private static bool EnemySlowMouthStateAttachedPrefix(EnemySlowMouth __instance)
        {
            if (!ShouldUseHeadOnlyBehavior(__instance))
            {
                return true;
            }

            __instance.UpdateState(EnemySlowMouth.State.GoToPlayer);
            return false;
        }

        [HarmonyPatch(typeof(EnemySlowMouth), nameof(EnemySlowMouth.StatePuke), new[] { typeof(bool) })]
        [HarmonyPrefix]
        private static bool EnemySlowMouthStatePukePrefix(EnemySlowMouth __instance)
        {
            if (!ShouldUseHeadOnlyBehavior(__instance))
            {
                return true;
            }

            __instance.UpdateState(EnemySlowMouth.State.GoToPlayer);
            return false;
        }

        [HarmonyPatch(typeof(EnemySlowMouth), nameof(EnemySlowMouth.StateDetach), new[] { typeof(bool) })]
        [HarmonyPrefix]
        private static bool EnemySlowMouthStateDetachPrefix(EnemySlowMouth __instance)
        {
            if (!ShouldUseHeadOnlyBehavior(__instance))
            {
                return true;
            }

            __instance.UpdateState(EnemySlowMouth.State.GoToPlayer);
            return false;
        }

        [HarmonyPatch(typeof(EnemySlowMouth), nameof(EnemySlowMouth.StateIdlePuke), new[] { typeof(bool) })]
        [HarmonyPrefix]
        private static bool EnemySlowMouthStateIdlePukePrefix(EnemySlowMouth __instance)
        {
            if (!ShouldUseHeadOnlyBehavior(__instance))
            {
                return true;
            }

            __instance.UpdateState(EnemySlowMouth.State.GoToPlayer);
            return false;
        }

        [HarmonyPatch(typeof(EnemySlowMouthAttaching), nameof(EnemySlowMouthAttaching.Update))]
        [HarmonyPrefix]
        private static bool EnemySlowMouthAttachingUpdatePrefix(EnemySlowMouthAttaching __instance)
        {
            if (__instance == null || !ShouldUseHeadOnlyBehavior(__instance.enemySlowMouth))
            {
                return true;
            }

            __instance.Detach();
            return false;
        }

        [HarmonyPatch(typeof(EnemySlowMouthAttaching), nameof(EnemySlowMouthAttaching.AttachToPlayer))]
        [HarmonyPrefix]
        private static bool EnemySlowMouthAttachingAttachToPlayerPrefix(EnemySlowMouthAttaching __instance)
        {
            return !ShouldUseHeadOnlyBehavior(__instance?.enemySlowMouth);
        }

        private static bool ShouldUseHeadOnlyBehavior(EnemySlowMouth? slowMouth)
        {
            if (slowMouth == null)
            {
                return false;
            }

            if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
            {
                return false;
            }

            var player = slowMouth.playerTarget;
            if (player == null)
            {
                return false;
            }

            return LastChanceMonstersDisabledGateHelper.ShouldTreatDisabledAsActive(player);
        }
    }
}

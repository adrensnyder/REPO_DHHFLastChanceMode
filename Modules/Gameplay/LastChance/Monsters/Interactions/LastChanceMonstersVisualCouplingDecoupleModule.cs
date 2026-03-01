#nullable enable

using System;
using HarmonyLib;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Interactions
{
    internal static class LastChanceMonstersVisualCouplingDecoupleModule
    {
        [HarmonyPatch(typeof(EnemyHeartHugger), nameof(EnemyHeartHugger.PlayersInGasLogic))]
        internal static class EnemyHeartHuggerPlayersInGasLogicPatch
        {
            [HarmonyFinalizer]
            private static Exception? Finalizer(Exception? __exception)
            {
                if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
                {
                    return __exception;
                }

                // During LastChance runtime, null tumble-wing visuals are tolerated.
                if (__exception is NullReferenceException)
                {
                    return null;
                }

                return __exception;
            }
        }

        [HarmonyPatch(typeof(EnemyHeartHuggerGasChecker), nameof(EnemyHeartHuggerGasChecker.Update))]
        internal static class EnemyHeartHuggerGasCheckerUpdatePatch
        {
            [HarmonyFinalizer]
            private static Exception? Finalizer(Exception? __exception)
            {
                if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
                {
                    return __exception;
                }

                // During LastChance runtime, null tumble-wing visuals are tolerated.
                if (__exception is NullReferenceException)
                {
                    return null;
                }

                return __exception;
            }
        }

        [HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.UpgradeTumbleWingsVisualsActive), new[] { typeof(bool), typeof(bool) })]
        internal static class PlayerAvatarUpgradeTumbleWingsVisualsActivePatch
        {
            [HarmonyPrefix]
            private static bool Prefix(PlayerAvatar? __instance)
            {
                if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
                {
                    return true;
                }

                if (__instance == null)
                {
                    return false;
                }

                return __instance.upgradeTumbleWingsLogic != null;
            }
        }
    }
}

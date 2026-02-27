#nullable enable

using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Adapters;
using HarmonyLib;
using UnityEngine;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Interactions
{
    [HarmonyPatch]
    internal static class LastChanceMonstersBeamerHeadAimModule
    {
        [HarmonyPatch(typeof(EnemyBeamer), "StateAttackStart")]
        [HarmonyPostfix]
        private static void StateAttackStartPostfix(EnemyBeamer __instance)
        {
            if (!TryGetAimContext(__instance, out var enemy, out _, out var targetPoint))
            {
                return;
            }

            var from = enemy.CenterTransform != null ? enemy.CenterTransform.position : enemy.transform.position;
            var dir = targetPoint - from;
            if (dir.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var aim = Quaternion.LookRotation(dir);
            aim = Quaternion.Euler(0f, aim.eulerAngles.y, 0f);
            __instance.aimHorizontalTarget = aim;
        }

        [HarmonyPatch(typeof(EnemyBeamer), "VerticalAimLogic")]
        [HarmonyPostfix]
        private static void VerticalAimLogicPostfix(EnemyBeamer __instance)
        {
            if (!TryGetAimContext(__instance, out _, out _, out var targetPoint))
            {
                return;
            }

            var laserRayTransform = __instance.laserRayTransform;
            if (laserRayTransform == null)
            {
                return;
            }

            var aimVerticalTransform = __instance.aimVerticalTransform;
            var dir = targetPoint - laserRayTransform.position;
            if (dir.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var worldAim = Quaternion.LookRotation(dir);
            var currentRotation = laserRayTransform.rotation;
            laserRayTransform.rotation = worldAim;
            var localAim = laserRayTransform.localRotation;
            localAim = Quaternion.Euler(laserRayTransform.eulerAngles.x, 0f, 0f);
            laserRayTransform.rotation = currentRotation;

            __instance.aimVerticalTarget = localAim;
            laserRayTransform.localRotation = localAim;
            if (aimVerticalTransform != null)
            {
                aimVerticalTransform.localRotation = localAim;
            }
        }

        private static bool TryGetAimContext(EnemyBeamer beamer, out Enemy enemy, out PlayerAvatar player, out Vector3 targetPoint)
        {
            enemy = null!;
            player = null!;
            targetPoint = default;

            if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
            {
                return false;
            }

            var enemyValue = beamer.enemy;
            var playerValue = beamer.playerTarget;
            if (enemyValue == null || playerValue == null)
            {
                return false;
            }

            enemy = enemyValue;
            player = playerValue;

            if (!LastChanceMonstersTargetProxyHelper.TryGetHeadProxyVisionTarget(player, out targetPoint))
            {
                return false;
            }

            return true;
        }
    }
}

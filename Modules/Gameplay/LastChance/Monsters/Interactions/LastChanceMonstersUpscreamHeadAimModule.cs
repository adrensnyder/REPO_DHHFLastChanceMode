#nullable enable

using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Adapters;
using HarmonyLib;
using UnityEngine;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Interactions
{
    [HarmonyPatch]
    internal static class LastChanceMonstersUpscreamHeadAimModule
    {
        [HarmonyPatch(typeof(EnemyUpscreamAnim), nameof(EnemyUpscreamAnim.AttackImpulse))]
        [HarmonyPrefix]
        private static bool AttackImpulsePrefix(EnemyUpscreamAnim __instance)
        {
            if (__instance == null || !LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
            {
                return true;
            }

            var player = __instance.controller != null ? __instance.controller.targetPlayer : null;
            if (player == null || !LastChanceMonstersTargetProxyHelper.IsHeadProxyActive(player))
            {
                return true;
            }

            if (SemiFunc.IsMultiplayer() && !SemiFunc.IsMasterClient())
            {
                return false;
            }

            if (!LastChanceMonstersTargetProxyHelper.TryGetHeadProxyPhysGrabObject(player, out var headPhys) || headPhys?.rb == null)
            {
                return true;
            }

            var from = __instance.transform.position;
            var direction = headPhys.centerPoint - from;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = player.transform.position - from;
            }

            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = __instance.transform.forward;
            }

            var launch = Vector3.Lerp(direction.normalized, Vector3.up, 0.6f);
            headPhys.rb.AddForce(launch * 45f, ForceMode.Impulse);
            headPhys.rb.AddTorque(-player.transform.right * 45f, ForceMode.Impulse);
            return false;
        }

        [HarmonyPatch(typeof(EnemyUpscream), nameof(EnemyUpscream.StateGoToPlayer))]
        [HarmonyPostfix]
        private static void StateGoToPlayerPostfix(EnemyUpscream __instance)
        {
            if (!TryGetContext(__instance, out var targetPoint))
            {
                return;
            }

            var enemy = __instance.enemy;
            if (enemy == null)
            {
                return;
            }

            if (!enemy.Jump.jumping)
            {
                enemy.NavMeshAgent.SetDestination(targetPoint);
            }

            if (Vector3.Distance(enemy.Rigidbody.transform.position, targetPoint) >= 1.5f || enemy.Jump.jumping || enemy.IsStunned())
            {
                return;
            }

            enemy.NavMeshAgent.ResetPath();
            SemiFunc.EnemyCartJumpReset(enemy);
            __instance.UpdateState(EnemyUpscream.State.Attack);
        }

        [HarmonyPatch(typeof(EnemyUpscream), nameof(EnemyUpscream.StateAttack))]
        [HarmonyPostfix]
        private static void StateAttackPostfix(EnemyUpscream __instance)
        {
            if (!TryGetContext(__instance, out var targetPoint))
            {
                return;
            }

            var enemy = __instance.enemy;
            if (enemy == null)
            {
                return;
            }

            var from = enemy.Rigidbody.transform.position;
            var dir = targetPoint - from;
            if (dir.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var yaw = Quaternion.Euler(0f, Quaternion.LookRotation(dir).eulerAngles.y, 0f);
            __instance.transform.rotation = Quaternion.Slerp(__instance.transform.rotation, yaw, 50f * Time.deltaTime);
        }

        [HarmonyPatch(typeof(EnemyUpscream), nameof(EnemyUpscream.HeadLogic))]
        [HarmonyPostfix]
        private static void HeadLogicPostfix(EnemyUpscream __instance)
        {
            if (!TryGetContext(__instance, out var targetPoint))
            {
                return;
            }

            var headTransform = __instance.headTransform;
            if (headTransform == null)
            {
                return;
            }

            var direction = targetPoint - headTransform.position;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            headTransform.rotation = Quaternion.LookRotation(direction);
        }

        private static bool TryGetContext(EnemyUpscream? upscream, out Vector3 targetPoint)
        {
            targetPoint = default;
            if (upscream == null || !LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
            {
                return false;
            }

            var player = upscream.targetPlayer;
            if (player == null)
            {
                return false;
            }

            return LastChanceMonstersTargetProxyHelper.TryGetHeadProxyVisionTarget(player, out targetPoint) ||
                   LastChanceMonstersTargetProxyHelper.TryGetHeadProxyTarget(player, out targetPoint);
        }
    }
}

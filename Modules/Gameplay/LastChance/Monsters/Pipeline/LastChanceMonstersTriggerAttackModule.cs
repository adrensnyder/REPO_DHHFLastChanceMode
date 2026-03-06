#nullable enable

using HarmonyLib;
using UnityEngine;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Pipeline
{
    [HarmonyPatch(typeof(EnemyTriggerAttack), nameof(EnemyTriggerAttack.OnTriggerStay))]
    internal static class LastChanceMonstersTriggerAttackModule
    {
        [HarmonyPrefix]
        private static bool OnTriggerStayPrefix(EnemyTriggerAttack __instance, Collider other)
        {
            if (__instance == null || other == null)
            {
                return true;
            }

            if (!LastChanceMonstersTargetProxyHelper.IsRuntimeMasterContextEnabled())
            {
                return true;
            }

            // Keep vanilla path for regular body trigger.
            if (other.GetComponent<PlayerTrigger>() != null)
            {
                return true;
            }

            if (!LastChanceMonstersTargetProxyHelper.TryGetPlayerFromDeathHeadCollider(other, out var player) || player == null)
            {
                return true;
            }

            if (!LastChanceMonstersTargetProxyHelper.TryGetHeadProxyTarget(player, out var headCenter))
            {
                return true;
            }

            if (!LevelGenerator.Instance.Generated)
            {
                return false;
            }

            var timer = __instance.TriggerCheckTimer;
            if (timer > 0f)
            {
                return false;
            }

            __instance.TriggerCheckTimerSet = true;

            var enemy = __instance.Enemy;
            if (enemy == null)
            {
                return false;
            }

            if (enemy.GetComponent<EnemyAnimal>() != null)
            {
                __instance.Attack = true;
                return false;
            }

            if (enemy.CurrentState != EnemyState.Chase && enemy.CurrentState != EnemyState.LookUnder)
            {
                return false;
            }

            var lookUnder = enemy.StateLookUnder;
            var chase = enemy.StateChase;
            var vision = enemy.Vision;
            var lookUnderReady = enemy.CurrentState == EnemyState.LookUnder &&
                                 lookUnder != null &&
                                 lookUnder.WaitDone;
            var chaseCanReach = chase != null && chase.ChaseCanReach;

            var viewId = player.photonView.ViewID;
            var visionTriggered = vision != null && vision.VisionTriggered.TryGetValue(viewId, out var triggered) && triggered;
            if (!visionTriggered)
            {
                var near = vision != null && Vector3.Distance(__instance.VisionTransform.position, headCenter) <= vision.VisionDistanceClose;
                if (vision != null)
                {
                    LastChanceMonstersTargetProxyHelper.EnsureVisionTriggered(vision, player, near);
                }
                visionTriggered = true;
            }

            if (!visionTriggered && !lookUnderReady)
            {
                return false;
            }

            var allowAttack = chaseCanReach && !lookUnderReady;
            var fallbackCanAttack = !chaseCanReach || lookUnderReady;

            var blocked = !LastChanceMonstersTargetProxyHelper.IsLineOfSightToHead(__instance.VisionTransform, headCenter, __instance.VisionMask, player);
            if (blocked)
            {
                if (!fallbackCanAttack)
                {
                    allowAttack = false;
                }
            }
            else if (fallbackCanAttack)
            {
                allowAttack = true;
            }

            if (allowAttack)
            {
                __instance.Attack = true;
            }

            return false;
        }
    }
}


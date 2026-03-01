#nullable enable

using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Adapters;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Interactions
{
    [HarmonyPatch]
    internal static class LastChanceMonstersCarryTargetPositionModule
    {
        [HarmonyPatch(typeof(EnemyHidden), nameof(EnemyHidden.StatePlayerGoTo))]
        internal static class EnemyHiddenStatePlayerGoToPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(EnemyHidden __instance)
            {
                if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
                {
                    return true;
                }

                ExecuteStatePlayerGoTo(__instance);
                return false;
            }
        }

        [HarmonyPatch(typeof(EnemyHidden), nameof(EnemyHidden.StatePlayerMove))]
        internal static class EnemyHiddenStatePlayerMovePatch
        {
            [HarmonyPrefix]
            private static bool Prefix(EnemyHidden __instance)
            {
                if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
                {
                    return true;
                }

                ExecuteStatePlayerMove(__instance);
                return false;
            }
        }

        [HarmonyPatch(typeof(EnemyHidden), nameof(EnemyHidden.StatePlayerRelease))]
        internal static class EnemyHiddenStatePlayerReleasePatch
        {
            [HarmonyPrefix]
            private static bool Prefix(EnemyHidden __instance)
            {
                if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
                {
                    return true;
                }

                ExecuteStatePlayerRelease(__instance);
                return false;
            }
        }

        private static void ExecuteStatePlayerGoTo(EnemyHidden hidden)
        {
            if (hidden.stateImpulse)
            {
                hidden.stateImpulse = false;
                hidden.stateTimer = 2f;
                hidden.agentSet = true;
            }

            hidden.stateTimer -= Time.deltaTime;
            var player = hidden.playerTarget;
            if (player == null || player.isDisabled || hidden.stateTimer <= 0f)
            {
                hidden.UpdateState(EnemyHidden.State.Leave);
                return;
            }

            var targetPosition = GetEffectivePlayerTargetPosition(player);
            SemiFunc.EnemyCartJump(hidden.enemy);
            if (hidden.enemy.Jump.jumping)
            {
                hidden.enemy.NavMeshAgent.Disable(0.5f);
                hidden.transform.position = Vector3.MoveTowards(hidden.transform.position, targetPosition, 5f * Time.deltaTime);
                hidden.agentSet = true;
            }
            else if (!hidden.enemy.NavMeshAgent.IsDisabled())
            {
                if (!hidden.agentSet && hidden.enemy.NavMeshAgent.HasPath() && Vector3.Distance(hidden.enemy.Rigidbody.transform.position + Vector3.down * 0.75f, hidden.enemy.NavMeshAgent.GetDestination()) < 0.25f)
                {
                    hidden.enemy.Jump.StuckTrigger(hidden.enemy.Rigidbody.transform.position - targetPosition);
                }

                hidden.enemy.NavMeshAgent.SetDestination(targetPosition);
                hidden.enemy.NavMeshAgent.OverrideAgent(5f, 10f, 0.25f);
                hidden.agentSet = false;
            }

            if (Vector3.Distance(hidden.enemy.Rigidbody.transform.position, targetPosition) < 1.5f)
            {
                SemiFunc.EnemyCartJumpReset(hidden.enemy);
                hidden.UpdateState(EnemyHidden.State.PlayerPickup);
            }
        }

        private static void ExecuteStatePlayerMove(EnemyHidden hidden)
        {
            if (hidden.stateImpulse)
            {
                hidden.stateTimer = 5f;
                hidden.maxMoveTimer = 10f;
                var foundDestination = false;
                var levelPoint = SemiFunc.LevelPointGetPlayerDistance(hidden.transform.position, 50f, 999f, false);
                if (!levelPoint)
                {
                    levelPoint = SemiFunc.LevelPointGetFurthestFromPlayer(hidden.transform.position, 5f);
                }

                if (levelPoint && NavMesh.SamplePosition(levelPoint.transform.position + Random.insideUnitSphere * 3f, out var navMeshHit, 5f, -1) && Physics.Raycast(navMeshHit.position, Vector3.down, 5f, LayerMask.GetMask("Default")))
                {
                    hidden.agentDestination = navMeshHit.position;
                    foundDestination = true;
                }

                if (!foundDestination)
                {
                    hidden.stateTimer = 0f;
                }

                hidden.stateImpulse = false;
            }

            if (hidden.enemy.Rigidbody.notMovingTimer > 2f)
            {
                hidden.stateTimer -= Time.deltaTime;
            }

            var player = hidden.playerTarget;
            if (player == null || player.isDisabled)
            {
                hidden.UpdateState(EnemyHidden.State.Leave);
                return;
            }

            var targetPosition = GetEffectivePlayerTargetPosition(player);

            SemiFunc.EnemyCartJump(hidden.enemy);
            hidden.enemy.NavMeshAgent.SetDestination(hidden.agentDestination);
            hidden.enemy.NavMeshAgent.OverrideAgent(5f, 10f, 0.25f);
            hidden.enemy.Jump.GapJumpOverride(0.1f, 20f, 20f);
            hidden.maxMoveTimer -= Time.deltaTime;

            var targetDistance = Vector3.Distance(hidden.enemy.Rigidbody.transform.position, targetPosition);
            if (!hidden.enemy.NavMeshAgent.HasPath() ||
                Vector3.Distance(hidden.transform.position, hidden.agentDestination) < 1f ||
                targetDistance > 5f ||
                hidden.stateTimer <= 0f ||
                hidden.maxMoveTimer <= 0f)
            {
                SemiFunc.EnemyCartJumpReset(hidden.enemy);
                hidden.UpdateState(EnemyHidden.State.PlayerRelease);
            }
        }

        private static void ExecuteStatePlayerRelease(EnemyHidden hidden)
        {
            if (hidden.stateImpulse)
            {
                hidden.stateImpulse = false;
                hidden.stateTimer = 2f;
            }

            hidden.stateTimer -= Time.deltaTime;
            var player = hidden.playerTarget;
            if (player == null || player.isDisabled)
            {
                hidden.UpdateState(EnemyHidden.State.Leave);
                return;
            }

            var targetPosition = GetEffectivePlayerTargetPosition(player);
            if (hidden.stateTimer <= 0f || Vector3.Distance(hidden.enemy.Rigidbody.transform.position, targetPosition) > 5f)
            {
                hidden.UpdateState(EnemyHidden.State.PlayerReleaseWait);
            }
        }

        internal static Vector3 GetEffectivePlayerTargetPosition(PlayerAvatar? player)
        {
            return LastChanceMonstersTargetProxyHelper.ResolveEffectivePlayerTargetPosition(player);
        }
    }
}

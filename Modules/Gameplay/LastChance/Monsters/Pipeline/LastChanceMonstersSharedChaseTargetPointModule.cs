#nullable enable

using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Support;
using HarmonyLib;
using UnityEngine;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Pipeline
{
    [HarmonyPatch]
    internal static class LastChanceMonstersSharedChaseTargetPointModule
    {
        [HarmonyPatch(typeof(EnemyStateChase), nameof(EnemyStateChase.Update))]
        internal static class EnemyStateChaseUpdatePatch
        {
            [HarmonyPrefix]
            private static bool Prefix(EnemyStateChase __instance)
            {
                if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
                {
                    return true;
                }

                ExecuteChaseUpdate(__instance);
                return false;
            }
        }

        [HarmonyPatch(typeof(EnemyStateChaseBegin), nameof(EnemyStateChaseBegin.Update))]
        internal static class EnemyStateChaseBeginUpdatePatch
        {
            [HarmonyPrefix]
            private static bool Prefix(EnemyStateChaseBegin __instance)
            {
                if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
                {
                    return true;
                }

                ExecuteChaseBeginUpdate(__instance);
                return false;
            }
        }

        private static void ExecuteChaseUpdate(EnemyStateChase state)
        {
            if (!state.Enemy.MasterClient)
            {
                return;
            }

            if (state.Enemy.CurrentState != EnemyState.Chase)
            {
                if (state.Active)
                {
                    state.Active = false;
                }

                return;
            }

            var targetPlayer = state.Enemy.TargetPlayerAvatar;
            if (targetPlayer == null)
            {
                state.Enemy.CurrentState = EnemyState.Roaming;
                return;
            }

            if (!state.Active)
            {
                targetPlayer.LastNavMeshPositionTimer = 0f;
                state.ChasePosition = ResolveTargetPosition(targetPlayer);
                state.VisionTimer = state.VisionTime;
                state.ChaseCanReachSet = false;
                state.SawPlayerHide = false;
                state.CantReachTime = 0f;
                state.StateTimer = Random.Range(state.StateTimeMin, state.StateTimeMax);
                state.Active = true;
            }

            state.Enemy.SetChaseTimer();
            state.Enemy.NavMeshAgent.UpdateAgent(state.Speed, state.Acceleration);
            if (state.Enemy.Vision.VisionTriggered[targetPlayer.photonView.ViewID])
            {
                state.VisionTimer = state.VisionTime;
            }
            else if (state.VisionTimer > 0f)
            {
                state.VisionTimer -= Time.deltaTime;
            }

            var effectiveTargetPosition = ResolveTargetPosition(targetPlayer);
            if (state.VisionTimer > 0f)
            {
                if (state.ChaseOnlyOnNavmesh || targetPlayer.LastNavMeshPositionTimer <= 0.25f)
                {
                    state.Enemy.NavMeshAgent.Enable();
                    state.Enemy.NavMeshAgent.SetDestination(targetPlayer.LastNavmeshPosition);
                    if (state.ChaseCanReachSet)
                    {
                        var point = state.Enemy.NavMeshAgent.GetPoint();
                        state.ChaseCanReach = Vector3.Distance(point, effectiveTargetPosition) <= 0.5f;

                        if (targetPlayer.isCrawling && !state.ChaseCanReach && SemiFunc.EnemyLookUnderCondition(state.Enemy, state.StateTimer, 5f, targetPlayer))
                        {
                            state.SawPlayerHidePosition = effectiveTargetPosition;
                            state.SawPlayerNavmeshPosition = targetPlayer.LastNavmeshPosition;
                            state.SawPlayerHide = true;
                        }

                        state.ChasePosition = point;
                    }

                    state.ChaseCanReachSet = true;
                }
                else
                {
                    state.Enemy.NavMeshAgent.Disable(0.1f);
                    state.transform.position = Vector3.MoveTowards(state.transform.position, effectiveTargetPosition, state.Speed * Time.deltaTime);
                }
            }
            else
            {
                if (state.SawPlayerHide)
                {
                    state.Enemy.CurrentState = EnemyState.LookUnder;
                    return;
                }

                state.Enemy.NavMeshAgent.SetDestination(state.ChasePosition);
                if (Vector3.Distance(state.transform.position, state.ChasePosition) < 1f)
                {
                    var levelPointAhead = state.Enemy.GetLevelPointAhead(state.ChasePosition);
                    if (levelPointAhead)
                    {
                        state.Enemy.NavMeshAgent.SetDestination(levelPointAhead.transform.position);
                    }

                    state.ChasePosition = state.Enemy.NavMeshAgent.GetDestination();
                }

                state.ChaseCanReach = true;
                state.ChaseCanReachSet = false;
            }

            if (state.ChaseCanReach && state.Enemy.Vision.VisionsTriggered[targetPlayer.photonView.ViewID] >= state.VisionsToReset)
            {
                state.StateTimer = Random.Range(state.StateTimeMin, state.StateTimeMax);
            }

            if (!state.ChaseCanReach)
            {
                state.CantReachTime += Time.deltaTime;
                if (state.CantReachTime > 2f)
                {
                    state.Enemy.Vision.VisionsTriggered[targetPlayer.photonView.ViewID] = 0;
                    state.Enemy.CurrentState = EnemyState.ChaseSlow;
                    return;
                }
            }
            else
            {
                state.CantReachTime = 0f;
            }

            state.StateTimer -= Time.deltaTime;
            if (state.StateTimer <= 0f)
            {
                state.Enemy.CurrentState = EnemyState.ChaseSlow;
            }

            if (targetPlayer.isDisabled && !LastChanceMonstersDisabledGateHelper.ShouldTreatDisabledAsActive(targetPlayer))
            {
                state.Enemy.Vision.VisionsTriggered[targetPlayer.photonView.ViewID] = 0;
                state.Enemy.CurrentState = EnemyState.Roaming;
            }
        }

        private static void ExecuteChaseBeginUpdate(EnemyStateChaseBegin state)
        {
            if (state.Enemy.CurrentState != EnemyState.ChaseBegin)
            {
                if (state.Active)
                {
                    state.Active = false;
                }

                return;
            }

            if (!state.Active)
            {
                if (state.Enemy.MasterClient)
                {
                    state.Enemy.StateChase.ChaseCanReach = true;
                    state.Enemy.NavMeshAgent.ResetPath();
                    state.StateTimer = Random.Range(state.StateTimeMin, state.StateTimeMax);
                }

                state.TargetPlayer = PlayerController.instance.playerAvatarScript;
                foreach (var playerAvatar in GameDirector.instance.PlayerList)
                {
                        if ((!playerAvatar.isDisabled || LastChanceMonstersDisabledGateHelper.ShouldTreatDisabledAsActive(playerAvatar)) &&
                            playerAvatar.photonView.ViewID == state.Enemy.TargetPlayerViewID)
                        {
                            state.TargetPlayer = playerAvatar;
                            break;
                        }
                }

                foreach (var playerAvatar in GameDirector.instance.PlayerList)
                {
                    if ((!playerAvatar.isDisabled || LastChanceMonstersDisabledGateHelper.ShouldTreatDisabledAsActive(playerAvatar)) &&
                        playerAvatar.isLocal)
                    {
                        if (GameManager.instance.gameMode != 0 && !(state.TargetPlayer == playerAvatar) && !state.Enemy.PlayerRoom.SameLocal && !state.Enemy.OnScreen.OnScreenLocal)
                        {
                            state.LocalEffect = false;
                            GameDirector.instance.CameraImpact.ShakeDistance(5f, 5f, 10f, state.transform.position, 0.25f);
                            GameDirector.instance.CameraShake.ShakeDistance(3f, 5f, 10f, state.transform.position, 0.5f);
                            break;
                        }

                        state.LocalEffect = true;
                        GameDirector.instance.CameraImpact.Shake(5f, 0.25f);
                        GameDirector.instance.CameraShake.Shake(3f, 0.5f);
                        if (state.Stinger)
                        {
                            CameraGlitch.Instance.PlayShort();
                            AudioScare.instance.PlayImpact();
                        }

                        break;
                    }
                }

                state.Active = true;
            }

            state.Enemy.SetChaseTimer();
            if (!state.Enemy.MasterClient)
            {
                return;
            }

            var targetPlayer = state.TargetPlayer;
            if (targetPlayer == null)
            {
                var enemyTarget = state.Enemy.TargetPlayerAvatar;
                if (enemyTarget != null &&
                    (!enemyTarget.isDisabled || LastChanceMonstersDisabledGateHelper.ShouldTreatDisabledAsActive(enemyTarget)))
                {
                    targetPlayer = enemyTarget;
                    state.TargetPlayer = enemyTarget;
                }
                else
                {
                    state.Enemy.CurrentState = EnemyState.Roaming;
                    return;
                }
            }

            state.Enemy.NavMeshAgent.UpdateAgent(0f, 5f);
            state.Enemy.NavMeshAgent.Stop(0.1f);
            state.transform.LookAt(ResolveTargetPosition(targetPlayer));
            state.transform.localEulerAngles = new Vector3(0f, state.transform.localEulerAngles.y, 0f);
            state.StateTimer -= Time.deltaTime;
            if (state.StateTimer <= 0f)
            {
                state.Enemy.CurrentState = EnemyState.Chase;
            }
        }

        private static Vector3 ResolveTargetPosition(PlayerAvatar targetPlayer)
        {
            return LastChanceMonstersTargetingOrchestrator.ResolveEffectiveTransformTargetPoint(targetPlayer.transform);
        }
    }
}

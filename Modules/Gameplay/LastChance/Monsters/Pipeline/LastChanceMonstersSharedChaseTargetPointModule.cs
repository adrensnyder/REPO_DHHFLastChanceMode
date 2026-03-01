#nullable enable

using BepInEx.Logging;
using DHHFLastChanceMode.Modules.Config;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Support;
using DHHFLastChanceMode.Modules.Utilities;
using HarmonyLib;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Pipeline
{
    [HarmonyPatch]
    internal static class LastChanceMonstersSharedChaseTargetPointModule
    {
        private static readonly ManualLogSource Log = Logger.CreateLogSource("DHHFLastChanceMode.LastChance.HeadmanChase");

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
                DebugChaseTransition(state.Enemy, "Chase->Roaming.NoTarget", null, "TargetPlayerAvatar is null");
                state.Enemy.CurrentState = EnemyState.Roaming;
                return;
            }

            DebugChaseHeartbeat(state.Enemy, "Chase.Heartbeat", targetPlayer, $"visionTimer={state.VisionTimer:F2} stateTimer={state.StateTimer:F2} canReach={state.ChaseCanReach}");

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
                DebugChaseTransition(state.Enemy, "Chase->Roaming.DisabledTarget", targetPlayer, "target is disabled and not eligible");
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
                    DebugChaseTransition(state.Enemy, "ChaseBegin.ResetActive", state.TargetPlayer, "CurrentState != ChaseBegin");
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

            DebugChaseHeartbeat(state.Enemy, "ChaseBegin.Heartbeat", state.TargetPlayer, $"active={state.Active} stateTimer={state.StateTimer:F2} targetViewId={state.Enemy.TargetPlayerViewID}");

            var targetPlayer = state.TargetPlayer;
            if (targetPlayer == null)
            {
                var enemyTarget = state.Enemy.TargetPlayerAvatar;
                if (enemyTarget != null &&
                    (!enemyTarget.isDisabled || LastChanceMonstersDisabledGateHelper.ShouldTreatDisabledAsActive(enemyTarget)))
                {
                    targetPlayer = enemyTarget;
                    state.TargetPlayer = enemyTarget;
                    DebugChaseTransition(state.Enemy, "ChaseBegin.TargetFallback", enemyTarget, "using Enemy.TargetPlayerAvatar fallback");
                }
                else
                {
                    DebugChaseTransition(state.Enemy, "ChaseBegin->Roaming.NoUsableTarget", enemyTarget, "TargetPlayer null and fallback invalid");
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
                DebugChaseTransition(state.Enemy, "ChaseBegin->Chase.TimerElapsed", targetPlayer, "transitioning to chase");
                state.Enemy.CurrentState = EnemyState.Chase;
            }
        }

        private static Vector3 ResolveTargetPosition(PlayerAvatar targetPlayer)
        {
            return LastChanceMonstersTargetingOrchestrator.ResolveEffectiveTransformTargetPoint(targetPlayer.transform);
        }

        private static void DebugChaseTransition(Enemy? enemy, string reason, PlayerAvatar? target, string details)
        {
            if (!InternalDebugFlags.DebugLastChanceHeadmanSlowMouthFlow || !LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled() || enemy == null)
            {
                return;
            }

            var enemyId = enemy.GetInstanceID();
            if (!LogLimiter.ShouldLog($"HeadmanChase.{reason}.{enemyId}", 20))
            {
                return;
            }

            var targetId = target != null && target.photonView != null ? target.photonView.ViewID : -1;
            var targetDisabled = target != null && target.isDisabled;
            var targetEligible = LastChanceMonstersDisabledGateHelper.ShouldTreatDisabledAsActive(target);
            var targetInfo = target == null ? "target=n/a" : $"target='{target.gameObject.name}' targetViewId={targetId}";
            Log.LogInfo(
                $"[HeadmanChase] enemy='{enemy.gameObject.name}' enemyId={enemyId} state={enemy.CurrentState} reason={reason} " +
                $"{targetInfo} targetDisabled={targetDisabled} targetEligible={targetEligible} details={details}");
        }

        private static void DebugChaseHeartbeat(Enemy? enemy, string reason, PlayerAvatar? target, string details)
        {
            if (!InternalDebugFlags.DebugLastChanceHeadmanSlowMouthFlow || !LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled() || enemy == null)
            {
                return;
            }

            var enemyId = enemy.GetInstanceID();
            if (!LogLimiter.ShouldLog($"HeadmanChase.{reason}.{enemyId}", 10))
            {
                return;
            }

            var targetId = target != null && target.photonView != null ? target.photonView.ViewID : -1;
            var targetDisabled = target != null && target.isDisabled;
            var targetEligible = LastChanceMonstersDisabledGateHelper.ShouldTreatDisabledAsActive(target);
            Log.LogInfo(
                $"[HeadmanChase] enemy='{enemy.gameObject.name}' enemyId={enemyId} state={enemy.CurrentState} reason={reason} " +
                $"targetViewId={targetId} targetDisabled={targetDisabled} targetEligible={targetEligible} {details}");
        }
    }
}

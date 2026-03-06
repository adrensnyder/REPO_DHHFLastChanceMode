#nullable enable

using System.Collections.Generic;
using BepInEx.Logging;
using DHHFLastChanceMode.Modules.Config;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Adapters;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Support;
using DHHFLastChanceMode.Modules.Utilities;
using HarmonyLib;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Pipeline
{
    [HarmonyPatch]
    internal static class LastChanceMonstersBehaviorDebugProbeModule
    {
        private static readonly ManualLogSource Log = Logger.CreateLogSource("DHHFLastChanceMode.LastChance.MonstersDebugProbe");
        private static readonly Dictionary<int, EnemyState> HeadmanEnemyStateById = new();
        private static readonly Dictionary<int, bool> HeadmanTargetDisabledById = new();
        private static readonly Dictionary<int, EnemySlowMouth.State> SlowMouthStateById = new();

        private static bool ShouldDebug()
        {
            return InternalDebugFlags.DebugLastChanceHeadmanSlowMouthFlow &&
                   LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled();
        }

        [HarmonyPatch(typeof(EnemyHeadUp), nameof(EnemyHeadUp.Update))]
        [HarmonyPostfix]
        private static void EnemyHeadUpUpdatePostfix(EnemyHeadUp __instance)
        {
            if (!ShouldDebug() || __instance == null || __instance.enemy == null)
            {
                return;
            }

            var enemy = __instance.enemy;
            var id = enemy.GetInstanceID();
            var target = enemy.TargetPlayerAvatar;
            var targetDisabled = target != null && target.isDisabled;

            var stateChanged = !HeadmanEnemyStateById.TryGetValue(id, out var previousState) || previousState != enemy.CurrentState;
            var disabledChanged = !HeadmanTargetDisabledById.TryGetValue(id, out var previousDisabled) || previousDisabled != targetDisabled;
            var shouldHeartbeat = LogLimiter.ShouldLog($"Headman.Heartbeat.{id}", 240);
            if (!stateChanged && !disabledChanged && !shouldHeartbeat)
            {
                return;
            }

            HeadmanEnemyStateById[id] = enemy.CurrentState;
            HeadmanTargetDisabledById[id] = targetDisabled;

            var treatAsActive = LastChanceMonstersDisabledGateHelper.ShouldTreatDisabledAsActive(target);
            var hasTarget = target != null;
            var visionTimer = enemy.StateChase != null ? enemy.StateChase.VisionTimer : -1f;
            var targetY = hasTarget && target!.PlayerVisionTarget != null && target.PlayerVisionTarget.VisionTransform != null
                ? target.PlayerVisionTarget.VisionTransform.position.y
                : -1f;
            var scopeActive = LastChanceMonstersDisabledOverrideModule.IsOverrideScopeActive;
            Log.LogInfo(
                $"[HeadmanDebug] enemy={enemy.gameObject.name} id={id} componentId={__instance.GetInstanceID()} state={enemy.CurrentState} hasTarget={hasTarget} " +
                $"targetDisabled={targetDisabled} treatDisabledAsActive={treatAsActive} " +
                $"disabledOverrideScopeActive={scopeActive} visionTimer={visionTimer:F2} targetY={targetY:F2} " +
                $"{BuildPlayerAnchorSummary(target, null)}");
        }

        [HarmonyPatch(typeof(EnemySlowMouth), nameof(EnemySlowMouth.UpdateState))]
        [HarmonyPrefix]
        private static void EnemySlowMouthUpdateStatePrefix(EnemySlowMouth __instance, EnemySlowMouth.State newState)
        {
            if (!ShouldDebug() || __instance == null)
            {
                return;
            }

            var current = __instance.currentState;
            if (current == newState && !LogLimiter.ShouldLog($"SlowMouth.UpdateState.Same.{__instance.GetInstanceID()}", 240))
            {
                return;
            }

            Log.LogInfo(
                $"[SlowMouthDebug] UpdateState request enemy={__instance.gameObject.name} id={__instance.GetInstanceID()} from={current} to={newState} " +
                $"targetDisabled={(__instance.playerTarget != null && __instance.playerTarget.isDisabled)} " +
                $"targetActive={LastChanceMonstersDisabledGateHelper.ShouldTreatDisabledAsActive(__instance.playerTarget)} " +
                $"disabledOverrideScopeActive={LastChanceMonstersDisabledOverrideModule.IsOverrideScopeActive} " +
                $"{BuildPlayerAnchorSummary(__instance.playerTarget, __instance.currentTarget)}");
        }

        [HarmonyPatch(typeof(EnemySlowMouth), nameof(EnemySlowMouth.UpdateStateRPC))]
        [HarmonyPostfix]
        private static void EnemySlowMouthUpdateStateRpcPostfix(EnemySlowMouth __instance, EnemySlowMouth.State newState)
        {
            if (!ShouldDebug() || __instance == null)
            {
                return;
            }

            Log.LogInfo($"[SlowMouthDebug] UpdateStateRPC applied enemy={__instance.gameObject.name} id={__instance.GetInstanceID()} state={newState}");
        }

        [HarmonyPatch(typeof(EnemySlowMouth), nameof(EnemySlowMouth.Update))]
        [HarmonyPostfix]
        private static void EnemySlowMouthUpdatePostfix(EnemySlowMouth __instance)
        {
            if (!ShouldDebug() || __instance == null)
            {
                return;
            }

            var id = __instance.GetInstanceID();
            var state = __instance.currentState;
            var changed = !SlowMouthStateById.TryGetValue(id, out var previous) || previous != state;
            var heartbeat = LogLimiter.ShouldLog($"SlowMouth.Heartbeat.{id}", 240);
            if (!changed && !heartbeat)
            {
                return;
            }

            SlowMouthStateById[id] = state;
            var target = __instance.playerTarget;
            var targetDisabled = target != null && target.isDisabled;
            var targetActive = LastChanceMonstersDisabledGateHelper.ShouldTreatDisabledAsActive(target);
            Log.LogInfo(
                $"[SlowMouthDebug] enemy={__instance.gameObject.name} id={id} state={state} hasTarget={(target != null)} " +
                $"targetDisabled={targetDisabled} targetActive={targetActive} " +
                $"disabledOverrideScopeActive={LastChanceMonstersDisabledOverrideModule.IsOverrideScopeActive} " +
                $"{BuildPlayerAnchorSummary(target, __instance.currentTarget)}");
        }

        [HarmonyPatch(typeof(EnemySlowMouthAttaching), nameof(EnemySlowMouthAttaching.Update))]
        [HarmonyPostfix]
        private static void EnemySlowMouthAttachingUpdatePostfix(EnemySlowMouthAttaching __instance)
        {
            if (!ShouldDebug() || __instance == null)
            {
                return;
            }

            if (!LogLimiter.ShouldLog($"SlowMouthAttaching.Update.{__instance.GetInstanceID()}", 120))
            {
                return;
            }

            var target = __instance.targetPlayerAvatar;
            var targetDisabled = target != null && target.isDisabled;
            Log.LogInfo(
                $"[SlowMouthDebug] Attaching.Update active={__instance.isActive} hasTarget={(target != null)} " +
                $"targetDisabled={targetDisabled} enemyState={__instance.enemySlowMouth.currentState} " +
                $"disabledOverrideScopeActive={LastChanceMonstersDisabledOverrideModule.IsOverrideScopeActive} " +
                $"{BuildPlayerAnchorSummary(target, __instance.targetTransform)}");
        }

        [HarmonyPatch(typeof(EnemyHeadController), nameof(EnemyHeadController.VisionTriggered))]
        [HarmonyPrefix]
        private static void EnemyHeadControllerVisionTriggeredPrefix(EnemyHeadController __instance)
        {
            if (!ShouldDebug() || __instance == null || __instance.Enemy == null)
            {
                return;
            }

            var enemy = __instance.Enemy;
            if (!LogLimiter.ShouldLog($"Headman.VisionTriggered.Prefix.{enemy.GetInstanceID()}", 20))
            {
                return;
            }

            Log.LogInfo(
                $"[HeadmanDebug] VisionTriggered.Prefix enemy={enemy.gameObject.name} id={enemy.GetInstanceID()} " +
                $"state={enemy.CurrentState} targetViewId={enemy.TargetPlayerViewID} " +
                $"{BuildPlayerAnchorSummary(enemy.TargetPlayerAvatar, enemy.TargetPlayerAvatar?.PlayerVisionTarget?.VisionTransform)}");
        }

        [HarmonyPatch(typeof(EnemyHeadController), nameof(EnemyHeadController.VisionTriggered))]
        [HarmonyPostfix]
        private static void EnemyHeadControllerVisionTriggeredPostfix(EnemyHeadController __instance)
        {
            if (!ShouldDebug() || __instance == null || __instance.Enemy == null)
            {
                return;
            }

            var enemy = __instance.Enemy;
            if (!LogLimiter.ShouldLog($"Headman.VisionTriggered.{enemy.GetInstanceID()}", 20))
            {
                return;
            }

            var target = enemy.TargetPlayerAvatar;
            Log.LogInfo(
                $"[HeadmanDebug] VisionTriggered enemy={enemy.gameObject.name} id={enemy.GetInstanceID()} " +
                $"state={enemy.CurrentState} targetViewId={(target?.photonView != null ? target.photonView.ViewID : -1)} " +
                $"{BuildPlayerAnchorSummary(target, target?.PlayerVisionTarget != null ? target.PlayerVisionTarget.VisionTransform : null)}");
        }

        [HarmonyPatch(typeof(EnemyStateChaseBegin), nameof(EnemyStateChaseBegin.Update))]
        [HarmonyPrefix]
        private static void EnemyStateChaseBeginUpdatePrefix(EnemyStateChaseBegin __instance)
        {
            if (!ShouldDebug() || __instance == null || __instance.Enemy == null)
            {
                return;
            }

            var enemy = __instance.Enemy;
            if (!LogLimiter.ShouldLog($"Headman.ChaseBegin.Update.Prefix.{enemy.GetInstanceID()}", 15))
            {
                return;
            }

            var target = __instance.TargetPlayer ?? enemy.TargetPlayerAvatar;
            Log.LogInfo(
                $"[HeadmanDebug] ChaseBegin.Update.Prefix enemy={enemy.gameObject.name} id={enemy.GetInstanceID()} " +
                $"enemyState={enemy.CurrentState} active={__instance.Active} stateTimer={__instance.StateTimer:F2} " +
                $"master={enemy.MasterClient} targetViewId={(target?.photonView != null ? target.photonView.ViewID : -1)} " +
                $"{BuildPlayerAnchorSummary(target, target?.PlayerVisionTarget?.VisionTransform)}");
        }

        [HarmonyPatch(typeof(EnemyStateChaseBegin), nameof(EnemyStateChaseBegin.Update))]
        [HarmonyPostfix]
        private static void EnemyStateChaseBeginUpdatePostfix(EnemyStateChaseBegin __instance)
        {
            if (!ShouldDebug() || __instance == null || __instance.Enemy == null)
            {
                return;
            }

            var enemy = __instance.Enemy;
            if (!LogLimiter.ShouldLog($"Headman.ChaseBegin.Update.Postfix.{enemy.GetInstanceID()}", 15))
            {
                return;
            }

            var target = __instance.TargetPlayer ?? enemy.TargetPlayerAvatar;
            Log.LogInfo(
                $"[HeadmanDebug] ChaseBegin.Update.Postfix enemy={enemy.gameObject.name} id={enemy.GetInstanceID()} " +
                $"enemyState={enemy.CurrentState} active={__instance.Active} stateTimer={__instance.StateTimer:F2} " +
                $"master={enemy.MasterClient} targetViewId={(target?.photonView != null ? target.photonView.ViewID : -1)} " +
                $"{BuildPlayerAnchorSummary(target, target?.PlayerVisionTarget?.VisionTransform)}");
        }

        [HarmonyPatch(typeof(EnemyStateChase), nameof(EnemyStateChase.Update))]
        [HarmonyPrefix]
        private static void EnemyStateChaseUpdatePrefix(EnemyStateChase __instance)
        {
            if (!ShouldDebug() || __instance == null || __instance.Enemy == null)
            {
                return;
            }

            var enemy = __instance.Enemy;
            if (!LogLimiter.ShouldLog($"Headman.Chase.Update.Prefix.{enemy.GetInstanceID()}", 15))
            {
                return;
            }

            var target = enemy.TargetPlayerAvatar;
            Log.LogInfo(
                $"[HeadmanDebug] Chase.Update.Prefix enemy={enemy.gameObject.name} id={enemy.GetInstanceID()} " +
                $"enemyState={enemy.CurrentState} active={__instance.Active} visionTimer={__instance.VisionTimer:F2} " +
                $"stateTimer={__instance.StateTimer:F2} canReach={__instance.ChaseCanReach} master={enemy.MasterClient} " +
                $"targetViewId={(target?.photonView != null ? target.photonView.ViewID : -1)} " +
                $"{BuildPlayerAnchorSummary(target, target?.PlayerVisionTarget?.VisionTransform)}");
        }

        [HarmonyPatch(typeof(EnemyStateChase), nameof(EnemyStateChase.Update))]
        [HarmonyPostfix]
        private static void EnemyStateChaseUpdatePostfix(EnemyStateChase __instance)
        {
            if (!ShouldDebug() || __instance == null || __instance.Enemy == null)
            {
                return;
            }

            LogRoamingWriter("EnemyStateChase.Update", __instance.Enemy, $"visionTimer={__instance.VisionTimer:F2} stateTimer={__instance.StateTimer:F2} canReach={__instance.ChaseCanReach}");
        }

        [HarmonyPatch(typeof(EnemyHeadController), nameof(EnemyHeadController.OnStunnedEnd))]
        [HarmonyPrefix]
        private static void EnemyHeadControllerOnStunnedEndPrefix(EnemyHeadController __instance)
        {
            if (!ShouldDebug() || __instance == null || __instance.Enemy == null)
            {
                return;
            }

            var enemy = __instance.Enemy;
            if (!LogLimiter.ShouldLog($"Headman.StunnedEnd.{enemy.GetInstanceID()}", 20))
            {
                return;
            }

            Log.LogInfo(
                $"[HeadmanDebug] OnStunnedEnd enemy={enemy.gameObject.name} id={enemy.GetInstanceID()} " +
                $"stateBefore={enemy.CurrentState} -> Roaming");
        }

        [HarmonyPatch(typeof(EnemyHeadController), nameof(EnemyHeadController.OnStunnedEnd))]
        [HarmonyPostfix]
        private static void EnemyHeadControllerOnStunnedEndPostfix(EnemyHeadController __instance)
        {
            if (!ShouldDebug() || __instance == null || __instance.Enemy == null)
            {
                return;
            }

            LogRoamingWriter("EnemyHeadController.OnStunnedEnd", __instance.Enemy, "postfix");
        }

        [HarmonyPatch(typeof(EnemyStateStunned), nameof(EnemyStateStunned.Update))]
        [HarmonyPrefix]
        private static void EnemyStateStunnedUpdatePrefix(EnemyStateStunned __instance)
        {
            if (!ShouldDebug() || __instance == null || __instance.enemy == null)
            {
                return;
            }

            var enemy = __instance.enemy;
            if (enemy.GetComponentInChildren<EnemyHeadController>() == null)
            {
                return;
            }

            if (!LogLimiter.ShouldLog($"Headman.Stunned.Update.Prefix.{enemy.GetInstanceID()}", 15))
            {
                return;
            }

            var target = enemy.TargetPlayerAvatar;
            Log.LogInfo(
                $"[HeadmanDebug] Stunned.Update.Prefix enemy={enemy.gameObject.name} id={enemy.GetInstanceID()} " +
                $"enemyState={enemy.CurrentState} stunTimer={__instance.stunTimer:F2} overrideDisableTimer={__instance.overrideDisableTimer:F2} " +
                $"active={__instance.active} targetViewId={(target?.photonView != null ? target.photonView.ViewID : -1)} " +
                $"{BuildPlayerAnchorSummary(target, target?.PlayerVisionTarget?.VisionTransform)}");
        }

        [HarmonyPatch(typeof(EnemyStateStunned), nameof(EnemyStateStunned.Update))]
        [HarmonyPostfix]
        private static void EnemyStateStunnedUpdatePostfix(EnemyStateStunned __instance)
        {
            if (!ShouldDebug() || __instance == null || __instance.enemy == null)
            {
                return;
            }

            var enemy = __instance.enemy;
            if (enemy.GetComponentInChildren<EnemyHeadController>() == null)
            {
                return;
            }

            if (!LogLimiter.ShouldLog($"Headman.Stunned.Update.Postfix.{enemy.GetInstanceID()}", 15))
            {
                return;
            }

            var target = enemy.TargetPlayerAvatar;
            Log.LogInfo(
                $"[HeadmanDebug] Stunned.Update.Postfix enemy={enemy.gameObject.name} id={enemy.GetInstanceID()} " +
                $"enemyState={enemy.CurrentState} stunTimer={__instance.stunTimer:F2} overrideDisableTimer={__instance.overrideDisableTimer:F2} " +
                $"active={__instance.active} targetViewId={(target?.photonView != null ? target.photonView.ViewID : -1)} " +
                $"{BuildPlayerAnchorSummary(target, target?.PlayerVisionTarget?.VisionTransform)}");

            LogRoamingWriter(
                "EnemyStateStunned.Update",
                enemy,
                $"stunTimer={__instance.stunTimer:F2} overrideDisableTimer={__instance.overrideDisableTimer:F2} active={__instance.active}");
        }

        [HarmonyPatch(typeof(EnemyStateSneak), nameof(EnemyStateSneak.Update))]
        [HarmonyPostfix]
        private static void EnemyStateSneakUpdatePostfix(EnemyStateSneak __instance)
        {
            if (!ShouldDebug() || __instance == null || __instance.Enemy == null)
            {
                return;
            }

            var target = __instance.TargetPlayer ?? __instance.Enemy.TargetPlayerAvatar;
            LogRoamingWriter(
                "EnemyStateSneak.Update",
                __instance.Enemy,
                $"stateTimer={__instance.StateTimer:F2} active={__instance.Active} targetViewId={(target?.photonView != null ? target.photonView.ViewID : -1)}");
        }

        [HarmonyPatch(typeof(EnemyStateChaseEnd), nameof(EnemyStateChaseEnd.Update))]
        [HarmonyPostfix]
        private static void EnemyStateChaseEndUpdatePostfix(EnemyStateChaseEnd __instance)
        {
            if (!ShouldDebug() || __instance == null || __instance.Enemy == null)
            {
                return;
            }

            LogRoamingWriter(
                "EnemyStateChaseEnd.Update",
                __instance.Enemy,
                $"stateTimer={__instance.StateTimer:F2} active={__instance.Active}");
        }

        [HarmonyPatch(typeof(Enemy), nameof(Enemy.PlayerRemoved))]
        [HarmonyPostfix]
        private static void EnemyPlayerRemovedPostfix(Enemy __instance, int photonID)
        {
            if (!ShouldDebug() || __instance == null)
            {
                return;
            }

            LogRoamingWriter("Enemy.PlayerRemoved", __instance, $"removedPhotonId={photonID}");
        }

        [HarmonyPatch(typeof(EnemySlowMouth), nameof(EnemySlowMouth.OnSpawn))]
        [HarmonyPostfix]
        private static void EnemySlowMouthOnSpawnPostfix(EnemySlowMouth __instance)
        {
            if (!ShouldDebug() || __instance == null)
            {
                return;
            }

            Log.LogInfo(
                $"[SlowMouthDebug] OnSpawn enemy={__instance.gameObject.name} id={__instance.GetInstanceID()} " +
                $"{BuildPlayerAnchorSummary(__instance.playerTarget, __instance.currentTarget)}");
        }

        private static string BuildPlayerAnchorSummary(PlayerAvatar? target, Transform? currentTarget)
        {
            if (target == null)
            {
                return "anchorSummary=target-null";
            }

            var headDelta = -1f;
            if (currentTarget != null && LastChanceMonstersTargetProxyHelper.TryGetHeadProxyVisionTarget(target, out var headVision))
            {
                headDelta = Vector3.Distance(currentTarget.position, headVision);
            }

            var cameraDelta = -1f;
            if (currentTarget != null && target.localCamera != null)
            {
                cameraDelta = Vector3.Distance(currentTarget.position, target.localCamera.transform.position);
            }

            return $"anchorSummary=currentTargetToHead={headDelta:F2} currentTargetToCamera={cameraDelta:F2}";
        }

        private static void LogRoamingWriter(string source, Enemy? enemy, string details)
        {
            if (enemy == null || enemy.GetComponentInChildren<EnemyHeadController>() == null)
            {
                return;
            }

            if (enemy.CurrentState != EnemyState.Roaming)
            {
                return;
            }

            var id = enemy.GetInstanceID();
            if (!LogLimiter.ShouldLog($"Headman.RoamingWriter.{source}.{id}", 5))
            {
                return;
            }

            var target = enemy.TargetPlayerAvatar;
            var targetId = target?.photonView != null ? target.photonView.ViewID : -1;
            var targetDisabled = target != null && target.isDisabled;
            var targetEligible = LastChanceMonstersDisabledGateHelper.ShouldTreatDisabledAsActive(target);
            var visionTimer = enemy.StateChase != null ? enemy.StateChase.VisionTimer : -1f;
            Log.LogInfo(
                $"[HeadmanRoamingWriter] source={source} enemy={enemy.gameObject.name} id={id} " +
                $"targetViewId={targetId} targetDisabled={targetDisabled} targetEligible={targetEligible} " +
                $"visionTimer={visionTimer:F2} details={details}");
        }
    }
}

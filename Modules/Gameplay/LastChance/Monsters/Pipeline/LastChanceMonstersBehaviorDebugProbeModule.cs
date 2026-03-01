#nullable enable

using System.Collections.Generic;
using BepInEx.Logging;
using DHHFLastChanceMode.Modules.Config;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Support;
using DHHFLastChanceMode.Modules.Utilities;
using HarmonyLib;
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

            var id = __instance.GetInstanceID();
            var enemy = __instance.enemy;
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
            var targetY = hasTarget ? target!.PlayerVisionTarget.VisionTransform.position.y : -1f;
            Log.LogInfo(
                $"[HeadmanDebug] enemy={enemy.gameObject.name} state={enemy.CurrentState} hasTarget={hasTarget} " +
                $"targetDisabled={targetDisabled} treatDisabledAsActive={treatAsActive} visionTimer={visionTimer:F2} targetY={targetY:F2}");
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
                $"[SlowMouthDebug] UpdateState request enemy={__instance.gameObject.name} from={current} to={newState} " +
                $"targetDisabled={(__instance.playerTarget != null && __instance.playerTarget.isDisabled)} " +
                $"targetActive={LastChanceMonstersDisabledGateHelper.ShouldTreatDisabledAsActive(__instance.playerTarget)}");
        }

        [HarmonyPatch(typeof(EnemySlowMouth), nameof(EnemySlowMouth.UpdateStateRPC))]
        [HarmonyPostfix]
        private static void EnemySlowMouthUpdateStateRpcPostfix(EnemySlowMouth __instance, EnemySlowMouth.State newState)
        {
            if (!ShouldDebug() || __instance == null)
            {
                return;
            }

            Log.LogInfo($"[SlowMouthDebug] UpdateStateRPC applied enemy={__instance.gameObject.name} state={newState}");
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
                $"[SlowMouthDebug] enemy={__instance.gameObject.name} state={state} hasTarget={(target != null)} " +
                $"targetDisabled={targetDisabled} targetActive={targetActive}");
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
                $"targetDisabled={targetDisabled} enemyState={__instance.enemySlowMouth.currentState}");
        }
    }
}

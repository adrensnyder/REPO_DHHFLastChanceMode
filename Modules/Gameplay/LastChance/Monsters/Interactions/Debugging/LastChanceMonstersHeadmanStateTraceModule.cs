#nullable enable

using BepInEx.Logging;
using DHHFLastChanceMode.Modules.Config;
using DHHFLastChanceMode.Modules.Utilities;
using HarmonyLib;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Interactions.Debugging
{
    [HarmonyPatch]
    internal static class LastChanceMonstersHeadmanStateTraceModule
    {
        private static readonly ManualLogSource Log = Logger.CreateLogSource("DHHFLastChanceMode.LastChance.Headman");

        [HarmonyPatch(typeof(EnemyHeadController), "VisionTriggered")]
        [HarmonyPrefix]
        private static void EnemyHeadVisionTriggeredPrefix(EnemyHeadController __instance)
        {
            if (!LastChanceMonstersDebugGate.IsEnabled(InternalDebugFlags.DebugLastChanceHeadmanFlow) || __instance == null)
            {
                return;
            }

            var enemy = __instance.Enemy;
            LogDecision(
                "VisionTriggered",
                enemy,
                $"state={enemy?.CurrentState} target={GetPlayerName(GetVisionPlayer(enemy))} " +
                $"dist={GetVisionDistance(enemy):F2} culled={GetVisionCulled(enemy)} near={GetVisionNear(enemy)}");
        }

        [HarmonyPatch(typeof(EnemyStateChaseBegin), "Update")]
        [HarmonyPostfix]
        private static void EnemyStateChaseBeginUpdatePostfix(EnemyStateChaseBegin __instance)
        {
            if (!LastChanceMonstersDebugGate.IsEnabled(InternalDebugFlags.DebugLastChanceHeadmanFlow) || __instance == null)
            {
                return;
            }

            var enemy = __instance.Enemy;
            if (enemy == null || enemy.CurrentState != EnemyState.ChaseBegin)
            {
                return;
            }

            var target = __instance.TargetPlayer;
            var timer = __instance.StateTimer;
            LogHeartbeat(
                "ChaseBegin.Update",
                enemy,
                $"stateTimer={timer:F2} target={GetPlayerName(target)} targetDisabled={LastChanceMonstersTargetProxyHelper.IsDisabled(target)} " +
                $"headProxy={LastChanceMonstersTargetProxyHelper.IsHeadProxyActive(target)} bodyDist={GetDistance(enemy, target, useHead: false)} " +
                $"headDist={GetDistance(enemy, target, useHead: true)}");
        }

        [HarmonyPatch(typeof(EnemyStateChase), "Update")]
        [HarmonyPostfix]
        private static void EnemyStateChaseUpdatePostfix(EnemyStateChase __instance)
        {
            if (!LastChanceMonstersDebugGate.IsEnabled(InternalDebugFlags.DebugLastChanceHeadmanFlow) || __instance == null)
            {
                return;
            }

            var enemy = __instance.Enemy;
            if (enemy == null || enemy.CurrentState != EnemyState.Chase)
            {
                return;
            }

            var target = enemy.TargetPlayerAvatar;
            var visionTimer = __instance.VisionTimer;
            var cantReach = __instance.CantReachTime;
            var chaseCanReach = __instance.ChaseCanReach;
            var stateTimer = __instance.StateTimer;

            LogHeartbeat(
                "Chase.Update",
                enemy,
                $"stateTimer={stateTimer:F2} visionTimer={visionTimer:F2} cantReach={cantReach:F2} canReach={chaseCanReach} " +
                $"target={GetPlayerName(target)} targetDisabled={LastChanceMonstersTargetProxyHelper.IsDisabled(target)} " +
                $"headProxy={LastChanceMonstersTargetProxyHelper.IsHeadProxyActive(target)} bodyDist={GetDistance(enemy, target, useHead: false)} " +
                $"headDist={GetDistance(enemy, target, useHead: true)} chasePos={GetChasePosition(enemy)}");
        }

        [HarmonyPatch(typeof(EnemyStateChaseSlow), "Update")]
        [HarmonyPostfix]
        private static void EnemyStateChaseSlowUpdatePostfix(EnemyStateChaseSlow __instance)
        {
            if (!LastChanceMonstersDebugGate.IsEnabled(InternalDebugFlags.DebugLastChanceHeadmanFlow) || __instance == null)
            {
                return;
            }

            var enemy = __instance.Enemy;
            if (enemy == null || enemy.CurrentState != EnemyState.ChaseSlow)
            {
                return;
            }

            var target = enemy.TargetPlayerAvatar;
            var timer = __instance.StateTimer;
            var destination = GetNavDestination(enemy);
            LogHeartbeat(
                "ChaseSlow.Update",
                enemy,
                $"stateTimer={timer:F2} target={GetPlayerName(target)} targetDisabled={LastChanceMonstersTargetProxyHelper.IsDisabled(target)} " +
                $"headProxy={LastChanceMonstersTargetProxyHelper.IsHeadProxyActive(target)} bodyDist={GetDistance(enemy, target, useHead: false)} " +
                $"headDist={GetDistance(enemy, target, useHead: true)} destination={destination}");
        }

        private static string GetPlayerName(PlayerAvatar? player)
        {
            return player == null ? "n/a" : player.name;
        }

        private static string GetDistance(Enemy enemy, PlayerAvatar? player, bool useHead)
        {
            if (enemy == null || player == null)
            {
                return "n/a";
            }

            var from = enemy.CenterTransform != null ? enemy.CenterTransform.position : enemy.transform.position;
            var to = player.transform.position;
            if (useHead && LastChanceMonstersTargetProxyHelper.TryGetHeadProxyTarget(player, out var headCenter))
            {
                to = headCenter;
            }

            return Vector3.Distance(from, to).ToString("F2");
        }

        private static PlayerAvatar? GetVisionPlayer(Enemy? enemy)
        {
            return enemy?.Vision?.onVisionTriggeredPlayer;
        }

        private static float GetVisionDistance(Enemy? enemy)
        {
            return enemy?.Vision?.onVisionTriggeredDistance ?? -1f;
        }

        private static bool GetVisionCulled(Enemy? enemy)
        {
            return enemy?.Vision?.onVisionTriggeredCulled ?? false;
        }

        private static bool GetVisionNear(Enemy? enemy)
        {
            return enemy?.Vision?.onVisionTriggeredNear ?? false;
        }

        private static Vector3 GetChasePosition(Enemy? enemy)
        {
            return enemy?.StateChase?.ChasePosition ?? Vector3.zero;
        }

        private static Vector3 GetNavDestination(Enemy? enemy)
        {
            return enemy?.NavMeshAgent?.GetDestination() ?? Vector3.zero;
        }

        private static void LogDecision(string reason, Enemy? enemy, string message)
        {
            if (!LastChanceMonstersDebugGate.IsEnabled(InternalDebugFlags.DebugLastChanceHeadmanFlow))
            {
                return;
            }

            if (!LastChanceMonstersDebugGate.IsVerbose(InternalDebugFlags.DebugLastChanceHeadmanVerbose) &&
                !LogLimiter.ShouldLog($"Headman.{reason}", 10))
            {
                return;
            }

            var runtime = LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled();
            var enemyInfo = enemy == null ? "enemy=n/a id=n/a" : $"enemy={enemy.name} id={enemy.GetInstanceID()}";
            Log.LogInfo($"[Headman][{reason}] runtime={runtime} {enemyInfo} {message}");
        }

        private static void LogHeartbeat(string reason, Enemy? enemy, string message)
        {
            if (!LastChanceMonstersDebugGate.IsEnabled(InternalDebugFlags.DebugLastChanceHeadmanFlow))
            {
                return;
            }

            var key = enemy == null
                ? $"Headman.{reason}.none"
                : $"Headman.{reason}.{enemy.GetInstanceID()}";

            var interval = LastChanceMonstersDebugGate.IsVerbose(InternalDebugFlags.DebugLastChanceHeadmanVerbose) ? 3 : 15;
            if (!LogLimiter.ShouldLog(key, interval))
            {
                return;
            }

            var runtime = LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled();
            var enemyInfo = enemy == null ? "enemy=n/a id=n/a" : $"enemy={enemy.name} id={enemy.GetInstanceID()}";
            Log.LogInfo($"[Headman][{reason}] runtime={runtime} {enemyInfo} {message}");
        }
    }
}

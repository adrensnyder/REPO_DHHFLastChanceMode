#nullable enable

using System;
using BepInEx.Logging;
using DHHFLastChanceMode.Modules.Config;
using DHHFLastChanceMode.Modules.Utilities;
using HarmonyLib;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Interactions.Debugging
{
    [HarmonyPatch]
    internal static class LastChanceMonstersSpinnyStateTraceModule
    {
        private static readonly ManualLogSource Log = Logger.CreateLogSource("DHHFLastChanceMode.LastChance.Spinny");

        [HarmonyPatch(typeof(EnemySpinny), "UpdateState")]
        private static class UpdateStateTracePatch
        {
            [HarmonyPrefix]
            private static void Prefix(EnemySpinny __instance, EnemySpinny.State _nextState)
            {
                if (!ShouldTrace(__instance))
                {
                    return;
                }

                DebugLog(
                    "UpdateState.Call",
                    $"enemyId={GetInstanceKey(__instance)} from={ReadStateName(__instance)} to={_nextState} {BuildSnapshot(__instance)}");

                var from = ReadStateName(__instance);
                var to = _nextState.ToString();
                if (string.Equals(from, nameof(EnemySpinny.State.WaitForRoulette), StringComparison.Ordinal) &&
                    string.Equals(to, nameof(EnemySpinny.State.CloseMouth), StringComparison.Ordinal))
                {
                    DebugLog("WaitForRoulette.CloseMouthReason", $"enemyId={GetInstanceKey(__instance)} {BuildDecisionSnapshot(__instance)}");
                }
            }
        }

        [HarmonyPatch(typeof(EnemySpinny), "StateWaitForRoulette")]
        private static class StateWaitForRouletteTracePatch
        {
            [HarmonyPrefix]
            private static void Prefix(EnemySpinny __instance)
            {
                if (!ShouldTrace(__instance))
                {
                    return;
                }

                DebugLog("WaitForRoulette.Enter", $"enemyId={GetInstanceKey(__instance)} {BuildSnapshot(__instance)}");
            }

            [HarmonyPostfix]
            private static void Postfix(EnemySpinny __instance)
            {
                if (!ShouldTrace(__instance))
                {
                    return;
                }

                DebugLog("WaitForRoulette.Exit", $"enemyId={GetInstanceKey(__instance)} {BuildSnapshot(__instance)}");
                DebugLog("WaitForRoulette.Decision", $"enemyId={GetInstanceKey(__instance)} {BuildDecisionSnapshot(__instance)}");
            }
        }

        [HarmonyPatch(typeof(EnemySpinny), "HasLineOfSight")]
        private static class HasLineOfSightTracePatch
        {
            [HarmonyPostfix]
            private static void Postfix(EnemySpinny __instance, bool __result)
            {
                if (!ShouldTrace(__instance))
                {
                    return;
                }

                DebugLog("HasLineOfSight", $"enemyId={GetInstanceKey(__instance)} result={__result} {BuildSnapshot(__instance)}");
            }
        }

        private static bool ShouldTrace(EnemySpinny? instance)
        {
            if (!LastChanceMonstersDebugGate.IsEnabled(InternalDebugFlags.DebugLastChanceSpinnyFlow) || instance == null)
            {
                return false;
            }

            if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
            {
                return false;
            }

            var player = instance.playerTarget;
            return player != null && LastChanceMonstersTargetProxyHelper.IsHeadProxyActive(player);
        }

        private static string BuildSnapshot(EnemySpinny instance)
        {
            var player = instance.playerTarget;
            var lockPoint = instance.playerLockPoint;
            var mouthOpened = instance.enemySpinnyAnim != null && instance.enemySpinnyAnim.mouthOpened;
            var playerIsTumbling = player != null && player.isTumbling;
            var stateTimer = instance.stateTimer;
            var lockPointTimer = instance.lockPointTimer;
            var offLockPointTimer = instance.offLockPointTimer;
            var reachedPoint = instance.reachedPoint;

            float? dist = null;
            var tumbleRb = player?.tumble?.rb;
            if (tumbleRb != null && lockPoint != null)
            {
                dist = Vector3.Distance(tumbleRb.position, lockPoint.position);
            }

            return
                $"state={ReadStateName(instance)} " +
                $"player={(player != null ? (player.photonView != null ? player.photonView.ViewID.ToString() : player.GetInstanceID().ToString()) : "n/a")} " +
                $"isTumbling={playerIsTumbling} " +
                $"stateTimer={stateTimer:F2} " +
                $"lockPointTimer={lockPointTimer:F2} " +
                $"offLockPointTimer={offLockPointTimer:F2} " +
                $"reachedPoint={reachedPoint} " +
                $"distToLock={(dist.HasValue ? dist.Value.ToString("F2") : "n/a")} " +
                $"mouthOpened={mouthOpened}";
        }

        private static string ReadStateName(EnemySpinny instance)
        {
            return instance.currentState.ToString();
        }

        private static string BuildDecisionSnapshot(EnemySpinny instance)
        {
            var player = instance.playerTarget;
            var stateTimer = instance.stateTimer;
            var offLockPointTimer = instance.offLockPointTimer;
            var reachedPoint = instance.reachedPoint;
            var enemy = instance.enemy;
            var stunned = enemy != null && enemy.IsStunned();
            var closeByTimeoutNoReach = stateTimer <= 0f && !reachedPoint;
            var closeByStunnedOrOffLock = stunned || offLockPointTimer > 1.5f;
            var canRouletteByGrab = instance.enemySpinnyAnim != null && instance.enemySpinnyAnim.mouthOpened;
            var playerId = player != null ? (player.photonView != null ? player.photonView.ViewID.ToString() : player.GetInstanceID().ToString()) : "n/a";
            return $"player={playerId} reachedPoint={reachedPoint} stateTimer={stateTimer:F2} offLockPointTimer={offLockPointTimer:F2} stunned={stunned} closeByTimeoutNoReach={closeByTimeoutNoReach} closeByStunnedOrOffLock={closeByStunnedOrOffLock} mouthOpened={canRouletteByGrab}";
        }

        private static void DebugLog(string reason, string detail)
        {
            if (!LastChanceMonstersDebugGate.IsEnabled(InternalDebugFlags.DebugLastChanceSpinnyFlow))
            {
                return;
            }

            if (!LastChanceMonstersDebugGate.IsVerbose(InternalDebugFlags.DebugLastChanceSpinnyVerbose) &&
                !LogLimiter.ShouldLog($"Spinny.Trace.{reason}", 10))
            {
                return;
            }

            Log.LogInfo($"[Spinny][{reason}] {detail}");
        }

        private static int GetInstanceKey(EnemySpinny instance)
        {
            return instance.GetInstanceID();
        }
    }
}

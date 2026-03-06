#nullable enable

using System;
using BepInEx.Logging;
using DHHFLastChanceMode.Modules.Config;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Adapters;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Support;
using DHHFLastChanceMode.Modules.Utilities;
using HarmonyLib;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Interactions
{
    [HarmonyPatch(typeof(EnemySpinny), nameof(EnemySpinny.LockInPlayer))]
    internal static class LastChanceMonstersSpinnyLockBridgeModule
    {
        private static readonly ManualLogSource Log = Logger.CreateLogSource("DHHFLastChanceMode.LastChance.Spinny");

        [HarmonyPostfix]
        private static void Postfix(EnemySpinny __instance, bool _horizontalPull = false, bool _fixedUpdate = false)
        {
            if (__instance == null)
            {
                return;
            }

            if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
            {
                return;
            }

            if (!IsSpinnyLockState(__instance.currentState))
            {
                return;
            }

            var player = __instance.playerTarget;
            if (!LastChanceMonstersLockBridgeCore.IsHeadProxyRuntimeApplicable(player))
            {
                return;
            }

            var tumble = player!.tumble;
            var tumbleRb = tumble.rb;
            if (tumbleRb == null)
            {
                DebugLog("Bridge.Skip.NoTumbleRb", $"enemyId={GetInstanceKey(__instance)} player={GetPlayerId(player)}");
                return;
            }

            StabilizeTumbleAtLockPoint(__instance, tumbleRb, _fixedUpdate);

            if (!LastChanceMonstersTargetProxyHelper.TryGetHeadProxyPhysGrabObject(player, out var headPhys) || headPhys?.rb == null)
            {
                DebugLog("Bridge.Skip.NoHeadPhys", $"enemyId={GetInstanceKey(__instance)} player={GetPlayerId(player)}");
                return;
            }

            var headRb = headPhys.rb;
            var couple = LastChanceMonstersLockBridgeCore.CoupleHeadToTarget(headPhys, headRb, tumbleRb, _fixedUpdate);
            if (couple.HardSnap)
            {
                DebugLog("Bridge.HardSnap", $"enemyId={GetInstanceKey(__instance)} player={GetPlayerId(player)} dist={couple.Distance:0.00}");
                return;
            }

            DebugLog(
                "Bridge.Apply",
                $"enemyId={GetInstanceKey(__instance)} player={GetPlayerId(player)} state={__instance.currentState} dist={couple.Distance:0.00} follow={couple.ForceMagnitude:0.00} fixed={_fixedUpdate} horizontal={_horizontalPull}");
        }

        private static void StabilizeTumbleAtLockPoint(EnemySpinny instance, Rigidbody tumbleRb, bool fixedUpdate)
        {
            if (!fixedUpdate || instance.currentState != EnemySpinny.State.WaitForRoulette)
            {
                return;
            }

            var lockPoint = instance.playerLockPoint;
            if (lockPoint == null)
            {
                return;
            }

            var result = LastChanceMonstersLockBridgeCore.StabilizeTargetAtLockPoint(
                tumbleRb,
                lockPoint,
                instance.offLockPointTimer,
                fixedUpdate,
                isPrimaryLockState: true);
            if (!result.Applied)
            {
                return;
            }

            if (result.Kind == LastChanceMonstersLockBridgeCore.LockStabilizeKind.Emergency)
            {
                DebugLog("Bridge.LockStabilizeEmergency", $"enemyId={GetInstanceKey(instance)} dist={result.Distance:0.00} offLock={result.OffLockTimer:0.00}");
                return;
            }

            if (result.Kind == LastChanceMonstersLockBridgeCore.LockStabilizeKind.Snap)
            {
                DebugLog("Bridge.LockStabilizeSnap", $"enemyId={GetInstanceKey(instance)} dist={result.Distance:0.00}");
                return;
            }

            if (result.Kind == LastChanceMonstersLockBridgeCore.LockStabilizeKind.Force)
            {
                DebugLog("Bridge.LockStabilizeForce", $"enemyId={GetInstanceKey(instance)} dist={result.Distance:0.00} force={result.ForceMagnitude:0.00}");
            }
        }

        private static bool IsSpinnyLockState(EnemySpinny.State state)
        {
            return state == EnemySpinny.State.WaitForRoulette ||
                   state == EnemySpinny.State.Roulette ||
                   state == EnemySpinny.State.RouletteEndPause ||
                   state == EnemySpinny.State.RouletteEnd ||
                   state == EnemySpinny.State.RouletteEffect;
        }

        private static void DebugLog(string reason, string detail)
        {
            if (!InternalDebugFlags.DebugLastChanceSpinnyFlow)
            {
                return;
            }

            if (!InternalDebugFlags.DebugLastChanceSpinnyVerbose && !LogLimiter.ShouldLog($"Spinny.Bridge.{reason}", 8))
            {
                return;
            }

            Log.LogInfo($"[Spinny][{reason}] {detail}");
        }

        private static int GetPlayerId(PlayerAvatar player)
        {
            return player.photonView != null ? player.photonView.ViewID : player.GetInstanceID();
        }

        private static int GetInstanceKey(EnemySpinny instance)
        {
            return instance.GetInstanceID();
        }
    }
}

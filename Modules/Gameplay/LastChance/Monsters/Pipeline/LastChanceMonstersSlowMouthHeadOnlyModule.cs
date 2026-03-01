#nullable enable

using BepInEx.Logging;
using DHHFLastChanceMode.Modules.Config;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Adapters;
using DHHFLastChanceMode.Modules.Utilities;
using HarmonyLib;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Pipeline
{
    [HarmonyPatch]
    internal static class LastChanceMonstersSlowMouthHeadOnlyModule
    {
        private static readonly ManualLogSource Log = Logger.CreateLogSource("DHHFLastChanceMode.LastChance.SlowMouthHeadOnly");

        [HarmonyPatch(typeof(EnemySlowMouth), nameof(EnemySlowMouth.UpdateState))]
        [HarmonyPrefix]
        private static void EnemySlowMouthUpdateStatePrefix(EnemySlowMouth __instance, ref EnemySlowMouth.State newState)
        {
            if (!ShouldUseHeadOnlyBehavior(__instance))
            {
                return;
            }

            if (newState == EnemySlowMouth.State.Attack ||
                newState == EnemySlowMouth.State.Attached ||
                newState == EnemySlowMouth.State.Puke ||
                newState == EnemySlowMouth.State.Detach ||
                newState == EnemySlowMouth.State.Leave)
            {
                DebugStateRemap(__instance, "UpdateState", newState, EnemySlowMouth.State.GoToPlayer);
                newState = EnemySlowMouth.State.GoToPlayer;
            }
        }

        [HarmonyPatch(typeof(EnemySlowMouth), nameof(EnemySlowMouth.UpdateStateRPC))]
        [HarmonyPrefix]
        private static void EnemySlowMouthUpdateStateRpcPrefix(EnemySlowMouth __instance, ref EnemySlowMouth.State newState)
        {
            if (!ShouldUseHeadOnlyBehavior(__instance))
            {
                return;
            }

            if (newState == EnemySlowMouth.State.Attack ||
                newState == EnemySlowMouth.State.Attached ||
                newState == EnemySlowMouth.State.Puke ||
                newState == EnemySlowMouth.State.Detach ||
                newState == EnemySlowMouth.State.Leave)
            {
                DebugStateRemap(__instance, "UpdateStateRPC", newState, EnemySlowMouth.State.GoToPlayer);
                newState = EnemySlowMouth.State.GoToPlayer;
            }
        }

        [HarmonyPatch(typeof(EnemySlowMouth), nameof(EnemySlowMouth.StateAttack), new[] { typeof(bool) })]
        [HarmonyPrefix]
        private static bool EnemySlowMouthStateAttackPrefix(EnemySlowMouth __instance)
        {
            if (!ShouldUseHeadOnlyBehavior(__instance))
            {
                return true;
            }

            DebugDirectBlock(__instance, "StateAttack");
            __instance.UpdateState(EnemySlowMouth.State.GoToPlayer);
            return false;
        }

        [HarmonyPatch(typeof(EnemySlowMouth), nameof(EnemySlowMouth.StateAttached), new[] { typeof(bool) })]
        [HarmonyPrefix]
        private static bool EnemySlowMouthStateAttachedPrefix(EnemySlowMouth __instance)
        {
            if (!ShouldUseHeadOnlyBehavior(__instance))
            {
                return true;
            }

            DebugDirectBlock(__instance, "StateAttached");
            __instance.UpdateState(EnemySlowMouth.State.GoToPlayer);
            return false;
        }

        [HarmonyPatch(typeof(EnemySlowMouth), nameof(EnemySlowMouth.StatePuke), new[] { typeof(bool) })]
        [HarmonyPrefix]
        private static bool EnemySlowMouthStatePukePrefix(EnemySlowMouth __instance)
        {
            if (!ShouldUseHeadOnlyBehavior(__instance))
            {
                return true;
            }

            DebugDirectBlock(__instance, "StatePuke");
            __instance.UpdateState(EnemySlowMouth.State.GoToPlayer);
            return false;
        }

        [HarmonyPatch(typeof(EnemySlowMouth), nameof(EnemySlowMouth.StateDetach), new[] { typeof(bool) })]
        [HarmonyPrefix]
        private static bool EnemySlowMouthStateDetachPrefix(EnemySlowMouth __instance)
        {
            if (!ShouldUseHeadOnlyBehavior(__instance))
            {
                return true;
            }

            DebugDirectBlock(__instance, "StateDetach");
            __instance.UpdateState(EnemySlowMouth.State.GoToPlayer);
            return false;
        }

        [HarmonyPatch(typeof(EnemySlowMouthAttaching), nameof(EnemySlowMouthAttaching.Update))]
        [HarmonyPrefix]
        private static bool EnemySlowMouthAttachingUpdatePrefix(EnemySlowMouthAttaching __instance)
        {
            if (__instance == null || !ShouldUseHeadOnlyBehavior(__instance.enemySlowMouth))
            {
                return true;
            }

            DebugAttachAction(__instance.enemySlowMouth, "Attaching.Update", "ForceDetach");
            __instance.Detach();
            return false;
        }

        [HarmonyPatch(typeof(EnemySlowMouthAttaching), nameof(EnemySlowMouthAttaching.AttachToPlayer))]
        [HarmonyPrefix]
        private static bool EnemySlowMouthAttachingAttachToPlayerPrefix(EnemySlowMouthAttaching __instance)
        {
            if (__instance != null && ShouldUseHeadOnlyBehavior(__instance.enemySlowMouth))
            {
                DebugAttachAction(__instance.enemySlowMouth, "Attaching.AttachToPlayer", "Blocked");
            }

            return !ShouldUseHeadOnlyBehavior(__instance?.enemySlowMouth);
        }

        [HarmonyPatch(typeof(EnemySlowMouth), nameof(EnemySlowMouth.AttachToPlayer))]
        [HarmonyPrefix]
        private static bool EnemySlowMouthAttachToPlayerPrefix(EnemySlowMouth __instance)
        {
            if (!ShouldUseHeadOnlyBehavior(__instance))
            {
                return true;
            }

            var result = ExecuteHeadOnlyAttack(__instance);
            DebugAttachAction(__instance, "EnemySlowMouth.AttachToPlayer", result);
            return false;
        }

        private static bool ShouldUseHeadOnlyBehavior(EnemySlowMouth? slowMouth)
        {
            if (slowMouth == null)
            {
                return false;
            }

            if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
            {
                return false;
            }

            var player = ResolveBehaviorTarget(slowMouth);
            if (player != null && LastChanceMonstersTargetProxyHelper.IsHeadProxyActive(player))
            {
                return true;
            }

            return LastChanceMonstersTargetProxyHelper.IsHeadProxyActive(PlayerAvatar.instance);
        }

        private static PlayerAvatar? ResolveBehaviorTarget(EnemySlowMouth slowMouth)
        {
            if (slowMouth.playerTarget != null)
            {
                return slowMouth.playerTarget;
            }

            var enemy = slowMouth.enemy;
            if (enemy != null && enemy.TargetPlayerAvatar != null)
            {
                return enemy.TargetPlayerAvatar;
            }

            return null;
        }

        private static void DebugStateRemap(EnemySlowMouth? slowMouth, string source, EnemySlowMouth.State from, EnemySlowMouth.State to)
        {
            if (!ShouldDebug(slowMouth))
            {
                return;
            }

            var id = slowMouth!.GetInstanceID();
            if (!LogLimiter.ShouldLog($"SlowMouthHeadOnly.Remap.{source}.{id}.{from}.{to}", 30))
            {
                return;
            }

            Log.LogInfo(
                $"[SlowMouthHeadOnly] source={source} enemyId={id} remap={from}->{to} state={slowMouth.currentState} " +
                $"{BuildTargetSummary(slowMouth)}");
        }

        private static void DebugDirectBlock(EnemySlowMouth? slowMouth, string source)
        {
            if (!ShouldDebug(slowMouth))
            {
                return;
            }

            var id = slowMouth!.GetInstanceID();
            if (!LogLimiter.ShouldLog($"SlowMouthHeadOnly.Block.{source}.{id}", 30))
            {
                return;
            }

            Log.LogInfo($"[SlowMouthHeadOnly] source={source} enemyId={id} blocked=vanilla-state {BuildTargetSummary(slowMouth)}");
        }

        private static void DebugAttachAction(EnemySlowMouth? slowMouth, string source, string action)
        {
            if (!ShouldDebug(slowMouth))
            {
                return;
            }

            var id = slowMouth!.GetInstanceID();
            if (!LogLimiter.ShouldLog($"SlowMouthHeadOnly.Attach.{source}.{action}.{id}", 20))
            {
                return;
            }

            Log.LogInfo($"[SlowMouthHeadOnly] source={source} enemyId={id} action={action} {BuildTargetSummary(slowMouth)}");
        }

        private static bool ShouldDebug(EnemySlowMouth? slowMouth)
        {
            return slowMouth != null &&
                   InternalDebugFlags.DebugLastChanceHeadmanSlowMouthFlow &&
                   LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled();
        }

        private static string BuildTargetSummary(EnemySlowMouth slowMouth)
        {
            var player = ResolveBehaviorTarget(slowMouth) ?? PlayerAvatar.instance;
            var playerId = player != null && player.photonView != null ? player.photonView.ViewID : -1;
            var playerDisabled = player != null && player.isDisabled;
            var headActive = LastChanceMonstersTargetProxyHelper.IsHeadProxyActive(player);
            var state = slowMouth.currentState;
            var currentTargetDistanceToHead = -1f;
            var currentTargetDistanceToCamera = -1f;

            if (slowMouth.currentTarget != null && LastChanceMonstersTargetProxyHelper.TryGetHeadProxyVisionTarget(player, out var headVision))
            {
                currentTargetDistanceToHead = Vector3.Distance(slowMouth.currentTarget.position, headVision);
            }

            if (slowMouth.currentTarget != null && player != null && player.localCamera != null)
            {
                currentTargetDistanceToCamera = Vector3.Distance(slowMouth.currentTarget.position, player.localCamera.transform.position);
            }

            return $"state={state} targetViewId={playerId} targetDisabled={playerDisabled} headProxyActive={headActive} " +
                   $"currentTargetToHead={currentTargetDistanceToHead:F2} currentTargetToCamera={currentTargetDistanceToCamera:F2}";
        }

        private static string ExecuteHeadOnlyAttack(EnemySlowMouth slowMouth)
        {
            if (slowMouth == null || slowMouth.currentTarget == null)
            {
                return "HeadOnlyAttack.SkipNoCurrentTarget";
            }

            var distance = Vector3.Distance(slowMouth.centerTransform.position, slowMouth.currentTarget.position);
            if (distance > 2f)
            {
                return "HeadOnlyAttack.SkipDistance";
            }

            if (slowMouth.idlePukeCooldown > 0f)
            {
                return "HeadOnlyAttack.SkipCooldown";
            }

            slowMouth.idlePukePreviousState = EnemySlowMouth.State.GoToPlayer;
            slowMouth.UpdateState(EnemySlowMouth.State.IdlePuke);
            return "HeadOnlyAttack.TriggerIdlePuke";
        }
    }
}

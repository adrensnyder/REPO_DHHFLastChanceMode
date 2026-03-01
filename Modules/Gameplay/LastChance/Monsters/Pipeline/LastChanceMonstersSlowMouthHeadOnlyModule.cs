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
        private static readonly ManualLogSource Log = Logger.CreateLogSource("DHHFLastChanceMode.LastChance.SlowMouthHeadTarget");

        [HarmonyPatch(typeof(EnemySlowMouth), nameof(EnemySlowMouth.UpdatePlayerTargetRPC))]
        [HarmonyPostfix]
        private static void EnemySlowMouthUpdatePlayerTargetRpcPostfix(EnemySlowMouth __instance)
        {
            if (!ShouldApplyTargetFix(__instance))
            {
                return;
            }

            ApplyHeadTarget(__instance, "UpdatePlayerTargetRPC");
        }

        [HarmonyPatch(typeof(EnemySlowMouth), nameof(EnemySlowMouth.AttachToPlayer))]
        [HarmonyPrefix]
        private static void EnemySlowMouthAttachToPlayerPrefix(EnemySlowMouth __instance)
        {
            if (!ShouldApplyTargetFix(__instance))
            {
                return;
            }

            ApplyHeadTarget(__instance, "AttachToPlayer");
        }

        [HarmonyPatch(typeof(EnemySlowMouthAttaching), nameof(EnemySlowMouthAttaching.SetTarget))]
        [HarmonyPostfix]
        private static void EnemySlowMouthAttachingSetTargetPostfix(EnemySlowMouthAttaching __instance, PlayerAvatar _playerAvatar)
        {
            if (__instance == null || _playerAvatar == null)
            {
                return;
            }

            if (!ShouldApplyTargetFix(__instance.enemySlowMouth) ||
                !LastChanceMonstersTargetProxyHelper.IsHeadProxyActive(_playerAvatar))
            {
                return;
            }

            if (LastChanceMonstersTargetProxyHelper.TryGetHeadProxyVisionTransform(_playerAvatar, out var headVision) &&
                headVision != null)
            {
                __instance.targetTransform = headVision;
                DebugTargetApply(__instance.enemySlowMouth, "Attaching.SetTarget");
            }
        }

        private static bool ShouldApplyTargetFix(EnemySlowMouth? slowMouth)
        {
            if (slowMouth == null)
            {
                return false;
            }

            if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
            {
                return false;
            }

            var player = ResolveBehaviorTarget(slowMouth) ?? PlayerAvatar.instance;
            return LastChanceMonstersTargetProxyHelper.IsHeadProxyActive(player);
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

        private static void ApplyHeadTarget(EnemySlowMouth slowMouth, string source)
        {
            var player = ResolveBehaviorTarget(slowMouth) ?? PlayerAvatar.instance;
            if (!LastChanceMonstersTargetProxyHelper.IsHeadProxyActive(player))
            {
                return;
            }

            if (LastChanceMonstersTargetProxyHelper.TryGetHeadProxyVisionTransform(player, out var headVision) &&
                headVision != null)
            {
                slowMouth.currentTarget = headVision;
                DebugTargetApply(slowMouth, source);
            }
        }

        private static void DebugTargetApply(EnemySlowMouth? slowMouth, string source)
        {
            if (slowMouth == null ||
                !InternalDebugFlags.DebugLastChanceHeadmanSlowMouthFlow ||
                !LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
            {
                return;
            }

            var id = slowMouth.GetInstanceID();
            if (!LogLimiter.ShouldLog($"SlowMouthHeadTarget.Apply.{source}.{id}", 30))
            {
                return;
            }

            var target = ResolveBehaviorTarget(slowMouth) ?? PlayerAvatar.instance;
            var currentTargetDistanceToHead = -1f;
            var currentTargetDistanceToCamera = -1f;

            if (slowMouth.currentTarget != null &&
                target != null &&
                LastChanceMonstersTargetProxyHelper.TryGetHeadProxyVisionTarget(target, out var headVision))
            {
                currentTargetDistanceToHead = Vector3.Distance(slowMouth.currentTarget.position, headVision);
            }

            if (slowMouth.currentTarget != null && target != null && target.localCamera != null)
            {
                currentTargetDistanceToCamera = Vector3.Distance(slowMouth.currentTarget.position, target.localCamera.transform.position);
            }

            Log.LogInfo(
                $"[SlowMouthHeadTarget] source={source} enemyId={id} state={slowMouth.currentState} " +
                $"targetDisabled={(target != null && target.isDisabled)} headProxyActive={LastChanceMonstersTargetProxyHelper.IsHeadProxyActive(target)} " +
                $"currentTargetToHead={currentTargetDistanceToHead:F2} currentTargetToCamera={currentTargetDistanceToCamera:F2}");
        }
    }
}

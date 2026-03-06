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
    internal static class LastChanceMonstersVisionAnchorProxyModule
    {
        private static readonly ManualLogSource Log = Logger.CreateLogSource("DHHFLastChanceMode.LastChance.VisionAnchor");

        [HarmonyPatch(typeof(PlayerVisionTarget), nameof(PlayerVisionTarget.Update))]
        [HarmonyPostfix]
        private static void PlayerVisionTargetUpdatePostfix(PlayerVisionTarget __instance)
        {
            if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled() || __instance == null)
            {
                return;
            }

            var player = __instance.PlayerAvatar;
            if (!LastChanceMonstersTargetProxyHelper.IsHeadProxyActive(player))
            {
                return;
            }

            var visionTransform = __instance.VisionTransform;
            if (visionTransform == null)
            {
                return;
            }

            if (!LastChanceMonstersTargetProxyHelper.TryGetHeadProxyVisionTransform(player, out var headVision) || headVision == null)
            {
                return;
            }

            var delta = Vector3.Distance(visionTransform.position, headVision.position);
            visionTransform.position = headVision.position;
            visionTransform.rotation = headVision.rotation;
            DebugAnchorApply(player, "PlayerVisionTarget.Update", delta);
        }

        [HarmonyPatch(typeof(SemiFunc), nameof(SemiFunc.PlayerGetFaceEyeTransform), new[] { typeof(PlayerAvatar) })]
        [HarmonyPostfix]
        private static void PlayerGetFaceEyeTransformPostfix(PlayerAvatar _player, ref Transform __result)
        {
            if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled() || !LastChanceMonstersTargetProxyHelper.IsHeadProxyActive(_player))
            {
                return;
            }

            if (LastChanceMonstersTargetProxyHelper.TryGetHeadProxyVisionTransform(_player, out var headVision) && headVision != null)
            {
                var delta = __result != null ? Vector3.Distance(__result.position, headVision.position) : -1f;
                __result = headVision;
                DebugAnchorApply(_player, "SemiFunc.PlayerGetFaceEyeTransform", delta);
            }
        }

        private static void DebugAnchorApply(PlayerAvatar? player, string source, float delta)
        {
            if (!InternalDebugFlags.DebugLastChanceHeadmanSlowMouthFlow || player == null)
            {
                return;
            }

            var viewId = player.photonView != null ? player.photonView.ViewID : player.GetInstanceID();
            if (!LogLimiter.ShouldLog($"VisionAnchor.{source}.{viewId}", 120))
            {
                return;
            }

            var headActive = LastChanceMonstersTargetProxyHelper.IsHeadProxyActive(player);
            Log.LogInfo(
                $"[VisionAnchor] source={source} playerViewId={viewId} isLocal={player.isLocal} " +
                $"headProxyActive={headActive} delta={delta:F3}");
        }
    }
}

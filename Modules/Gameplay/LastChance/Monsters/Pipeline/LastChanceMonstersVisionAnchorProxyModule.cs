#nullable enable

using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Adapters;
using HarmonyLib;
using UnityEngine;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Pipeline
{
    [HarmonyPatch]
    internal static class LastChanceMonstersVisionAnchorProxyModule
    {
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

            visionTransform.position = headVision.position;
            visionTransform.rotation = headVision.rotation;
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
                __result = headVision;
            }
        }
    }
}

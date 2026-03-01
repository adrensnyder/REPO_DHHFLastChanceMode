#nullable enable

using System.Collections.Generic;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Support;
using HarmonyLib;
using UnityEngine;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Pipeline
{
    [HarmonyPatch(typeof(SemiFunc), nameof(SemiFunc.PlayerGetAllPlayerAvatarWithinRange), new[] { typeof(float), typeof(Vector3), typeof(bool), typeof(LayerMask) })]
    internal static class LastChanceMonstersSharedPlayerSearchModule
    {
        [HarmonyPostfix]
        private static void Postfix(float range, Vector3 position, bool doRaycastCheck, LayerMask layerMask, ref List<PlayerAvatar> __result)
        {
            __result ??= new List<PlayerAvatar>();
            LastChanceMonstersTargetingOrchestrator.ExtendPlayersWithinRangeLastChanceAware(__result, range, position, doRaycastCheck, layerMask);
        }
    }

    [HarmonyPatch(typeof(SemiFunc), nameof(SemiFunc.PlayerGetNearestPlayerAvatarWithinRange), new[] { typeof(float), typeof(Vector3), typeof(bool), typeof(LayerMask) })]
    internal static class LastChanceMonstersEffectiveTargetPointModule
    {
        [HarmonyPostfix]
        private static void Postfix(float range, Vector3 position, bool doRaycastCheck, LayerMask layerMask, ref PlayerAvatar? __result)
        {
            __result = LastChanceMonstersTargetingOrchestrator.ResolveNearestPlayerWithinRangeLastChanceAware(
                __result,
                range,
                position,
                doRaycastCheck,
                layerMask);
        }
    }
}

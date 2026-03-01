#nullable enable

using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Adapters;
using HarmonyLib;
using UnityEngine;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Interactions
{
    [HarmonyPatch]
    internal static class LastChanceMonstersHiddenDestinationModule
    {
        private const float FallbackMinDistance = 8f;
        private const float FallbackMaxDistance = 999f;

        [HarmonyPatch(typeof(SemiFunc), nameof(SemiFunc.LevelPointGetPlayerDistance), new[] { typeof(Vector3), typeof(float), typeof(float), typeof(bool) })]
        internal static class SemiFuncLevelPointGetPlayerDistancePatch
        {
            [HarmonyPostfix]
            private static void Postfix(Vector3 _position, ref LevelPoint? __result)
            {
                if (__result != null || !LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
                {
                    return;
                }

                __result = SemiFunc.LevelPointGet(_position, FallbackMinDistance, FallbackMaxDistance);
            }
        }

        [HarmonyPatch(typeof(SemiFunc), nameof(SemiFunc.LevelPointGetFurthestFromPlayer), new[] { typeof(Vector3), typeof(float) })]
        internal static class SemiFuncLevelPointGetFurthestFromPlayerPatch
        {
            [HarmonyPostfix]
            private static void Postfix(Vector3 _position, ref LevelPoint? __result)
            {
                if (__result != null || !LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
                {
                    return;
                }

                __result = SemiFunc.LevelPointGet(_position, FallbackMinDistance, FallbackMaxDistance);
            }
        }
    }
}

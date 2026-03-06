#nullable enable

using DeathHeadHopper.UI;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Runtime;
using HarmonyLib;

namespace DHHFLastChanceMode.Modules.Gameplay.Core.Abilities
{
    [HarmonyPatch(typeof(AbilitySpot), "Awake")]
    internal static class AbilitySpotAwakeRegistryPatch
    {
        [HarmonyPostfix]
        private static void Postfix(AbilitySpot __instance)
        {
            LastChanceRuntimeObjectRegistry.RegisterAbilitySpot(__instance);
        }
    }
}

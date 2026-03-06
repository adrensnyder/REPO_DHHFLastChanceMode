#nullable enable

using DHHFLastChanceMode.Modules.Gameplay.LastChance.Runtime;
using HarmonyLib;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Adapters
{
    [HarmonyPatch(typeof(Enemy), "Awake")]
    internal static class LastChanceEnemyAwakeRegistryPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Enemy __instance)
        {
            LastChanceRuntimeObjectRegistry.RegisterEnemy(__instance);
        }
    }

    [HarmonyPatch(typeof(EnemyAnimal), "Awake")]
    internal static class LastChanceEnemyAnimalAwakeRegistryPatch
    {
        [HarmonyPostfix]
        private static void Postfix(EnemyAnimal __instance)
        {
            LastChanceRuntimeObjectRegistry.RegisterEnemyAnimal(__instance);
        }
    }

    [HarmonyPatch(typeof(PlayerVoiceChat), "Awake")]
    internal static class LastChancePlayerVoiceChatAwakeRegistryPatch
    {
        [HarmonyPostfix]
        private static void Postfix(PlayerVoiceChat __instance)
        {
            LastChanceRuntimeObjectRegistry.RegisterPlayerVoiceChat(__instance);
        }
    }

    [HarmonyPatch(typeof(PlayerVoiceChat), "OnDestroy")]
    internal static class LastChancePlayerVoiceChatDestroyRegistryPatch
    {
        [HarmonyPrefix]
        private static void Prefix(PlayerVoiceChat __instance)
        {
            LastChanceRuntimeObjectRegistry.UnregisterPlayerVoiceChat(__instance);
        }
    }
}

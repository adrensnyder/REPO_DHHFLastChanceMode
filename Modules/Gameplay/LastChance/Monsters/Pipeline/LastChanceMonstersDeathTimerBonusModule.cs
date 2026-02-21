#nullable enable

using HarmonyLib;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Runtime;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Pipeline
{
    [HarmonyPatch(typeof(EnemyHealth), "DeathImpulseRPC")]
    internal static class LastChanceMonstersDeathTimerBonusModule
    {
        [HarmonyPostfix]
        private static void Postfix(EnemyHealth __instance)
        {
            if (__instance == null)
            {
                return;
            }

            if (!LastChanceRuntimeOrchestrator.IsRuntimeActive)
            {
                return;
            }

            LastChanceTimerController.TryApplyMonsterDeathTimerBonusHost();
        }
    }
}

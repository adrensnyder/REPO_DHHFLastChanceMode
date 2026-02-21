#nullable enable

using HarmonyLib;

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

            LastChanceTimerController.TryApplyMonsterDeathTimerBonusHost();
        }
    }
}

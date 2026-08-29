#nullable enable

using HarmonyLib;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Runtime
{
    [HarmonyPatch(typeof(ShopManager), nameof(ShopManager.ShopInitialize))]
    internal static class LastChanceConsolationMoneyShopPatch
    {
        [HarmonyPrefix]
        private static void Prefix(ShopManager __instance)
        {
            LastChanceTimerController.TryApplyPendingConsolationMoney(__instance);
        }
    }
}

#nullable enable

using HarmonyLib;

namespace DHHFLastChanceMode.Modules.Config
{
    [HarmonyPatch(typeof(MenuPageLobby), nameof(MenuPageLobby.ButtonStart))]
    internal static class CompatibilityGateStartHookPatch
    {
        private static void Prefix()
        {
            CompatibilityGate.ForceResolvePendingPresenceForStart();
        }
    }
}

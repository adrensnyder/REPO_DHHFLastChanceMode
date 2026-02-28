#nullable enable

using DHHFLastChanceMode.Modules.Config;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Adapters;
using DHHFLastChanceMode.Modules.Utilities;
using HarmonyLib;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Pipeline
{
    [HarmonyPatch(typeof(EnemyThinMan), nameof(EnemyThinMan.StateStand))]
    internal static class LastChanceMonstersThinManStandModule
    {
        private static readonly System.Collections.Generic.Dictionary<int, int> s_lastTransitionFrameByEnemy = new();

        internal static void ResetRuntimeState()
        {
            s_lastTransitionFrameByEnemy.Clear();
        }

        [HarmonyPostfix]
        private static void Postfix(EnemyThinMan __instance)
        {
            if (__instance == null || !LastChanceMonstersTargetProxyHelper.IsRuntimeMasterContextEnabled())
            {
                return;
            }

            if (EnemyDirector.instance != null && EnemyDirector.instance.debugNoVision)
            {
                return;
            }

            if (__instance.playerTarget is PlayerAvatar)
            {
                return;
            }

            var onScreen = __instance.enemy?.OnScreen;
            if (onScreen == null)
            {
                return;
            }

            var players = GameDirector.instance?.PlayerList;
            if (players == null)
            {
                return;
            }

            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player == null)
                {
                    continue;
                }

                var eligible = !LastChanceMonstersTargetProxyHelper.IsDisabled(player) || LastChanceMonstersTargetProxyHelper.IsHeadProxyActive(player);
                if (!eligible || !onScreen.GetOnScreen(player))
                {
                    continue;
                }

                __instance.SetTarget(player);
                __instance.UpdateState(EnemyThinMan.State.OnScreen);

                if (InternalDebugFlags.DebugLastChanceThinManFlow)
                {
                    var enemyId = __instance.GetInstanceID();
                    var frame = UnityEngine.Time.frameCount;
                    if (!s_lastTransitionFrameByEnemy.TryGetValue(enemyId, out var lastFrame) || frame - lastFrame >= 120)
                    {
                        s_lastTransitionFrameByEnemy[enemyId] = frame;
                        LastChanceMonstersOnScreenCameraModule.DebugLog(
                            "StateStand.ProxyAcquire",
                            $"enemy={__instance.gameObject.name} player={(player.photonView != null ? player.photonView.ViewID : -1)} fromDisabled={LastChanceMonstersTargetProxyHelper.IsDisabled(player)} headProxy={LastChanceMonstersTargetProxyHelper.IsHeadProxyActive(player)}");
                    }
                }

                return;
            }
        }
    }
}

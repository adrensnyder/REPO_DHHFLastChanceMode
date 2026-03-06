#nullable enable

using HarmonyLib;
using UnityEngine;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Adapters
{
    [HarmonyPatch(typeof(EnemyDirector), nameof(EnemyDirector.Update))]
    internal static class LastChanceMonstersBodyPositionProxyModule
    {
        internal static void ResetRuntimeState()
        {
            LastChanceMonstersDiscoveryCache.InvalidateEnemies();
        }

        [HarmonyPostfix]
        private static void Postfix()
        {
            if (!LastChanceMonstersTargetProxyHelper.IsRuntimeMasterContextEnabled())
            {
                ResetRuntimeState();
                return;
            }

            var players = GameDirector.instance?.PlayerList;
            if (players == null || players.Count == 0)
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

                if (!LastChanceMonstersTargetProxyHelper.TryGetHeadProxyTarget(player, out var headCenter))
                {
                    continue;
                }

                player.transform.position = headCenter;
            }
        }
    }
}


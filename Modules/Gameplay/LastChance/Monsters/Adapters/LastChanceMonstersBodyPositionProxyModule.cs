#nullable enable

using HarmonyLib;
using UnityEngine;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Adapters
{
    [HarmonyPatch(typeof(EnemyDirector), nameof(EnemyDirector.Update))]
    internal static class LastChanceMonstersBodyPositionProxyModule
    {
        private const float TargetScanIntervalSeconds = 1f;

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

                // General safety: if any enemy is already tracking this player as target,
                // avoid rewriting the body position to prevent steering/path regressions.
                if (IsTargetedByAnyEnemy(player))
                {
                    continue;
                }

                player.transform.position = headCenter;
            }
        }

        private static bool IsTargetedByAnyEnemy(PlayerAvatar player)
        {
            if (player == null)
            {
                return false;
            }

            var enemies = LastChanceMonstersDiscoveryCache.GetEnemies(TargetScanIntervalSeconds);
            var animals = LastChanceMonstersDiscoveryCache.GetEnemyAnimals(TargetScanIntervalSeconds);

            for (var i = 0; i < enemies.Length; i++)
            {
                var enemy = enemies[i];
                if (enemy == null)
                {
                    continue;
                }

                if (enemy.TargetPlayerAvatar == player)
                {
                    return true;
                }
            }

            // EnemyAnimal keeps its own target field on the EnemyAnimal component.
            for (var i = 0; i < animals.Length; i++)
            {
                var animal = animals[i];
                if (animal == null)
                {
                    continue;
                }

                if (animal.playerTarget == player)
                {
                    return true;
                }
            }

            return false;
        }
    }
}


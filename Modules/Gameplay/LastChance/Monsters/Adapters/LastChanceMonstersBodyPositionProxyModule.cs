#nullable enable

using HarmonyLib;
using System;
using UnityEngine;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Adapters
{
    [HarmonyPatch(typeof(EnemyDirector), nameof(EnemyDirector.Update))]
    internal static class LastChanceMonstersBodyPositionProxyModule
    {
        private const float TargetScanIntervalSeconds = 1f;
        private static Enemy[] s_cachedEnemies = Array.Empty<Enemy>();
        private static EnemyAnimal[] s_cachedAnimals = Array.Empty<EnemyAnimal>();
        private static float s_nextTargetScanAt;

        [HarmonyPostfix]
        private static void Postfix()
        {
            if (!LastChanceMonstersTargetProxyHelper.IsRuntimeMasterContextEnabled())
            {
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

            RefreshTargetCachesIfDue();

            for (var i = 0; i < s_cachedEnemies.Length; i++)
            {
                var enemy = s_cachedEnemies[i];
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
            for (var i = 0; i < s_cachedAnimals.Length; i++)
            {
                var animal = s_cachedAnimals[i];
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

        private static void RefreshTargetCachesIfDue()
        {
            var now = Time.unscaledTime;
            if (now < s_nextTargetScanAt)
            {
                return;
            }

            s_cachedEnemies = UnityEngine.Object.FindObjectsOfType<Enemy>();
            s_cachedAnimals = UnityEngine.Object.FindObjectsOfType<EnemyAnimal>();
            s_nextTargetScanAt = now + TargetScanIntervalSeconds;
        }
    }
}


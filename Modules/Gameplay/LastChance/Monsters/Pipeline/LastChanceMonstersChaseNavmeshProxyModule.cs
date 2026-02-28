#nullable enable

using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Adapters;
using HarmonyLib;
using UnityEngine;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Pipeline
{
    [HarmonyPatch(typeof(EnemyStateChase), "Update")]
    internal static class LastChanceMonstersChaseNavmeshProxyModule
    {
        [HarmonyPrefix]
        private static void Prefix(EnemyStateChase __instance)
        {
            if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
            {
                return;
            }

            var enemy = __instance.Enemy;
            if (enemy == null || enemy.CurrentState != EnemyState.Chase)
            {
                return;
            }

            var player = enemy.TargetPlayerAvatar;
            if (player == null)
            {
                return;
            }

            if (!LastChanceMonstersTargetProxyHelper.TryGetHeadProxyTarget(player, out var headCenter))
            {
                return;
            }

            player.LastNavmeshPosition = headCenter;
            player.LastNavMeshPositionTimer = 0f;
        }
    }
}


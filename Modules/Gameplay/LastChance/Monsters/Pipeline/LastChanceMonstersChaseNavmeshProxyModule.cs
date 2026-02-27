#nullable enable

using BepInEx.Logging;
using DHHFLastChanceMode.Modules.Config;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Adapters;
using DHHFLastChanceMode.Modules.Utilities;
using HarmonyLib;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Pipeline
{
    [HarmonyPatch(typeof(EnemyStateChase), "Update")]
    internal static class LastChanceMonstersChaseNavmeshProxyModule
    {
        private static readonly ManualLogSource Log = Logger.CreateLogSource("DHHFLastChanceMode.LastChance.Headman");

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

            if (InternalDebugFlags.DebugLastChanceHeadmanFlow)
            {
                var key = $"Headman.NavmeshProxy.{enemy.GetInstanceID()}";
                if (InternalDebugFlags.DebugLastChanceHeadmanVerbose || LogLimiter.ShouldLog(key, 10))
                {
                    Log.LogInfo(
                        $"[Headman][NavmeshProxy] enemyId={enemy.GetInstanceID()} player={player.name} " +
                        $"head={headCenter} body={player.transform.position}");
                }
            }
        }
    }
}


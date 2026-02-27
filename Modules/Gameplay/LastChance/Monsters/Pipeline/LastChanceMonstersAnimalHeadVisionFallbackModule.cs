#nullable enable

using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Pipeline
{
    [HarmonyPatch(typeof(EnemyAnimal), "Update")]
    internal static class LastChanceMonstersAnimalHeadVisionFallbackModule
    {
        private const float ProbeInterval = 0.2f;
        private static readonly Dictionary<int, float> NextProbeAtByAnimal = new();

        [HarmonyPostfix]
        private static void UpdatePostfix(EnemyAnimal __instance)
        {
            if (__instance == null)
            {
                return;
            }

            var key = __instance.GetInstanceID();
            if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled() ||
                !LastChanceMonstersTargetProxyHelper.IsMasterContext() ||
                !IsStateEligible(__instance.currentState))
            {
                NextProbeAtByAnimal.Remove(key);
                return;
            }

            var now = Time.unscaledTime;
            if (NextProbeAtByAnimal.TryGetValue(key, out var nextAt) && now < nextAt)
            {
                return;
            }

            NextProbeAtByAnimal[key] = now + ProbeInterval;

            var enemy = __instance.enemy;
            var vision = enemy?.Vision;
            var visionTransform = vision?.VisionTransform;
            if (enemy == null || vision == null || visionTransform == null)
            {
                return;
            }

            var players = GameDirector.instance?.PlayerList;
            if (players == null || players.Count == 0)
            {
                return;
            }

            var visionMask = enemy.VisionMask;

            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player == null || !LastChanceMonstersTargetProxyHelper.IsHeadProxyActive(player))
                {
                    continue;
                }

                if (!LastChanceMonstersTargetProxyHelper.TryGetHeadProxyVisionTarget(player, out var headCenter))
                {
                    continue;
                }

                var delta = headCenter - visionTransform.position;
                var distance = delta.magnitude;
                if (distance > vision.VisionDistance || distance <= 0.001f)
                {
                    continue;
                }

                var direction = delta / distance;
                var near = distance <= vision.VisionDistanceClose;
                var inCone = Vector3.Dot(visionTransform.forward, direction) >= vision.VisionDotStanding || near;
                if (!inCone)
                {
                    continue;
                }

                if (!LastChanceMonstersTargetProxyHelper.IsLineOfSightToHead(visionTransform, headCenter, visionMask, player))
                {
                    continue;
                }

                vision.onVisionTriggeredPlayer = player;
                vision.onVisionTriggeredID = player.photonView != null ? player.photonView.ViewID : player.GetInstanceID();
                LastChanceMonstersTargetProxyHelper.EnsureVisionTriggered(vision, player, near);
                __instance.OnVision();
                return;
            }
        }

        private static bool IsStateEligible(EnemyAnimal.State state)
        {
            return state == EnemyAnimal.State.Idle ||
                   state == EnemyAnimal.State.Roam ||
                   state == EnemyAnimal.State.Investigate ||
                   state == EnemyAnimal.State.Leave;
        }
    }
}

#nullable enable

using System.Collections.Generic;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Adapters;
using UnityEngine;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Support
{
    internal static class LastChanceMonstersTargetingOrchestrator
    {
        internal static Vector3 ResolveEffectiveTransformTargetPoint(Transform? transform)
        {
            return LastChanceMonstersTargetProxyHelper.ResolveEffectiveTransformTargetPosition(transform);
        }

        internal static List<PlayerAvatar> GetAllPlayersWithinRangeLastChanceAware(float range, Vector3 position, bool doRaycastCheck, LayerMask layerMask)
        {
            var list = SemiFunc.PlayerGetAllPlayerAvatarWithinRange(range, position, doRaycastCheck, layerMask) ?? new List<PlayerAvatar>();
            if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
            {
                return list;
            }

            var players = GameDirector.instance?.PlayerList;
            if (players == null || players.Count == 0)
            {
                return list;
            }

            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player == null || list.Contains(player))
                {
                    continue;
                }

                if (!LastChanceMonstersTargetProxyHelper.TryGetHeadProxyTarget(player, out var headCenter))
                {
                    continue;
                }

                var dist = Vector3.Distance(position, headCenter);
                if (dist > range)
                {
                    continue;
                }

                if (doRaycastCheck && IsWallBlocking(position, headCenter, dist, layerMask))
                {
                    continue;
                }

                list.Add(player);
            }

            return list;
        }

        internal static PlayerAvatar? GetNearestPlayerWithinRangeLastChanceAware(float range, Vector3 position, bool doRaycastCheck, LayerMask layerMask)
        {
            var list = GetAllPlayersWithinRangeLastChanceAware(range, position, doRaycastCheck, layerMask);
            if (list.Count == 0)
            {
                return null;
            }

            var bestDistance = range;
            PlayerAvatar? bestPlayer = null;
            for (var i = 0; i < list.Count; i++)
            {
                var player = list[i];
                if (player == null)
                {
                    continue;
                }

                var point = ResolveDistancePoint(player);
                var dist = Vector3.Distance(position, point);
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    bestPlayer = player;
                }
            }

            return bestPlayer;
        }

        private static bool IsWallBlocking(Vector3 origin, Vector3 target, float distance, LayerMask layerMask)
        {
            var direction = target - origin;
            var hits = Physics.RaycastAll(origin, direction, distance, layerMask, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < hits.Length; i++)
            {
                var hitTransform = hits[i].collider?.transform;
                if (hitTransform != null && hitTransform.CompareTag("Wall"))
                {
                    return true;
                }
            }

            return false;
        }

        private static Vector3 ResolveDistancePoint(PlayerAvatar player)
        {
            if (LastChanceMonstersTargetProxyHelper.TryGetHeadProxyTarget(player, out var headCenter))
            {
                return headCenter;
            }

            var vision = player.PlayerVisionTarget?.VisionTransform;
            if (vision != null)
            {
                return vision.position;
            }

            return player.transform.position;
        }
    }
}

#nullable enable

using System;
using System.Collections.Generic;
using DHHFLastChanceMode.Modules.Config;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Runtime;
using UnityEngine;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Adapters
{
    internal static class LastChanceMonstersTargetProxyHelper
    {
        private const float EnemyDiscoveryRefreshSeconds = 1f;

        private static bool IsRuntimeGateEnabled()
        {
            return FeatureFlags.LastChanceMonstersSearchEnabled &&
                   FeatureFlags.LastChangeMode &&
                   LastChanceRuntimeOrchestrator.IsRuntimeActive;
        }

        internal static bool IsRuntimeEnabled()
        {
            return IsRuntimeGateEnabled();
        }

        internal static bool IsMasterContext()
        {
            return SemiFunc.IsMasterClientOrSingleplayer();
        }

        internal static bool IsRuntimeMasterContextEnabled()
        {
            return IsRuntimeEnabled() && IsMasterContext();
        }

        internal static bool IsRuntimeFeatureEnabled(bool featureEnabled)
        {
            return featureEnabled && IsRuntimeEnabled();
        }

        internal static bool IsRuntimeMasterFeatureEnabled(bool featureEnabled)
        {
            return featureEnabled && IsRuntimeMasterContextEnabled();
        }

        internal static bool IsDisabled(PlayerAvatar? player)
        {
            return player != null && player.isDisabled;
        }

        internal static bool IsHeadProxyActive(PlayerAvatar? player)
        {
            if (player == null)
            {
                return false;
            }

            var head = player.playerDeathHead;
            if (head == null)
            {
                return player.deadSet || IsDisabled(player);
            }

            if (player.deadSet || IsDisabled(player))
            {
                return true;
            }

            return head.triggered || head.physGrabObject != null || head.gameObject.activeInHierarchy;
        }

        internal static bool TryGetHeadCenter(PlayerAvatar? player, out Vector3 center)
        {
            center = default;
            if (player == null)
            {
                return false;
            }

            var head = player.playerDeathHead;
            if (head == null)
            {
                return false;
            }

            var phys = head.physGrabObject;
            if (phys != null)
            {
                center = phys.centerPoint;
                return true;
            }

            center = head.transform.position;
            return true;
        }

        internal static bool TryGetHeadProxyTarget(PlayerAvatar? player, out Vector3 center)
        {
            center = default;
            return IsRuntimeEnabled() && IsHeadProxyActive(player) && TryGetHeadCenter(player, out center);
        }

        internal static bool TryGetHeadProxyVisionTarget(PlayerAvatar? player, out Vector3 point)
        {
            point = default;
            if (!IsRuntimeEnabled() || !IsHeadProxyActive(player) || player?.playerDeathHead == null)
            {
                return false;
            }

            var eyes = player.playerDeathHead.playerEyes;
            if (eyes != null)
            {
                point = eyes.transform.position;
                return true;
            }

            return TryGetHeadCenter(player, out point);
        }

        internal static bool TryGetHeadProxyVisionTransform(PlayerAvatar? player, out Transform? transform)
        {
            transform = null;
            if (!IsRuntimeEnabled() || !IsHeadProxyActive(player) || player?.playerDeathHead == null)
            {
                return false;
            }

            var eyes = player.playerDeathHead.playerEyes;
            if (eyes != null)
            {
                transform = eyes.transform;
                return transform != null;
            }

            transform = player.playerDeathHead.transform;
            return transform != null;
        }

        internal static bool TryGetHeadProxyTransform(PlayerAvatar? player, out Transform? transform)
        {
            transform = null;
            if (!IsRuntimeEnabled() || !IsHeadProxyActive(player) || player?.playerDeathHead == null)
            {
                return false;
            }

            transform = player.playerDeathHead.transform;
            return transform != null;
        }

        internal static bool TryGetHeadProxyPhysGrabObject(PlayerAvatar? player, out PhysGrabObject? physGrabObject)
        {
            physGrabObject = null;
            if (!IsRuntimeEnabled() || !IsHeadProxyActive(player) || player?.playerDeathHead == null)
            {
                return false;
            }

            physGrabObject = player.playerDeathHead.physGrabObject;
            return physGrabObject != null;
        }

        internal static bool TryGetPlayerFromDeathHeadCollider(Collider? other, out PlayerAvatar? player)
        {
            player = null;
            if (other == null)
            {
                return false;
            }

            var deathHead = other.GetComponentInParent<PlayerDeathHead>();
            if (deathHead == null)
            {
                return false;
            }

            player = deathHead.playerAvatar;
            return player != null;
        }

        internal static bool TryResolvePlayerAvatarFromTransform(Transform? transform, out PlayerAvatar? player)
        {
            player = null;
            if (transform == null)
            {
                return false;
            }

            var direct = transform.GetComponentInParent<PlayerAvatar>();
            if (direct != null)
            {
                player = direct;
                return true;
            }

            var controller = transform.GetComponentInParent<PlayerController>();
            if (controller != null)
            {
                var fromController = controller.playerAvatarScript;
                if (fromController != null)
                {
                    player = fromController;
                    return true;
                }
            }

            var visionTarget = transform.GetComponentInParent<PlayerVisionTarget>();
            if (visionTarget != null && visionTarget.PlayerAvatar != null)
            {
                player = visionTarget.PlayerAvatar;
                return true;
            }

            return false;
        }

        internal static Vector3 ResolveEffectivePlayerTargetPosition(PlayerAvatar? player)
        {
            if (player == null)
            {
                return Vector3.zero;
            }

            if (!IsRuntimeEnabled())
            {
                return player.transform.position;
            }

            return TryGetHeadProxyTarget(player, out var headCenter)
                ? headCenter
                : player.transform.position;
        }

        internal static Vector3 ResolveEffectiveTransformTargetPosition(Transform? transform)
        {
            if (transform == null)
            {
                return Vector3.zero;
            }

            if (!IsRuntimeEnabled())
            {
                return transform.position;
            }

            TryResolvePlayerAvatarFromTransform(transform, out var player);
            if (player != null && TryGetHeadProxyTarget(player, out var headCenter))
            {
                return headCenter;
            }

            return transform.position;
        }

        internal static bool IsLineOfSightToHead(Transform origin, Vector3 headCenter, LayerMask visionMask, PlayerAvatar player)
        {
            var dir = headCenter - origin.position;
            var dist = dir.magnitude;
            if (dist <= 0.001f)
            {
                return true;
            }

            var hits = Physics.RaycastAll(origin.position, dir.normalized, dist, visionMask, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < hits.Length; i++)
            {
                var t = hits[i].transform;
                if (t == null)
                {
                    continue;
                }

                if (t.CompareTag("Enemy"))
                {
                    continue;
                }

                // Consider the target DeathHead colliders transparent for LOS checks.
                var hitHead = t.GetComponentInParent<PlayerDeathHead>();
                if (hitHead != null && hitHead == player.playerDeathHead)
                {
                    continue;
                }

                if (t.GetComponentInParent<PlayerTumble>() != null)
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        internal static void EnsureVisionTriggered(EnemyVision vision, PlayerAvatar player, bool near)
        {
            if (vision == null || player == null || player.photonView == null)
            {
                return;
            }

            var viewId = player.photonView.ViewID;
            if (!vision.VisionTriggered.ContainsKey(viewId))
            {
                vision.VisionTriggered[viewId] = false;
            }

            if (!vision.VisionsTriggered.ContainsKey(viewId))
            {
                vision.VisionsTriggered[viewId] = 0;
            }

            vision.VisionTrigger(viewId, player, culled: false, playerNear: near);
        }

        internal static IEnumerable<Enemy> EnumerateEnemies()
        {
            var all = LastChanceMonstersDiscoveryCache.GetEnemies(EnemyDiscoveryRefreshSeconds);
            for (var i = 0; i < all.Length; i++)
            {
                var enemy = all[i];
                if (enemy != null)
                {
                    yield return enemy;
                }
            }
        }
    }
}


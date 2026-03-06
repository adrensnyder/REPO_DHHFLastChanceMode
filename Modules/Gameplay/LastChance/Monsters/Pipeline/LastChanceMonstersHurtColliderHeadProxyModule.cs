#nullable enable

using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Pipeline
{
    [HarmonyPatch(typeof(HurtCollider), nameof(HurtCollider.Update))]
    internal static class LastChanceMonstersHurtColliderHeadProxyModule
    {
        [HarmonyPostfix]
        private static void UpdatePostfix(HurtCollider __instance)
        {
            if (__instance == null)
            {
                return;
            }

            if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
            {
                return;
            }

            if (!__instance.isActiveAndEnabled || !__instance.playerLogic || __instance.playerDamageCooldown <= 0f)
            {
                return;
            }

            var overlaps = CollectOverlaps(__instance, __instance.LayerMask);
            if (overlaps == null || overlaps.Length == 0)
            {
                return;
            }

            var processedPlayers = new HashSet<int>();
            for (var i = 0; i < overlaps.Length; i++)
            {
                var collider = overlaps[i];
                if (collider == null)
                {
                    continue;
                }

                var player = ResolvePlayer(collider);
                if (player == null && LastChanceMonstersTargetProxyHelper.TryGetPlayerFromDeathHeadCollider(collider, out var headPlayer))
                {
                    player = headPlayer;
                }

                if (player == null)
                {
                    continue;
                }

                if (!LastChanceMonstersTargetProxyHelper.IsDisabled(player) || !LastChanceMonstersTargetProxyHelper.IsHeadProxyActive(player))
                {
                    continue;
                }

                var key = player.photonView != null ? player.photonView.ViewID : player.GetInstanceID();
                if (!processedPlayers.Add(key))
                {
                    continue;
                }

                __instance.PlayerHurt(player);
            }
        }

        private static Collider[]? CollectOverlaps(HurtCollider instance, LayerMask mask)
        {
            if (instance.ColliderIsBox)
            {
                var box = instance.BoxCollider;
                if (box == null)
                {
                    return null;
                }

                var center = instance.transform.TransformPoint(box.center);
                var scaledSize = new Vector3(
                    instance.transform.lossyScale.x * box.size.x,
                    instance.transform.lossyScale.y * box.size.y,
                    instance.transform.lossyScale.z * box.size.z);
                return Physics.OverlapBox(center, scaledSize * 0.5f, instance.transform.rotation, mask, QueryTriggerInteraction.Collide);
            }

            var sphere = instance.SphereCollider;
            if (sphere == null)
            {
                return null;
            }

            var centerSphere = sphere.bounds.center;
            var radius = instance.transform.lossyScale.x * sphere.radius;
            return Physics.OverlapSphere(centerSphere, radius, mask, QueryTriggerInteraction.Collide);
        }

        private static PlayerAvatar? ResolvePlayer(Collider collider)
        {
            var avatar = collider.GetComponentInParent<PlayerAvatar>();
            if (avatar != null)
            {
                return avatar;
            }

            var controller = collider.GetComponentInParent<PlayerController>();
            if (controller?.playerAvatarScript != null)
            {
                return controller.playerAvatarScript;
            }

            var trigger = collider.GetComponent<PlayerTrigger>() ?? collider.GetComponentInParent<PlayerTrigger>();
            return trigger?.PlayerAvatar;
        }
    }
}

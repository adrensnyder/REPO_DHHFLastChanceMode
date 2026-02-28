#nullable enable

using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Adapters;
using HarmonyLib;
using UnityEngine;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Interactions
{
    [HarmonyPatch]
    internal static class LastChanceMonstersBirthdayBoyBalloonHeadProxyModule
    {
        [HarmonyPatch(typeof(BirthdayBoyBalloon), nameof(BirthdayBoyBalloon.Update))]
        [HarmonyPostfix]
        private static void UpdatePostfix(BirthdayBoyBalloon __instance)
        {
            if (__instance == null || __instance.popped)
            {
                return;
            }

            if (!LastChanceMonstersTargetProxyHelper.IsRuntimeMasterContextEnabled())
            {
                return;
            }

            if (!TryGetOverlappingHeadPopper(__instance, out var popper) || popper == null)
            {
                return;
            }

            __instance.popped = true;
            __instance.popper = popper;
        }

        [HarmonyPatch(typeof(BirthdayBoyBalloon), nameof(BirthdayBoyBalloon.OnTriggerEnter))]
        [HarmonyPrefix]
        private static bool OnTriggerEnterPrefix(BirthdayBoyBalloon __instance, Collider _other)
        {
            if (__instance == null || _other == null || __instance.popped)
            {
                return true;
            }

            if (!LastChanceMonstersTargetProxyHelper.IsRuntimeMasterContextEnabled())
            {
                return true;
            }

            var deathHead = _other.GetComponentInParent<PlayerDeathHead>();
            if (deathHead == null)
            {
                return true;
            }

            var player = deathHead.playerAvatar;
            if (player == null || !LastChanceMonstersTargetProxyHelper.IsHeadProxyActive(player))
            {
                return true;
            }

            __instance.popped = true;
            __instance.popper = player;
            return false;
        }

        private static bool TryGetOverlappingHeadPopper(BirthdayBoyBalloon balloon, out PlayerAvatar? popper)
        {
            popper = null;
            var players = SemiFunc.PlayerGetList();
            if (players == null)
            {
                return false;
            }

            var bestDistance = float.MaxValue;
            foreach (var player in players)
            {
                if (player == null || !LastChanceMonstersTargetProxyHelper.IsHeadProxyActive(player))
                {
                    continue;
                }

                var head = player.playerDeathHead;
                if (head == null || !IsHeadOverlappingBalloon(balloon, head))
                {
                    continue;
                }

                var dist = (head.transform.position - balloon.transform.position).sqrMagnitude;
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    popper = player;
                }
            }

            return popper != null;
        }

        private static bool IsHeadOverlappingBalloon(BirthdayBoyBalloon balloon, PlayerDeathHead head)
        {
            if (!TryGetBalloonCollider(balloon, 1, out var balloonColA) &&
                !TryGetBalloonCollider(balloon, 2, out var _))
            {
                return false;
            }

            var headColliders = head.colliders;
            if (headColliders == null || headColliders.Length == 0)
            {
                headColliders = head.GetComponentsInChildren<Collider>();
            }

            for (var i = 0; i < headColliders.Length; i++)
            {
                var headCol = headColliders[i];
                if (!IsColliderValid(headCol))
                {
                    continue;
                }

                if (balloonColA != null && Physics.ComputePenetration(
                    balloonColA,
                    balloonColA.transform.position,
                    balloonColA.transform.rotation,
                    headCol,
                    headCol.transform.position,
                    headCol.transform.rotation,
                    out _,
                    out var distA) && distA > 0f)
                {
                    return true;
                }

                if (TryGetBalloonCollider(balloon, 2, out var balloonColB) && balloonColB != null && Physics.ComputePenetration(
                    balloonColB,
                    balloonColB.transform.position,
                    balloonColB.transform.rotation,
                    headCol,
                    headCol.transform.position,
                    headCol.transform.rotation,
                    out _,
                    out var distB) && distB > 0f)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetBalloonCollider(BirthdayBoyBalloon balloon, int index, out Collider? collider)
        {
            collider = index == 1 ? balloon.collider1 : balloon.collider2;
            return IsColliderValid(collider);
        }

        private static bool IsColliderValid(Collider? collider)
        {
            return collider != null && collider.enabled && collider.gameObject.activeInHierarchy;
        }
    }
}

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

            var nearby = __instance.PlayerNearby();
            if (nearby == null || !LastChanceMonstersTargetProxyHelper.IsHeadProxyActive(nearby))
            {
                return;
            }

            if (!LastChanceMonstersTargetProxyHelper.TryGetHeadProxyTarget(nearby, out var headCenter))
            {
                return;
            }

            if (!IsHeadOverlappingBalloon(__instance, headCenter))
            {
                return;
            }

            __instance.popped = true;
            __instance.popper = nearby;
        }

        [HarmonyPatch(typeof(BirthdayBoyBalloon), nameof(BirthdayBoyBalloon.OnTriggerEnter))]
        [HarmonyPostfix]
        private static void OnTriggerEnterPostfix(BirthdayBoyBalloon __instance, Collider _other)
        {
            if (__instance == null || _other == null || __instance.popped)
            {
                return;
            }

            if (!LastChanceMonstersTargetProxyHelper.IsRuntimeMasterContextEnabled())
            {
                return;
            }

            var deathHead = _other.GetComponentInParent<PlayerDeathHead>();
            if (deathHead == null)
            {
                return;
            }

            var player = deathHead.playerAvatar;
            if (player == null || !LastChanceMonstersTargetProxyHelper.IsHeadProxyActive(player))
            {
                return;
            }

            if (__instance.PlayerNearby() == null)
            {
                return;
            }

            __instance.popped = true;
            __instance.popper = player;
        }

        private static bool IsHeadOverlappingBalloon(BirthdayBoyBalloon balloon, Vector3 headCenter)
        {
            if (balloon.collider1 != null && IsInsideCollider(balloon.collider1, headCenter))
            {
                return true;
            }

            if (balloon.collider2 != null && IsInsideCollider(balloon.collider2, headCenter))
            {
                return true;
            }

            return false;
        }

        private static bool IsInsideCollider(Collider collider, Vector3 point)
        {
            var closest = collider.ClosestPoint(point);
            return (closest - point).sqrMagnitude <= 0.0004f;
        }
    }
}

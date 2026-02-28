#nullable enable

using System.Linq;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Adapters;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Interactions
{
    [HarmonyPatch]
    internal static class LastChanceMonstersValuableThrowerHeadAimModule
    {
        [HarmonyPatch(typeof(EnemyValuableThrower), nameof(EnemyValuableThrower.PlayerLookAt))]
        [HarmonyPostfix]
        private static void PlayerLookAtPostfix(EnemyValuableThrower __instance)
        {
            if (!TryGetContext(__instance, out var targetPoint))
            {
                return;
            }

            var enemy = __instance.enemy;
            if (enemy == null)
            {
                return;
            }

            var from = enemy.Rigidbody.transform.position;
            var dir = targetPoint - from;
            if (dir.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var yaw = Quaternion.Euler(0f, Quaternion.LookRotation(dir).eulerAngles.y, 0f);
            __instance.transform.rotation = Quaternion.Slerp(__instance.transform.rotation, yaw, 50f * Time.deltaTime);
        }

        [HarmonyPatch(typeof(EnemyValuableThrower), nameof(EnemyValuableThrower.Throw))]
        [HarmonyPrefix]
        private static bool ThrowPrefix(EnemyValuableThrower __instance)
        {
            if (!TryGetContext(__instance, out var targetPoint))
            {
                return true;
            }

            var valuableTarget = __instance.valuableTarget;
            if (valuableTarget == null)
            {
                return false;
            }

            foreach (var physGrabber in Enumerable.ToList(valuableTarget.playerGrabbing))
            {
                if (!SemiFunc.IsMultiplayer())
                {
                    physGrabber.ReleaseObject(valuableTarget.photonView.ViewID, 0.5f);
                    continue;
                }

                physGrabber.photonView.RPC("ReleaseObjectRPC", RpcTarget.All, false, 0.5f, valuableTarget.photonView.ViewID);
            }

            var forceDirection = targetPoint - valuableTarget.centerPoint;
            forceDirection = Vector3.Lerp(__instance.transform.forward, forceDirection, 0.5f);

            valuableTarget.ResetMass();
            var force = Mathf.Min(20f * valuableTarget.rb.mass, 100f);
            valuableTarget.ResetIndestructible();
            valuableTarget.rb.AddForce(forceDirection * force, ForceMode.Impulse);
            valuableTarget.rb.AddTorque(valuableTarget.transform.right * 0.5f, ForceMode.Impulse);
            valuableTarget.impactDetector.PlayerHurtMultiplier(5f, 2f);
            __instance.valuableTarget = null;

            return false;
        }

        private static bool TryGetContext(EnemyValuableThrower? thrower, out Vector3 targetPoint)
        {
            targetPoint = default;
            if (thrower == null || !LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
            {
                return false;
            }

            var player = thrower.playerTarget;
            if (player == null)
            {
                return false;
            }

            return LastChanceMonstersTargetProxyHelper.TryGetHeadProxyVisionTarget(player, out targetPoint) ||
                   LastChanceMonstersTargetProxyHelper.TryGetHeadProxyTarget(player, out targetPoint);
        }
    }
}

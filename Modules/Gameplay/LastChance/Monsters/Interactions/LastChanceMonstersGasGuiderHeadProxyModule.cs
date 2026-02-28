#nullable enable

using HarmonyLib;
using BepInEx.Logging;
using UnityEngine;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Adapters;
using DHHFLastChanceMode.Modules.Config;
using DHHFLastChanceMode.Modules.Utilities;
using Logger = BepInEx.Logging.Logger;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Interactions
{
    [HarmonyPatch(typeof(EnemyHeartHuggerGasGuider), nameof(EnemyHeartHuggerGasGuider.FixedUpdate))]
    internal static class LastChanceMonstersGasGuiderHeadProxyModule
    {
        private static readonly ManualLogSource Log = Logger.CreateLogSource("DHHFLastChanceMode.LastChance.HeartHugger");

        [HarmonyPrefix]
        private static bool Prefix(EnemyHeartHuggerGasGuider __instance)
        {
            if (__instance == null || !LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled() || !LastChanceMonstersTargetProxyHelper.IsMasterContext())
            {
                return true;
            }

            var player = __instance.player;
            if (player == null || !LastChanceMonstersTargetProxyHelper.IsHeadProxyActive(player))
            {
                return true;
            }

            var phys = __instance.physGrabObject;
            if (phys?.rb == null)
            {
                DebugLog("Guider.Fixed.SkipNoPhys", $"player={GetPlayerId(player)}");
                return true;
            }

            var enemyHeartHugger = __instance.enemyHeartHugger;
            if (enemyHeartHugger?.headCenterTransform == null)
            {
                DebugLog("Guider.Fixed.SkipNoEnemyHead", $"player={GetPlayerId(player)}");
                return true;
            }

            var rb = phys.rb;
            var start = __instance.startPosition;
            var dirToHead = (enemyHeartHugger.headCenterTransform.position - start).normalized;
            var from = rb.position;
            var to = __instance.transform.position;

            phys.OverrideZeroGravity(0.1f);
            if (rb.isKinematic)
            {
                rb.position = Vector3.Lerp(from, to, 0.3f);
                var targetRot = Quaternion.LookRotation(dirToHead.sqrMagnitude > 0.0001f ? dirToHead : rb.transform.forward, Vector3.up);
                rb.rotation = Quaternion.Slerp(rb.rotation, targetRot, 0.25f);
                DebugLog("Guider.Fixed.Kinematic", $"player={GetPlayerId(player)} rbPos={from} target={to}");
                return false;
            }

            var torque = SemiFunc.PhysFollowDirection(rb.transform, dirToHead, rb, 0.5f);
            rb.AddTorque(torque / Mathf.Max(rb.mass, 0.0001f), ForceMode.Force);
            var force = SemiFunc.PhysFollowPosition(from, to, rb.velocity, 5f);
            rb.AddForce(force, ForceMode.Acceleration);
            DebugLog("Guider.Fixed.Apply", $"player={GetPlayerId(player)} rbPos={from} target={to} forceMag={force.magnitude:0.00}");
            return false;
        }

        private static void DebugLog(string reason, string detail)
        {
            if (!InternalDebugFlags.DebugLastChanceHeartHuggerFlow || !LogLimiter.ShouldLog($"HeartHugger.{reason}", 30))
            {
                return;
            }

            Log.LogInfo($"[HeartHugger][{reason}] {detail}");
        }

        private static int GetPlayerId(PlayerAvatar player)
        {
            var view = player.photonView;
            return view != null ? view.ViewID : player.GetInstanceID();
        }
    }
}

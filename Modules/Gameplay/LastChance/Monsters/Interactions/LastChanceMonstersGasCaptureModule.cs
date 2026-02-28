#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Logging;
using DHHFLastChanceMode.Modules.Config;
using HarmonyLib;
using UnityEngine;
using DHHFLastChanceMode.Modules.Utilities;
using Logger = BepInEx.Logging.Logger;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Interactions
{
    [HarmonyPatch(typeof(EnemyHeartHuggerGasChecker), nameof(EnemyHeartHuggerGasChecker.Update))]
    internal static class LastChanceMonstersGasCaptureModule
    {
        private static readonly ManualLogSource Log = Logger.CreateLogSource("DHHFLastChanceMode.LastChance.HeartHugger");
        [HarmonyPostfix]
        private static void Postfix(EnemyHeartHuggerGasChecker __instance)
        {
            if (__instance == null || !LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled() || !LastChanceMonstersTargetProxyHelper.IsMasterContext())
            {
                return;
            }

            if (__instance.checkTimer < 0.95f)
            {
                return;
            }

            var owner = __instance.enemyHeartHugger;
            if (owner == null)
            {
                return;
            }

            var playersColliding = __instance.playersColliding as IList;
            if (playersColliding == null)
            {
                return;
            }

            var ownerVision = owner.enemy?.Vision;
            var prev = __instance.prevCheckPos;
            var current = __instance.transform.position;
            var travel = current - prev;

            var direction = travel.normalized;
            var distance = travel.magnitude;
            var radius = Mathf.Max(__instance.transform.localScale.z * 0.5f, 0.2f);

            if (distance <= 0.01f)
            {
                DebugLog("Gas.NoTravel", $"overlap fallback radius={radius:0.00}");
                var overlap = Physics.OverlapSphere(current, radius, ~0, QueryTriggerInteraction.Ignore);
                var processed = 0;
                for (var i = 0; i < overlap.Length; i++)
                {
                    var col = overlap[i];
                    if (col == null)
                    {
                        continue;
                    }

                    if (ProcessCandidateCollider(__instance, owner, playersColliding, ownerVision, col, current, radius))
                    {
                        processed++;
                    }
                }
                DebugLog("Gas.NoTravel.Result", $"overlap={overlap.Length} processed={processed}");

                return;
            }

            var hits = Physics.SphereCastAll(prev, radius, direction, distance, LayerMask.GetMask("Player", "PhysGrabObject"), QueryTriggerInteraction.Ignore);
            for (var i = 0; i < hits.Length; i++)
            {
                var collider = hits[i].collider;
                if (collider == null)
                {
                    continue;
                }
                _ = ProcessCandidateCollider(__instance, owner, playersColliding, ownerVision, collider, hits[i].point, radius);
            }
        }

        private static bool ProcessCandidateCollider(
            EnemyHeartHuggerGasChecker checkerInstance,
            EnemyHeartHugger owner,
            IList playersColliding,
            EnemyVision? ownerVision,
            Collider collider,
            Vector3 hitPoint,
            float radius)
        {
            if (!TryResolvePlayer(collider, out var player) || player == null)
            {
                return false;
            }

            if (!LastChanceMonstersTargetProxyHelper.IsHeadProxyActive(player))
            {
                DebugLog("Gas.Skip.NotHeadProxy", $"player={GetPlayerId(player)}");
                return false;
            }

            if (owner.PlayerIsOnCooldown(player))
            {
                DebugLog("Gas.Skip.Cooldown", $"player={GetPlayerId(player)}");
                return false;
            }

            if (owner.PlayerInGasCheck(player))
            {
                DebugLog("Gas.Skip.AlreadyInGas", $"player={GetPlayerId(player)}");
                return false;
            }

            var visionOrigin = ownerVision?.VisionTransform;
            if (visionOrigin != null && player.PlayerVisionTarget?.VisionTransform != null)
            {
                var target = player.PlayerVisionTarget.VisionTransform.position;
                var dir = target - visionOrigin.position;
                if (Physics.Raycast(visionOrigin.position, dir, dir.magnitude, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore))
                {
                    DebugLog("Gas.Skip.BlockedLOS", $"player={GetPlayerId(player)}");
                    return false;
                }
            }

            if (!ContainsPlayer(playersColliding, player))
            {
                playersColliding.Add(player);
            }

            owner.PlayerInGas(player);
            DebugLog("Gas.PlayerInGas", $"player={GetPlayerId(player)} radius={radius:0.00}");
            TrySpawnGasGuiderForHeadProxy(checkerInstance, owner, player, hitPoint);
            return true;
        }

        private static bool TryResolvePlayer(Collider collider, out PlayerAvatar? player)
        {
            player = null;
            if (collider == null)
            {
                return false;
            }

            var controller = collider.GetComponentInParent<PlayerController>();
            if (controller != null && controller.playerAvatarScript != null)
            {
                player = controller.playerAvatarScript;
                return true;
            }

            var avatar = collider.GetComponentInParent<PlayerAvatar>();
            if (avatar != null)
            {
                player = avatar;
                return true;
            }

            var tumble = collider.GetComponentInParent<PlayerTumble>();
            if (tumble != null && tumble.playerAvatar != null)
            {
                player = tumble.playerAvatar;
                return true;
            }

            var deathHead = collider.GetComponentInParent<PlayerDeathHead>();
            if (deathHead != null && deathHead.playerAvatar != null)
            {
                player = deathHead.playerAvatar;
                return true;
            }

            return false;
        }

        private static bool ContainsPlayer(IList list, PlayerAvatar player)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], player))
                {
                    return true;
                }
            }

            return false;
        }

        private static void TrySpawnGasGuiderForHeadProxy(EnemyHeartHuggerGasChecker checkerInstance, EnemyHeartHugger owner, PlayerAvatar player, Vector3 hitPoint)
        {
            if (!LastChanceMonstersTargetProxyHelper.TryGetHeadProxyTransform(player, out var headTransform) || headTransform == null)
            {
                DebugLog("Guider.SpawnSkip.NoHeadTransform", $"player={GetPlayerId(player)}");
                return;
            }

            if (!LastChanceMonstersTargetProxyHelper.TryGetHeadProxyPhysGrabObject(player, out var headPhys) || headPhys == null)
            {
                DebugLog("Guider.SpawnSkip.NoHeadPhys", $"player={GetPlayerId(player)}");
                return;
            }

            var gasGuiderPrefab = checkerInstance.gasGuider;
            if (gasGuiderPrefab == null)
            {
                DebugLog("Guider.SpawnSkip.NoPrefab", $"player={GetPlayerId(player)}");
                return;
            }

            var instance = UnityEngine.Object.Instantiate(gasGuiderPrefab, checkerInstance.transform.position, Quaternion.identity);
            if (instance == null)
            {
                DebugLog("Guider.SpawnSkip.InstantiateNull", $"player={GetPlayerId(player)}");
                return;
            }

            var guider = instance.GetComponent<EnemyHeartHuggerGasGuider>();
            if (guider == null)
            {
                DebugLog("Guider.SpawnSkip.NoComponent", $"player={GetPlayerId(player)} prefab={gasGuiderPrefab.name}");
                return;
            }

            guider.playerTumble = player.tumble;
            guider.targetTransform = headTransform;
            guider.enemyHeartHugger = owner;
            guider.headTransform = headTransform;
            guider.startPosition = hitPoint;
            guider.physGrabObject = headPhys;
            guider.player = player;
            instance.SetActive(true);
            DebugLog("Guider.Spawned", $"player={GetPlayerId(player)} start={hitPoint} head={headTransform.position}");
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


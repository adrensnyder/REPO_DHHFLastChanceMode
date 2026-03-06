#nullable enable

using System;
using BepInEx.Logging;
using DHHFLastChanceMode.Modules.Config;
using HarmonyLib;
using UnityEngine;
using DHHFLastChanceMode.Modules.Utilities;
using Logger = BepInEx.Logging.Logger;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Interactions
{
    [HarmonyPatch(typeof(EnemyTricycle), nameof(EnemyTricycle.IsPlayerBlockingNavmeshPath))]
    internal static class LastChanceMonstersPathBlockingModule
    {
        private static readonly ManualLogSource Log = Logger.CreateLogSource("DHHFLastChanceMode.LastChance.Tricycle");
        private const float DefaultBlockingRadius = 0.5f;
        private const float DefaultBlockingDistance = 3f;
        private const float DefaultNavmeshRadius = 1f;
        private static float s_lastTargetSnapshotAt;

        [HarmonyPostfix]
        private static void Postfix(EnemyTricycle __instance, ref bool __result)
        {
            var runtimeMaster = LastChanceMonstersTargetProxyHelper.IsRuntimeMasterContextEnabled();
            if (__result || __instance == null || !runtimeMaster)
            {
                DebugLog("Skip.Early", $"result={__result} instanceNull={__instance == null} runtimeMaster={runtimeMaster}");
                return;
            }

            var enemy = __instance.enemy;
            if (enemy == null || enemy.CenterTransform == null)
            {
                DebugLog("Skip.NoEnemy", "enemy or center transform missing");
                return;
            }

            var navMeshAgent = enemy.NavMeshAgent;
            if (navMeshAgent == null)
            {
                DebugLog("Skip.NoNavMeshAgent", $"enemy={enemy.name}");
                return;
            }

            var heading = __instance.agentDirection;
            if (heading.sqrMagnitude <= 0.0001f)
            {
                DebugLog("Skip.NoHeading", $"enemy={enemy.name} heading={heading}");
                return;
            }

            var blockedByPlayer = __instance.isBlockedByPlayer;
            var blockedAvatar = __instance.isBlockedByPlayerAvatar;
            var playerTarget = __instance.playerTarget;
            var currentState = __instance.currentState.ToString();
            var targetDistBody = playerTarget != null ? Vector3.Distance(enemy.CenterTransform.position, playerTarget.transform.position) : -1f;
            var targetDistHead = playerTarget != null && LastChanceMonstersTargetProxyHelper.TryGetHeadProxyTarget(playerTarget, out var targetHead)
                ? Vector3.Distance(enemy.CenterTransform.position, targetHead)
                : -1f;
            var targetHorzBody = -1f;
            var targetHorzHead = -1f;
            var deltaYBody = 0f;
            var deltaYHead = 0f;
            var headingDotBody = 0f;
            var headingDotHead = 0f;
            var losBody = false;
            var losHead = false;
            var hasBody = false;
            var hasHead = false;
            if (playerTarget != null)
            {
                var center = enemy.CenterTransform.position;
                var bodyPos = playerTarget.transform.position;
                var bodyDelta = bodyPos - center;
                var bodyFlat = new Vector3(bodyDelta.x, 0f, bodyDelta.z);
                targetHorzBody = bodyFlat.magnitude;
                deltaYBody = bodyDelta.y;
                hasBody = bodyDelta.sqrMagnitude > 0.0001f;
                headingDotBody = hasBody ? Vector3.Dot(heading.normalized, bodyDelta.normalized) : 0f;
                losBody = HasLineOfSight(center, bodyPos, enemy.transform);

                if (LastChanceMonstersTargetProxyHelper.TryGetHeadProxyTarget(playerTarget, out var headPos))
                {
                    var headDelta = headPos - center;
                    var headFlat = new Vector3(headDelta.x, 0f, headDelta.z);
                    targetHorzHead = headFlat.magnitude;
                    deltaYHead = headDelta.y;
                    hasHead = headDelta.sqrMagnitude > 0.0001f;
                    headingDotHead = hasHead ? Vector3.Dot(heading.normalized, headDelta.normalized) : 0f;
                    losHead = HasLineOfSight(center, headPos, enemy.transform);
                }
            }
            DebugLog(
                "State",
                $"enemy={enemy.name} state={currentState} currentBlocked={blockedByPlayer} blockedAvatar={GetPlayerIdOrNone(blockedAvatar)} " +
                $"playerTarget={GetPlayerIdOrNone(playerTarget)} targetBodyDist={(targetDistBody >= 0f ? targetDistBody.ToString("F2") : "n/a")} " +
                $"targetHeadDist={(targetDistHead >= 0f ? targetDistHead.ToString("F2") : "n/a")} " +
                $"targetBodyH={(targetHorzBody >= 0f ? targetHorzBody.ToString("F2") : "n/a")} targetHeadH={(targetHorzHead >= 0f ? targetHorzHead.ToString("F2") : "n/a")} " +
                $"deltaYBody={(hasBody ? deltaYBody.ToString("F2") : "n/a")} deltaYHead={(hasHead ? deltaYHead.ToString("F2") : "n/a")} " +
                $"dotBody={(hasBody ? headingDotBody.ToString("F2") : "n/a")} dotHead={(hasHead ? headingDotHead.ToString("F2") : "n/a")} " +
                $"losBody={losBody} losHead={losHead} probeDist={DefaultBlockingDistance:F2} headingMag={heading.magnitude:F2}");
            TryLogTargetSnapshot(enemy);

            var hits = Physics.SphereCastAll(
                enemy.CenterTransform.position,
                DefaultBlockingRadius,
                heading.normalized,
                DefaultBlockingDistance,
                LayerMask.GetMask("Player", "PhysGrabObject"));
            DebugLog("Probe", $"enemy={enemy.name} hits={hits.Length} center={enemy.CenterTransform.position} heading={heading.normalized}");

            for (var i = 0; i < hits.Length; i++)
            {
                var collider = hits[i].collider;
                if (collider == null)
                {
                    continue;
                }

                DebugLog(
                    "Hit",
                    $"idx={i} collider={collider.name} dist={hits[i].distance:F2} point={hits[i].point} layer={LayerMask.LayerToName(collider.gameObject.layer)}");

                if (!TryResolvePlayerFromBlockingCollider(collider, out var player) || player == null)
                {
                    LastChanceMonstersTargetProxyHelper.TryGetPlayerFromDeathHeadCollider(collider, out var deathHeadPlayer);
                    var self = collider.transform.IsChildOf(enemy.transform);
                    var root = collider.transform.root != null ? collider.transform.root.name : "n/a";
                    DebugLog(
                        "Hit.Skip.NoPlayer",
                        $"collider={collider.name} layer={LayerMask.LayerToName(collider.gameObject.layer)} root={root} self={self} deathHeadPlayer={GetPlayerIdOrNone(deathHeadPlayer)}");
                    continue;
                }

                if (!TryResolveNavmeshPoint(player, false, out var candidatePoint, out var fallbackPoint))
                {
                    DebugLog("Hit.Skip.NoPoint", $"player={GetPlayerId(player)}");
                    continue;
                }

                var onNavmesh = navMeshAgent.OnNavmesh(candidatePoint, DefaultNavmeshRadius, true);
                if (!onNavmesh && fallbackPoint.HasValue)
                {
                    onNavmesh = navMeshAgent.OnNavmesh(fallbackPoint.Value, DefaultNavmeshRadius, true);
                    if (onNavmesh)
                    {
                        candidatePoint = fallbackPoint.Value;
                    }
                }

                if (!onNavmesh)
                {
                    var fromCenter = Vector3.Distance(enemy.CenterTransform.position, candidatePoint);
                    DebugLog("Hit.Skip.NotOnNavmesh", $"player={GetPlayerId(player)} point={candidatePoint} distFromEnemy={fromCenter:F2}");
                    continue;
                }

                // Conservative behavior gate: accept blocking only if the candidate point
                // is really inside the forward probe corridor, to avoid false positives
                // that can perturb vanilla movement/state transitions.
                if (!IsPointInsideProbeCorridor(enemy.CenterTransform.position, heading.normalized, candidatePoint))
                {
                    DebugLog("Hit.Skip.OutsideProbe", $"player={GetPlayerId(player)} point={candidatePoint}");
                    continue;
                }

                __instance.isBlockedByPlayerAvatar = player;
                __instance.isBlockedByPlayer = true;
                __result = true;
                DebugLog("Blocked.Set", $"player={GetPlayerId(player)} point={candidatePoint}");
                return;
            }

            DebugLog("Probe.NoBlock", $"enemy={enemy.name}");
        }

        private static bool TryResolvePlayerFromBlockingCollider(Collider collider, out PlayerAvatar? player)
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

        private static bool TryResolveNavmeshPoint(PlayerAvatar player, bool preferBodyAnchor, out Vector3 point, out Vector3? fallbackPoint)
        {
            fallbackPoint = null;
            if (preferBodyAnchor)
            {
                point = player.transform.position;
                return true;
            }

            if (LastChanceMonstersTargetProxyHelper.TryGetHeadProxyTarget(player, out var headCenter))
            {
                point = headCenter;
                fallbackPoint = player.transform.position;
                return true;
            }

            point = player.transform.position;
            return true;
        }

        private static bool IsPointInsideProbeCorridor(Vector3 origin, Vector3 headingNormalized, Vector3 point)
        {
            var toPoint = point - origin;
            var along = Vector3.Dot(toPoint, headingNormalized);
            if (along < 0f || along > DefaultBlockingDistance + 0.1f)
            {
                return false;
            }

            var projected = origin + headingNormalized * along;
            var radial = Vector3.Distance(projected, point);
            return radial <= DefaultBlockingRadius + 0.35f;
        }

        private static int GetPlayerId(PlayerAvatar player)
        {
            return player.photonView != null ? player.photonView.ViewID : player.GetInstanceID();
        }

        private static string GetPlayerIdOrNone(PlayerAvatar? player)
        {
            return player == null ? "n/a" : GetPlayerId(player).ToString();
        }

        private static bool HasLineOfSight(Vector3 from, Vector3 to, Transform? enemyRoot)
        {
            var delta = to - from;
            var dist = delta.magnitude;
            if (dist <= 0.001f)
            {
                return true;
            }

            var hits = Physics.RaycastAll(from, delta / dist, dist, ~0, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < hits.Length; i++)
            {
                var c = hits[i].collider;
                if (c == null)
                {
                    continue;
                }

                var t = c.transform;
                if (enemyRoot != null && t.IsChildOf(enemyRoot))
                {
                    continue;
                }

                if (TryResolvePlayerFromBlockingCollider(c, out var p) && p != null)
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private static void TryLogTargetSnapshot(Enemy enemy)
        {
            if (!InternalDebugFlags.DebugLastChanceTricycleFlow)
            {
                return;
            }

            var now = Time.unscaledTime;
            if (now - s_lastTargetSnapshotAt < 1.5f)
            {
                return;
            }

            s_lastTargetSnapshotAt = now;
            var players = GameDirector.instance?.PlayerList;
            if (players == null || players.Count == 0)
            {
                DebugLog("Targets", "no players in GameDirector list");
                return;
            }

            var center = enemy.CenterTransform.position;
            for (var i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null)
                {
                    continue;
                }

                var bodyDist = Vector3.Distance(center, p.transform.position);
                var headProxy = LastChanceMonstersTargetProxyHelper.TryGetHeadProxyTarget(p, out var headCenter);
                var headDist = headProxy ? Vector3.Distance(center, headCenter) : -1f;
                var disabled = LastChanceMonstersTargetProxyHelper.IsDisabled(p);
                var active = LastChanceMonstersTargetProxyHelper.IsHeadProxyActive(p);
                DebugLog(
                    "Targets",
                    $"player={GetPlayerId(p)} disabled={disabled} headProxyActive={active} headProxy={headProxy} bodyDist={bodyDist:F2} headDist={(headDist >= 0f ? headDist.ToString("F2") : "n/a")} bodyPos={p.transform.position} headPos={(headProxy ? headCenter.ToString() : "n/a")}");
            }
        }

        private static void DebugLog(string reason, string detail)
        {
            if (!InternalDebugFlags.DebugLastChanceTricycleFlow || !LogLimiter.ShouldLog($"Tricycle.{reason}", 30))
            {
                return;
            }

            Log.LogInfo($"[Tricycle][{reason}] {detail}");
        }
    }
}


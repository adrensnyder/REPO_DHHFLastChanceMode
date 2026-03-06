#nullable enable

using DHHFLastChanceMode.Modules.Config;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Support;
using DHHFLastChanceMode.Modules.Utilities;
using HarmonyLib;
using UnityEngine;
using BepInEx.Logging;
using Logger = BepInEx.Logging.Logger;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Pipeline
{
    internal static class LastChanceMonstersPlayerVisionCheckModule
    {
        private static readonly ManualLogSource Log = Logger.CreateLogSource("DHHFLastChanceMode.LastChance.CeilingEye");

        internal static void ResetRuntimeState()
        {
            LastChanceMonstersCeilingEyeLockCoordinator.ResetRuntimeState();
        }

        internal static void Apply()
        {
            // Patches are typed and registered in LastChanceHarmonyPatchRegistry.
        }

        internal static void Unapply()
        {
            // Patches remain installed; behavior is runtime-gated.
            ResetRuntimeState();
        }

        [HarmonyPatch(typeof(SemiFunc), nameof(SemiFunc.PlayerVisionCheck), new[] { typeof(Vector3), typeof(float), typeof(PlayerAvatar), typeof(bool) })]
        internal static class SemiFuncPlayerVisionCheckPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(Vector3 _position, float _range, PlayerAvatar _player, bool _previouslySeen, ref bool __result)
            {
                if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
                {
                    return true;
                }

                if (!LastChanceMonstersTargetProxyHelper.TryGetHeadProxyVisionTarget(_player, out var headCenter))
                {
                    return true;
                }

                __result = PlayerVisionCheckPositionLastChanceAware(_position, headCenter, _range, _player, _previouslySeen);
                return false;
            }
        }

        [HarmonyPatch(typeof(SemiFunc), nameof(SemiFunc.PlayerVisionCheckPosition), new[] { typeof(Vector3), typeof(Vector3), typeof(float), typeof(PlayerAvatar), typeof(bool) })]
        internal static class SemiFuncPlayerVisionCheckPositionPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(Vector3 _startPosition, Vector3 _endPosition, float _range, PlayerAvatar _player, bool _previouslySeen, ref bool __result)
            {
                if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
                {
                    return true;
                }

                if (!LastChanceMonstersTargetProxyHelper.TryGetHeadProxyVisionTarget(_player, out var headCenter))
                {
                    return true;
                }

                __result = PlayerVisionCheckPositionLastChanceAware(_startPosition, headCenter, _range, _player, _previouslySeen);
                return false;
            }
        }

        internal static bool PlayerVisionCheckPositionLastChanceAware(Vector3 startPosition, Vector3 endPosition, float range, PlayerAvatar player, bool previouslySeen)
        {
            if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
            {
                return SemiFunc.PlayerVisionCheckPosition(startPosition, endPosition, range, player, previouslySeen);
            }

            if (!LastChanceMonstersTargetProxyHelper.TryGetHeadProxyVisionTarget(player, out var headCenter))
            {
                return SemiFunc.PlayerVisionCheckPosition(startPosition, endPosition, range, player, previouslySeen);
            }

            endPosition = headCenter;
            var now = Time.unscaledTime;
            var seen = HeadProxyVisionCheckPosition(startPosition, endPosition, range, player);
            var allow = LastChanceMonstersCeilingEyeLockCoordinator.EvaluateVisionLock(player, seen, now, out var reason);
            DebugVision(reason, startPosition, endPosition, player, now, allow);
            return allow;
        }

        private static bool HeadProxyVisionCheckPosition(Vector3 startPosition, Vector3 endPosition, float range, PlayerAvatar player)
        {
            var candidatePoints = new[]
            {
                endPosition,
                endPosition + Vector3.up * 0.2f,
                endPosition + Vector3.up * 0.45f
            };

            for (var i = 0; i < candidatePoints.Length; i++)
            {
                if (HeadProxyVisionCheckPositionSingle(startPosition, candidatePoints[i], range, player))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HeadProxyVisionCheckPositionSingle(Vector3 startPosition, Vector3 endPosition, float range, PlayerAvatar player)
        {
            var direction = endPosition - startPosition;
            var distance = direction.magnitude;
            if (distance > range)
            {
                return false;
            }

            if (distance <= 0.001f)
            {
                return true;
            }

            var hits = Physics.RaycastAll(startPosition, direction.normalized, distance, ~0, QueryTriggerInteraction.Ignore);
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

                var hitHead = t.GetComponentInParent<PlayerDeathHead>();
                if (hitHead != null && player != null && hitHead == player.playerDeathHead)
                {
                    continue;
                }

                var hitAvatar = t.GetComponentInParent<PlayerAvatar>();
                if (hitAvatar != null && hitAvatar == player)
                {
                    continue;
                }

                if (t.GetComponentInParent<PlayerTumble>() != null)
                {
                    continue;
                }

                var hitToTarget = Vector3.Distance(hits[i].point, endPosition);
                if (hitToTarget <= 0.35f)
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private static void DebugVision(string reason, Vector3 startPosition, Vector3 endPosition, PlayerAvatar player, float now, bool decision)
        {
            if (!InternalDebugFlags.DebugLastChanceCeilingEyeFlow)
            {
                return;
            }

            var playerId = player != null && player.photonView != null ? player.photonView.ViewID : (player?.GetInstanceID() ?? 0);
            if (!LogLimiter.ShouldLog($"CeilingEye.Vision.{reason}.{playerId}", 90))
            {
                return;
            }

            Log.LogInfo(
                $"[CeilingEye][Vision][{reason}] playerId={playerId} decision={decision} " +
                $"start={startPosition} end={endPosition} now={now:F2}");
        }
    }
}

#nullable enable

using System;
using BepInEx.Logging;
using DHHFLastChanceMode.Modules.Config;
using DHHFLastChanceMode.Modules.Utilities;
using HarmonyLib;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Pipeline
{
    [HarmonyPatch]
    internal static class LastChanceMonstersCameraForceLockModule
    {
        private static readonly ManualLogSource Log = Logger.CreateLogSource("DHHFLastChanceMode.LastChance.CeilingEye");

        internal static void Apply()
        {
            // Patches are registered through explicit bootstrap registry; keep lifecycle callsite compatible.
            ResetRuntimeState();
        }

        internal static void Unapply()
        {
            // Patches remain loaded but become NOOP outside runtime gates.
            ResetRuntimeState();
        }

        [HarmonyPatch(typeof(CameraAim), nameof(CameraAim.AimTargetSoftSet), new[] { typeof(Vector3), typeof(float), typeof(float), typeof(float), typeof(GameObject), typeof(int) })]
        [HarmonyPrefix]
        private static bool AimTargetSoftSetPrefix(Vector3 position, GameObject obj)
        {
            return HandleCameraAimRequest(position, obj);
        }

        [HarmonyPatch(typeof(CameraAim), nameof(CameraAim.AimTargetSet), new[] { typeof(Vector3), typeof(float), typeof(float), typeof(GameObject), typeof(int) })]
        [HarmonyPrefix]
        private static bool AimTargetSetPrefix(Vector3 position, GameObject obj)
        {
            return HandleCameraAimRequest(position, obj);
        }

        private static bool HandleCameraAimRequest(Vector3 position, GameObject? source)
        {
            if (!IsSupportedCameraSource(source))
            {
                return true;
            }

            if (!IsLastChanceCameraContextActive())
            {
                return true;
            }

            if (!ShouldApplyCameraForce(source))
            {
                return false;
            }

            TryForceSpectateAimTo(position, source);
            return true;
        }

        private static bool IsSupportedCameraSource(GameObject? source)
        {
            if (source == null)
            {
                return false;
            }

            return source.GetComponentInParent<EnemyHeartHugger>() != null ||
                   source.GetComponentInParent<EnemyThinManAnim>() != null ||
                   source.GetComponentInParent<EnemySlowMouthAttaching>() != null ||
                   source.GetComponentInParent<EnemyOogly>() != null ||
                   source.GetComponentInParent<EnemyCeilingEye>() != null ||
                   source.GetComponentInParent<EnemySpinny>() != null ||
                   source.GetComponentInParent<EnemyUpscream>() != null;
        }

        private static bool IsLastChanceCameraContextActive()
        {
            if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
            {
                return false;
            }

            return LastChanceMonstersTargetProxyHelper.IsHeadProxyActive(PlayerAvatar.instance);
        }

        private static bool ShouldApplyCameraForce(GameObject? source)
        {
            if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
            {
                return false;
            }

            var local = PlayerAvatar.instance;
            if (!LastChanceMonstersTargetProxyHelper.IsHeadProxyActive(local))
            {
                return false;
            }

            var now = Time.unscaledTime;
            var key = source != null ? source.GetInstanceID() : 0;
            if (!LastChanceMonstersCeilingEyeLockCoordinator.CanForceCamera(local, now, out var reason))
            {
                DebugDecision(source, key, reason, now, false);
                return false;
            }

            // Gameplay stays active regardless; this only controls camera forcing.
            var allow = InternalConfig.LastChanceMonstersForceCameraOnLock;
            DebugDecision(source, key, allow ? "AllowForceCamera" : "ForceCameraDisabledByConfig", now, allow);
            return allow;
        }

        private static void DebugDecision(GameObject? source, int key, string reason, float now, bool decision)
        {
            if (!InternalDebugFlags.DebugLastChanceCeilingEyeFlow || !LogLimiter.ShouldLog($"CeilingEye.CameraForce.{reason}.{key}", 90))
            {
                return;
            }

            var sourceName = source != null ? source.name : "null-source";
            Log.LogInfo(
                $"[CeilingEye][CameraForce][{reason}] source='{sourceName}' key={key} decision={decision} " +
                $"now={now:F2} cfgForce={InternalConfig.LastChanceMonstersForceCameraOnLock}");
        }

        private static void TryForceSpectateAimTo(Vector3 targetPosition, GameObject? source)
        {
            var spectate = SpectateCamera.instance;
            if (spectate == null)
            {
                return;
            }

            var local = PlayerAvatar.instance;
            var spectated = spectate.player;
            if (local == null || spectated == null || !ReferenceEquals(local, spectated))
            {
                return;
            }

            var pivot = spectate.transform;
            var direction = targetPosition - pivot.position;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            var flat = new Vector2(direction.x, direction.z).magnitude;
            var pitch = -Mathf.Atan2(direction.y, Mathf.Max(0.0001f, flat)) * Mathf.Rad2Deg;

            spectate.normalAimHorizontal = yaw;
            spectate.normalAimVertical = Mathf.Clamp(pitch, -80f, 80f);

            if (InternalDebugFlags.DebugLastChanceCeilingEyeFlow && LogLimiter.ShouldLog("CeilingEye.SpectateBridge", 90))
            {
                Log.LogInfo($"[CeilingEye][SpectateBridge] source='{(source != null ? source.name : "null-source")}' yaw={yaw:F1} pitch={pitch:F1} target={targetPosition}");
            }
        }

        internal static void ResetRuntimeState()
        {
            LastChanceMonstersCeilingEyeLockCoordinator.ResetRuntimeState();
        }
    }
}

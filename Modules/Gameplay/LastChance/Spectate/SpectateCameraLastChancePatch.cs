#nullable enable

using System;
using System.Collections.Generic;
using BepInEx.Logging;
using DeathHeadHopper.DeathHead;
using DeathHeadHopper.Helpers;
using DHHFLastChanceMode.Modules.Config;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Guards;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Runtime;
using DHHFLastChanceMode.Modules.Utilities;
using HarmonyLib;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Spectate
{
    internal sealed class LastChanceSpectatePointInvocationState
    {
        internal PlayerAvatar? Player;
        internal Transform? OriginalSpectatePoint;
    }

    internal static class LastChanceSpectateHelper
    {
        internal static readonly ManualLogSource Log = Logger.CreateLogSource("DHHFLastChanceMode.LastChance.Spectate");
        private const string ForceSpectateLogKey = "LastChance.ForceDeathHeadSpectate";
        private const string DebugStateLogKey = "LastChance.SpectateState";
        private const string OrbitProxyName = "DHHFLastChanceMode.Spectate.OrbitProxy";
        private static bool s_forceComplete;
        private static bool s_legacyAlwaysWarningEmitted;
        private static DeathHeadController? s_cachedController;
        private static Transform? s_orbitProxy;
        private static FovBaseline? s_fovBaseline;
        private static string? s_lastSpectateDebugMessage;

        internal static bool IsActiveRuntimeGate()
        {
            return FeatureFlags.LastChangeMode &&
                   LastChanceRuntimeOrchestrator.IsRuntimeActive &&
                   LastChanceTimerController.IsActive &&
                   AllPlayersDeadGuard.AllPlayersDisabled();
        }

        internal static bool ShouldCycleDisabledPlayers()
        {
            if (!FeatureFlags.SpectateDeadPlayers)
            {
                return false;
            }

            var mode = (FeatureFlags.SpectateDeadPlayersMode ?? string.Empty).Trim();
            if (mode.Equals("Disabled", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (mode.Equals("Always", StringComparison.OrdinalIgnoreCase))
            {
                EmitLegacyAlwaysWarningOnce();
                return true;
            }

            return mode.Equals("LastChanceOnly", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool HandlePlayerSwitch(SpectateCamera spectate, bool next)
        {
            if (spectate == null)
            {
                return false;
            }

            if (!ShouldCycleDisabledPlayers())
            {
                EnsureSpectatePlayerLocal(spectate);
                ForceDeathHeadSpectateIfPossible();
                return true;
            }

            ResetForceState();
            var players = GameDirector.instance?.PlayerList;
            if (players == null || players.Count == 0)
            {
                return true;
            }

            TryPlayerSwitch(spectate, players, next);
            return true;
        }

        internal static LastChanceSpectatePointInvocationState? TryApplyOrbitProxy(SpectateCamera spectate)
        {
            if (spectate == null || !ShouldCycleDisabledPlayers())
            {
                return null;
            }

            var player = spectate.player;
            if (player == null || ReferenceEquals(player, PlayerAvatar.instance) || !player.isDisabled)
            {
                return null;
            }

            var original = player.spectatePoint;
            if (original == null || !TryGetDeathHeadAnchor(player, out var anchor))
            {
                return null;
            }

            var proxy = EnsureOrbitProxy();
            if (proxy == null)
            {
                return null;
            }

            var offset = player.transform != null
                ? original.position - player.transform.position
                : Vector3.zero;
            proxy.position = anchor + offset;
            proxy.rotation = original.rotation;
            player.spectatePoint = proxy;

            return new LastChanceSpectatePointInvocationState
            {
                Player = player,
                OriginalSpectatePoint = original
            };
        }

        internal static void RestoreOrbitProxyInvocation(LastChanceSpectatePointInvocationState? state)
        {
            if (state?.Player == null || state.OriginalSpectatePoint == null)
            {
                return;
            }

            state.Player.spectatePoint = state.OriginalSpectatePoint;
        }

        internal static void HandleStateNormalPostfix(SpectateCamera spectate)
        {
            if (spectate == null)
            {
                return;
            }

            MaintainFovState(spectate);

            if (ShouldCycleDisabledPlayers())
            {
                ResetForceState();
            }
            else
            {
                EnsureSpectatePlayerLocal(spectate);
                ForceDeathHeadSpectateIfPossible();
            }

            DebugLogState(spectate);
        }

        internal static void MaintainFovState(SpectateCamera? spectate)
        {
            if (spectate == null)
            {
                return;
            }

            var targetFov = FeatureFlags.LastChanceSpectateDefaultFov;
            if (targetFov <= 0f)
            {
                RestoreFovBaseline();
                return;
            }

            EnsureFovBaseline(spectate);
            ApplyFovState(spectate, targetFov);
        }

        internal static void ResetOwnedState()
        {
            RestoreFovBaseline();
            ResetForceState();
            s_cachedController = null;
            s_lastSpectateDebugMessage = null;

            if (s_orbitProxy != null)
            {
                var proxyObject = s_orbitProxy.gameObject;
                s_orbitProxy = null;
                if (proxyObject != null)
                {
                    UnityEngine.Object.Destroy(proxyObject);
                }
            }
        }

        internal static void ForceDeathHeadSpectateIfPossible()
        {
            if (s_forceComplete || !DHHFunc.LocalDeathHeadActive())
            {
                return;
            }

            var localAvatar = PlayerAvatar.instance;
            if (localAvatar != null && !DHHFunc.IsDeathHeadSpectatable(localAvatar))
            {
                return;
            }

            var controller = TryGetLocalDeathHeadController();
            if (controller == null)
            {
                return;
            }

            s_cachedController = controller;
            if (controller.spectated)
            {
                s_forceComplete = true;
                return;
            }

            var spectate = SpectateCamera.instance;
            if (spectate != null && localAvatar != null)
            {
                spectate.player = localAvatar;
            }

            controller.SetSpectated(true);
            controller.UpdateSpectated();
            s_forceComplete = controller.spectated;

            if (s_forceComplete && FeatureFlags.DebugLogging && LogLimiter.ShouldLog(ForceSpectateLogKey, 30))
            {
                Log.LogDebug("[LastChance] Forced local DeathHead spectate while dead-player cycling is disabled.");
            }
        }

        internal static void ResetForceState()
        {
            s_forceComplete = false;
        }

        private static void EnsureSpectatePlayerLocal(SpectateCamera spectate)
        {
            var local = PlayerAvatar.instance;
            if (local != null)
            {
                spectate.player = local;
            }
        }

        private static bool TryPlayerSwitch(SpectateCamera spectate, IList<PlayerAvatar> players, bool next)
        {
            if (players.Count == 0 || spectate.normalTransformPivot == null || spectate.normalTransformDistance == null)
            {
                return false;
            }

            var currentPlayer = spectate.player;
            var index = spectate.currentPlayerListIndex;
            var count = players.Count;

            for (var i = 0; i < count; i++)
            {
                index = next ? (index + 1) % count : (index - 1 + count) % count;
                var candidate = players[index];
                if (candidate == null || ReferenceEquals(candidate, currentPlayer) || candidate.spectatePoint == null)
                {
                    continue;
                }

                spectate.playerOverride = null;
                spectate.currentPlayerListIndex = index;
                spectate.player = candidate;
                spectate.normalTransformPivot.position = candidate.spectatePoint.position;
                var aimHorizontal = candidate.transform.eulerAngles.y;
                spectate.normalAimHorizontal = aimHorizontal;
                spectate.normalAimVertical = 0f;
                spectate.normalTransformPivot.rotation = Quaternion.Euler(0f, aimHorizontal, 0f);
                spectate.normalTransformPivot.localRotation = Quaternion.Euler(
                    spectate.normalTransformPivot.localRotation.eulerAngles.x,
                    spectate.normalTransformPivot.localRotation.eulerAngles.y,
                    0f);
                spectate.normalTransformDistance.localPosition = new Vector3(0f, 0f, -2f);
                spectate.transform.position = spectate.normalTransformDistance.position;
                spectate.transform.rotation = spectate.normalTransformDistance.rotation;

                if (SemiFunc.IsMultiplayer())
                {
                    SemiFunc.HUDSpectateSetName(candidate.playerName ?? "unknown");
                }

                SemiFunc.LightManagerSetCullTargetTransform(candidate.transform);
                spectate.CameraTeleportImpulse();
                spectate.normalMaxDistance = 3f;
                PlayerController.instance?.playerAvatarScript?.localCamera?.Teleported();
                return true;
            }

            spectate.playerOverride = null;
            return false;
        }

        private static bool TryGetDeathHeadAnchor(PlayerAvatar player, out Vector3 anchor)
        {
            anchor = default;
            var deathHead = player.playerDeathHead;
            if (deathHead == null)
            {
                return false;
            }

            anchor = deathHead.transform.position;
            if (deathHead.physGrabObject != null)
            {
                anchor = deathHead.physGrabObject.centerPoint;
            }

            return true;
        }

        private static Transform? EnsureOrbitProxy()
        {
            if (s_orbitProxy != null)
            {
                return s_orbitProxy;
            }

            var go = GameObject.Find(OrbitProxyName);
            if (go == null)
            {
                go = new GameObject(OrbitProxyName);
                UnityEngine.Object.DontDestroyOnLoad(go);
            }

            s_orbitProxy = go.transform;
            return s_orbitProxy;
        }

        private static DeathHeadController? TryGetLocalDeathHeadController()
        {
            var local = PlayerAvatar.instance;
            if (local == null || local.playerDeathHead == null)
            {
                return null;
            }

            return DHHFunc.GetLocalDeathHeadController();
        }

        private static void EnsureFovBaseline(SpectateCamera spectate)
        {
            if (s_fovBaseline != null && s_fovBaseline.Matches(spectate))
            {
                return;
            }

            RestoreFovBaseline();
            s_fovBaseline = FovBaseline.Capture(spectate);
        }

        private static void ApplyFovState(SpectateCamera spectate, float targetFov)
        {
            var cameraZoom = CameraZoom.Instance;
            if (cameraZoom != null)
            {
                cameraZoom.playerZoomDefault = targetFov;
                cameraZoom.zoomPrev = targetFov;
                cameraZoom.zoomCurrent = targetFov;
                cameraZoom.zoomNew = targetFov;
            }

            var mainCamera = Camera.main;
            if (mainCamera != null)
            {
                mainCamera.fieldOfView = targetFov;
            }

            if (spectate.MainCamera != null)
            {
                spectate.MainCamera.fieldOfView = targetFov;
            }

            if (spectate.TopCamera != null)
            {
                spectate.TopCamera.fieldOfView = targetFov;
            }

            spectate.cameraFieldOfView = targetFov;
        }

        private static void RestoreFovBaseline()
        {
            var baseline = s_fovBaseline;
            s_fovBaseline = null;
            baseline?.Restore();
        }

        private static void EmitLegacyAlwaysWarningOnce()
        {
            if (s_legacyAlwaysWarningEmitted)
            {
                return;
            }

            s_legacyAlwaysWarningEmitted = true;
            Log.LogWarning(
                "[LastChance] SpectateDeadPlayersMode=Always is now scoped to active LastChance only. " +
                "Use LastChanceOnly for the same active-runtime behavior; inactive spectate is never modified.");
        }

        private static void DebugLogState(SpectateCamera spectate)
        {
            if (!FeatureFlags.DebugLogging)
            {
                return;
            }

            var local = PlayerAvatar.instance;
            var current = spectate.player;
            var message =
                $"[LastChance] SpectateState active={IsActiveRuntimeGate()} cycle={ShouldCycleDisabledPlayers()} " +
                $"current={(current != null ? current.playerName : "null")} " +
                $"local={(local != null ? local.playerName : "null")}";
            if (string.Equals(s_lastSpectateDebugMessage, message, StringComparison.Ordinal))
            {
                return;
            }

            s_lastSpectateDebugMessage = message;
            if (LogLimiter.ShouldLog(DebugStateLogKey, 30))
            {
                Log.LogDebug(message);
            }
        }

        private sealed class FovBaseline
        {
            private SpectateCamera? _spectate;
            private Camera? _mainCamera;
            private Camera? _spectateMainCamera;
            private Camera? _topCamera;
            private CameraZoom? _cameraZoom;
            private float _mainCameraFov;
            private float _spectateMainCameraFov;
            private float _topCameraFov;
            private float _spectateFieldOfView;
            private float _zoomDefault;
            private float _zoomPrev;
            private float _zoomCurrent;
            private float _zoomNew;

            internal static FovBaseline Capture(SpectateCamera spectate)
            {
                var baseline = new FovBaseline
                {
                    _spectate = spectate,
                    _mainCamera = Camera.main,
                    _spectateMainCamera = spectate.MainCamera,
                    _topCamera = spectate.TopCamera,
                    _cameraZoom = CameraZoom.Instance,
                    _spectateFieldOfView = spectate.cameraFieldOfView
                };

                if (baseline._mainCamera != null)
                {
                    baseline._mainCameraFov = baseline._mainCamera.fieldOfView;
                }

                if (baseline._spectateMainCamera != null)
                {
                    baseline._spectateMainCameraFov = baseline._spectateMainCamera.fieldOfView;
                }

                if (baseline._topCamera != null)
                {
                    baseline._topCameraFov = baseline._topCamera.fieldOfView;
                }

                if (baseline._cameraZoom != null)
                {
                    baseline._zoomDefault = baseline._cameraZoom.playerZoomDefault;
                    baseline._zoomPrev = baseline._cameraZoom.zoomPrev;
                    baseline._zoomCurrent = baseline._cameraZoom.zoomCurrent;
                    baseline._zoomNew = baseline._cameraZoom.zoomNew;
                }

                return baseline;
            }

            internal bool Matches(SpectateCamera spectate)
            {
                return ReferenceEquals(_spectate, spectate) &&
                       ReferenceEquals(_mainCamera, Camera.main) &&
                       ReferenceEquals(_spectateMainCamera, spectate.MainCamera) &&
                       ReferenceEquals(_topCamera, spectate.TopCamera) &&
                       ReferenceEquals(_cameraZoom, CameraZoom.Instance);
            }

            internal void Restore()
            {
                if (_cameraZoom != null)
                {
                    _cameraZoom.playerZoomDefault = _zoomDefault;
                    _cameraZoom.zoomPrev = _zoomPrev;
                    _cameraZoom.zoomCurrent = _zoomCurrent;
                    _cameraZoom.zoomNew = _zoomNew;
                }

                if (_mainCamera != null)
                {
                    _mainCamera.fieldOfView = _mainCameraFov;
                }

                if (_spectateMainCamera != null)
                {
                    _spectateMainCamera.fieldOfView = _spectateMainCameraFov;
                }

                if (_topCamera != null)
                {
                    _topCamera.fieldOfView = _topCameraFov;
                }

                if (_spectate != null)
                {
                    _spectate.cameraFieldOfView = _spectateFieldOfView;
                }
            }
        }
    }

    [HarmonyPatch(typeof(SpectateCamera), nameof(SpectateCamera.PlayerSwitch), new[] { typeof(bool) })]
    internal static class SpectateCameraLastChancePlayerSwitchPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(SpectateCamera __instance, bool _next)
        {
            if (!LastChanceSpectateHelper.IsActiveRuntimeGate())
            {
                return true;
            }

            // Debug mode bypasses only the disabled-player switch handler; camera support remains active.
            if (FeatureFlags.DebugLogging)
            {
                LastChanceSpectateHelper.Log.LogInfo(
                    $"[LastChance] PlayerSwitch gate bypassed by DebugLogging; " +
                    $"vanilla/DHH handler will run (next={_next}, state={__instance.currentState}).");
                return true;
            }

            LastChanceSpectateHelper.HandlePlayerSwitch(__instance, _next);
            return false;
        }
    }

    [HarmonyPatch(typeof(SpectateCamera), nameof(SpectateCamera.StateNormal))]
    internal static class SpectateCameraLastChanceStateNormalPatch
    {
        [HarmonyPrefix]
        private static void Prefix(SpectateCamera __instance, out LastChanceSpectatePointInvocationState? __state)
        {
            __state = null;
            if (!LastChanceSpectateHelper.IsActiveRuntimeGate())
            {
                return;
            }

            __state = LastChanceSpectateHelper.TryApplyOrbitProxy(__instance);
        }

        [HarmonyPostfix]
        private static void Postfix(SpectateCamera __instance, LastChanceSpectatePointInvocationState? __state)
        {
            LastChanceSpectateHelper.RestoreOrbitProxyInvocation(__state);

            if (!LastChanceSpectateHelper.IsActiveRuntimeGate())
            {
                return;
            }

            LastChanceSpectateHelper.HandleStateNormalPostfix(__instance);
        }
    }

    [HarmonyPatch(typeof(SpectateCamera), nameof(SpectateCamera.UpdateState), new[] { typeof(SpectateCamera.State) })]
    internal static class SpectateCameraLastChanceUpdateStatePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(SpectateCamera __instance, SpectateCamera.State _state)
        {
            if (!LastChanceSpectateHelper.IsActiveRuntimeGate())
            {
                return true;
            }

            return __instance == null || _state != SpectateCamera.State.Head;
        }
    }

    [HarmonyPatch(typeof(SpectateCamera), nameof(SpectateCamera.LateUpdate))]
    internal static class SpectateCameraLastChanceLateUpdatePatch
    {
        [HarmonyPostfix]
        private static void Postfix(SpectateCamera __instance)
        {
            if (!LastChanceSpectateHelper.IsActiveRuntimeGate())
            {
                return;
            }

            LastChanceSpectateHelper.MaintainFovState(__instance);
        }
    }
}

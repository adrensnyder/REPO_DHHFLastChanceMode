#nullable enable

using BepInEx.Logging;
using DHHFLastChanceMode.Modules.Config;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Adapters;
using DHHFLastChanceMode.Modules.Utilities;
using HarmonyLib;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Pipeline
{
    [HarmonyPatch(typeof(EnemyOnScreen), "Awake")]
    internal static class LastChanceMonstersOnScreenCameraModule
    {
        private static readonly ManualLogSource Log = Logger.CreateLogSource("DHHFLastChanceMode.LastChance.ThinMan");
        private static readonly System.Collections.Generic.Dictionary<string, bool> s_lastBoolStateByKey = new();

        internal static void ResetRuntimeState()
        {
            s_lastBoolStateByKey.Clear();
        }

        [HarmonyPostfix]
        private static void AwakePostfix(EnemyOnScreen __instance)
        {
            if (__instance == null)
            {
                return;
            }

            if (__instance.GetComponent<OnScreenCameraSyncRuntime>() == null)
            {
                __instance.gameObject.AddComponent<OnScreenCameraSyncRuntime>();
                DebugLog("OnScreen.AwakeAttach", $"enemy={__instance.gameObject.name} attachedRuntimeSync=True");
            }
        }

        internal static void DebugLog(string reason, string detail)
        {
            if (!InternalDebugFlags.DebugLastChanceThinManFlow)
            {
                return;
            }

            if (!LogLimiter.ShouldLog($"ThinMan.{reason}", 300))
            {
                return;
            }

            Log.LogInfo($"[ThinMan][{reason}] {detail}");
        }

        internal static void DebugLogOnBoolTransition(string reason, string key, bool value, string detail)
        {
            if (!InternalDebugFlags.DebugLastChanceThinManFlow)
            {
                return;
            }

            var stateKey = $"{reason}.{key}";
            if (s_lastBoolStateByKey.TryGetValue(stateKey, out var previous) && previous == value)
            {
                if (!LogLimiter.ShouldLog($"ThinMan.{stateKey}.Heartbeat", 600))
                {
                    return;
                }
            }

            s_lastBoolStateByKey[stateKey] = value;
            Log.LogInfo($"[ThinMan][{reason}] {detail}");
        }
    }

    internal sealed class OnScreenCameraSyncRuntime : MonoBehaviour
    {
        private EnemyOnScreen? _onScreen;
        private bool _lastSyncedOnScreenLocal;
        private bool _lastSyncedCulledLocal;
        private bool _hasSyncSnapshot;
        private int _lastCameraInstanceId;
        private bool _hasCameraSnapshot;

        private void Awake()
        {
            _onScreen = GetComponent<EnemyOnScreen>();
        }

        private void LateUpdate()
        {
            if (_onScreen == null || !LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
            {
                return;
            }

            var current = CameraUtils.Instance != null ? CameraUtils.Instance.MainCamera : Camera.main;
            if (current != null)
            {
                _onScreen.MainCamera = current;

                var currentCameraId = current.GetInstanceID();
                var cameraChanged = !_hasCameraSnapshot || _lastCameraInstanceId != currentCameraId;
                if (cameraChanged)
                {
                    LastChanceMonstersOnScreenCameraModule.DebugLog("Camera.Sync", $"enemy={_onScreen.gameObject.name} camera={current.name} changed={cameraChanged}");
                }

                _lastCameraInstanceId = currentCameraId;
                _hasCameraSnapshot = true;
            }

            SyncLocalHeadProxyOnScreenState();
        }

        private void SyncLocalHeadProxyOnScreenState()
        {
            if (_onScreen == null || !GameManager.Multiplayer())
            {
                return;
            }

            var localPlayer = GetLocalPlayerAvatar();
            if (localPlayer?.photonView == null || localPlayer.photonView.ViewID < 0)
            {
                return;
            }

            if (!LastChanceMonstersTargetProxyHelper.IsHeadProxyActive(localPlayer))
            {
                _hasSyncSnapshot = false;
                LastChanceMonstersOnScreenCameraModule.DebugLog("Sync.Skip.NoHeadProxy", $"enemy={_onScreen.gameObject.name} player={localPlayer.photonView.ViewID}");
                return;
            }

            var onScreenLocal = _onScreen.OnScreenLocal;
            var culledLocal = _onScreen.CulledLocal;

            if (_hasSyncSnapshot && onScreenLocal == _lastSyncedOnScreenLocal && culledLocal == _lastSyncedCulledLocal)
            {
                return;
            }

            _lastSyncedOnScreenLocal = onScreenLocal;
            _lastSyncedCulledLocal = culledLocal;
            _hasSyncSnapshot = true;

            _onScreen.OnScreenPlayerUpdate(localPlayer.photonView.ViewID, onScreenLocal, culledLocal);
            LastChanceMonstersOnScreenCameraModule.DebugLogOnBoolTransition(
                "Sync.PlayerUpdate",
                $"{_onScreen.GetInstanceID()}.{localPlayer.photonView.ViewID}.OnScreen",
                onScreenLocal,
                $"enemy={_onScreen.gameObject.name} player={localPlayer.photonView.ViewID} onScreenLocal={onScreenLocal} culledLocal={culledLocal}");
            LastChanceMonstersOnScreenCameraModule.DebugLogOnBoolTransition(
                "Sync.PlayerUpdate",
                $"{_onScreen.GetInstanceID()}.{localPlayer.photonView.ViewID}.Culled",
                culledLocal,
                $"enemy={_onScreen.gameObject.name} player={localPlayer.photonView.ViewID} onScreenLocal={onScreenLocal} culledLocal={culledLocal}");
        }

        private static PlayerAvatar? GetLocalPlayerAvatar()
        {
            var director = GameDirector.instance;
            if (director == null || director.PlayerList == null)
            {
                return null;
            }

            foreach (var player in director.PlayerList)
            {
                if (player?.photonView != null && player.photonView.IsMine)
                {
                    return player;
                }
            }

            return null;
        }
    }

    [HarmonyPatch(typeof(EnemyOnScreen), "GetOnScreen")]
    internal static class LastChanceMonstersOnScreenSafeLookupPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(EnemyOnScreen __instance, PlayerAvatar _playerAvatar, ref bool __result)
        {
            if (__instance == null || _playerAvatar == null)
            {
                __result = false;
                return false;
            }

            if (!GameManager.Multiplayer())
            {
                __result = __instance.OnScreenLocal;
                LastChanceMonstersOnScreenCameraModule.DebugLog(
                    "GetOnScreen.Singleplayer",
                    $"enemy={__instance.gameObject.name} player={(_playerAvatar.photonView != null ? _playerAvatar.photonView.ViewID.ToString() : "n/a")} result={__result}");
                return false;
            }

            if (LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled() &&
                LastChanceMonstersTargetProxyHelper.IsHeadProxyActive(_playerAvatar) &&
                _playerAvatar.photonView != null &&
                _playerAvatar.photonView.IsMine)
            {
                __result = __instance.OnScreenLocal;
                LastChanceMonstersOnScreenCameraModule.DebugLog(
                    "GetOnScreen.HeadProxyLocal",
                    $"enemy={__instance.gameObject.name} player={_playerAvatar.photonView.ViewID} result={__result}");
                return false;
            }

            var key = _playerAvatar.photonView != null ? _playerAvatar.photonView.ViewID : -1;
            if (key < 0)
            {
                __result = false;
                return false;
            }

            if (!__instance.OnScreenPlayer.ContainsKey(key))
            {
                __instance.OnScreenPlayer[key] = false;
                __result = false;
                LastChanceMonstersOnScreenCameraModule.DebugLog(
                    "GetOnScreen.DictMiss",
                    $"enemy={__instance.gameObject.name} player={key} result={__result}");
                return false;
            }

            __result = __instance.OnScreenPlayer[key];
            LastChanceMonstersOnScreenCameraModule.DebugLogOnBoolTransition(
                "GetOnScreen.DictHit",
                $"{__instance.GetInstanceID()}.{key}",
                __result,
                $"enemy={__instance.gameObject.name} player={key} result={__result}");
            return false;
        }
    }
}

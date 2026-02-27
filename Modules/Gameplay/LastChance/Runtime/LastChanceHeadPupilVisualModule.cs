#nullable enable

using System.Collections.Generic;
using BepInEx.Logging;
using DHHFLastChanceMode.Modules.Config;
using DHHFLastChanceMode.Modules.Utilities;
using HarmonyLib;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Runtime
{
    [HarmonyPatch(typeof(PlayerDeathHead), "Update")]
    internal static class LastChanceHeadPupilVisualModule
    {
        private static readonly ManualLogSource Log = Logger.CreateLogSource("DHHFLastChanceMode.LastChance.Eyes");
        private static readonly Dictionary<int, Color> LastEyeColorByHeadId = new();

        internal static void ResetRuntimeState()
        {
            LastEyeColorByHeadId.Clear();
        }

        [HarmonyPostfix]
        private static void Postfix(PlayerDeathHead __instance)
        {
            if (__instance == null)
            {
                return;
            }

            var player = __instance.playerAvatar;
            if (!LastChancePupilGate.TryGetEligibleHead(player, out _, out var gateReason))
            {
                DebugLog("Skip.Gate", $"reason={gateReason} playerId={GetPlayerId(player)}");
                return;
            }

            ForcePupilsVisible(__instance);
            ForcePupilOverlayVisible(__instance);
            ForceEyeLookPipeline(__instance);
        }

        private static void ForcePupilsVisible(PlayerDeathHead head)
        {
            var right = head.pupilScaleTransformRight;
            var left = head.pupilScaleTransformLeft;
            if (right == null || left == null)
            {
                DebugLog("Pupil.Visible.MissingTransforms", $"headId={head.GetInstanceID()}");
                return;
            }

            var defaultScale = head.pupilScaleDefault;
            if (!right.gameObject.activeSelf)
            {
                right.gameObject.SetActive(true);
            }

            if (!left.gameObject.activeSelf)
            {
                left.gameObject.SetActive(true);
            }

            right.localScale = defaultScale;
            left.localScale = defaultScale;
            DebugLog(
                "Pupil.Visible.Forced",
                $"headId={head.GetInstanceID()} leftActive={left.gameObject.activeSelf} rightActive={right.gameObject.activeSelf} leftScale={left.localScale} rightScale={right.localScale}");
        }

        private static void ForcePupilOverlayVisible(PlayerDeathHead head)
        {
            var pupilMaterial = head.pupilMaterial;
            if (pupilMaterial == null)
            {
                DebugLog("Pupil.Overlay.MissingMaterial", $"headId={head.GetInstanceID()}");
                return;
            }

            var amountPropertyId = head.eyeMaterialAmount;
            var colorPropertyId = head.eyeMaterialColor;
            pupilMaterial.SetFloat(amountPropertyId, 1f);

            var eyeMaterial = head.eyeMaterial;
            var headId = head.GetInstanceID();
            if (eyeMaterial != null)
            {
                var eyeColor = eyeMaterial.GetColor(colorPropertyId);
                if (!LastEyeColorByHeadId.TryGetValue(headId, out var lastEyeColor) || !ApproximatelyEqual(lastEyeColor, eyeColor))
                {
                    LastEyeColorByHeadId[headId] = eyeColor;
                    var oppositePupilColor = GetOppositeColor(eyeColor);
                    pupilMaterial.SetColor(colorPropertyId, oppositePupilColor);
                    DebugLog(
                        "Pupil.Color.SyncedOnEyeChange",
                        $"headId={headId} eyeColor={eyeColor} pupilColor={oppositePupilColor}");
                }
            }

            var amountReadback = pupilMaterial.GetFloat(amountPropertyId);
            var colorReadback = pupilMaterial.GetColor(colorPropertyId);
            DebugLog(
                "Pupil.Overlay.Forced",
                $"headId={headId} amountPropertyId={amountPropertyId} amount={amountReadback:F3} colorPropertyId={colorPropertyId} color={colorReadback}");

            var renderers = head.pupilRenderers;
            if (renderers == null || renderers.Length == 0)
            {
                DebugLog("Pupil.Renderers.Missing", $"headId={head.GetInstanceID()}");
                return;
            }

            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                {
                    DebugLog("Pupil.Renderers.Null", $"headId={head.GetInstanceID()} idx={i}");
                    continue;
                }

                DebugLog(
                    "Pupil.Renderers.State",
                    $"headId={head.GetInstanceID()} idx={i} rendererEnabled={renderer.enabled} activeSelf={renderer.gameObject.activeSelf} activeInHierarchy={renderer.gameObject.activeInHierarchy}");
            }
        }

        private static void ForceEyeLookPipeline(PlayerDeathHead head)
        {
            var eyes = head.playerEyes;
            if (eyes == null)
            {
                DebugLog("Eyes.MissingPlayerEyes", $"headId={head.GetInstanceID()}");
                return;
            }

            if (!eyes.enabled)
            {
                eyes.enabled = true;
                DebugLog("Eyes.Enabled.Forced", $"headId={head.GetInstanceID()}");
                return;
            }

            DebugLog("Eyes.Enabled.Already", $"headId={head.GetInstanceID()}");
        }

        private static void DebugLog(string reason, string detail)
        {
            if (!FeatureFlags.DebugLogging || !InternalDebugFlags.DebugLastChanceEyesFlow || !LogLimiter.ShouldLog($"LastChance.Eyes.{reason}", 90))
            {
                return;
            }

            Log.LogInfo($"[LastChance][Eyes][{reason}] {detail}");
        }

        private static int GetPlayerId(PlayerAvatar? player)
        {
            if (player == null)
            {
                return -1;
            }

            return player.photonView != null ? player.photonView.ViewID : player.GetInstanceID();
        }

        private static Color GetOppositeColor(Color source)
        {
            return new Color(1f - source.r, 1f - source.g, 1f - source.b, source.a);
        }

        private static bool ApproximatelyEqual(Color a, Color b)
        {
            const float epsilon = 0.001f;
            return Mathf.Abs(a.r - b.r) < epsilon &&
                   Mathf.Abs(a.g - b.g) < epsilon &&
                   Mathf.Abs(a.b - b.b) < epsilon &&
                   Mathf.Abs(a.a - b.a) < epsilon;
        }
    }
}


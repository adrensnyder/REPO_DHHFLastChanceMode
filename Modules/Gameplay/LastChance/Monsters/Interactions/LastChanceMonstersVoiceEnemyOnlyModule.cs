#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using DHHFLastChanceMode.Modules.Config;
using DeathHeadHopper.DeathHead;
using HarmonyLib;
using UnityEngine;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Interactions
{
    [HarmonyPatch(typeof(PlayerVoiceChat), "Update")]
    internal static class LastChanceMonstersVoiceEnemyOnlyModule
    {
        private static readonly Dictionary<int, float> OriginalAudioSourceVolumeByViewId = new();
        private static readonly Dictionary<int, float> OriginalTtsVolumeByViewId = new();

        internal static void ResetRuntimeState()
        {
            var voiceChats = UnityEngine.Object.FindObjectsOfType<PlayerVoiceChat>();
            for (var i = 0; i < voiceChats.Length; i++)
            {
                var voiceChat = voiceChats[i];
                if (voiceChat == null)
                {
                    continue;
                }

                var photonView = voiceChat.photonView;
                var viewId = photonView?.ViewID ?? -1;
                RestoreVolumes(voiceChat, viewId);
            }

            OriginalAudioSourceVolumeByViewId.Clear();
            OriginalTtsVolumeByViewId.Clear();
        }

        [HarmonyPostfix]
        private static void Postfix(PlayerVoiceChat __instance)
        {
            if (__instance == null)
            {
                return;
            }

            var playerAvatar = __instance.playerAvatar;
            var photonView = __instance.photonView;
            if (playerAvatar == null || photonView == null)
            {
                return;
            }

            var viewId = photonView.ViewID;
            if (!ShouldApply(playerAvatar))
            {
                RestoreVolumes(__instance, viewId);
                return;
            }

            // Keep vanilla PlayerVoiceChat pipeline active (incl. investigate logic), but force no audible playback to players.
            ApplyEnemyOnlyVoiceMix(__instance, viewId);
            ForceTalkAnimationEnabled(__instance);
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var replacement = AccessTools.Method(typeof(LastChanceMonstersVoiceEnemyOnlyModule), nameof(GetEffectiveHeadSpectated));
            if (replacement == null)
            {
                return instructions;
            }

            var list = new List<CodeInstruction>(instructions);
            for (var i = 0; i < list.Count; i++)
            {
                var ins = list[i];
                if (ins.opcode == OpCodes.Ldfld && ins.operand is System.Reflection.FieldInfo f && f.Name == nameof(PlayerDeathHead.spectated) && f.DeclaringType == typeof(PlayerDeathHead))
                {
                    ins.opcode = OpCodes.Call;
                    ins.operand = replacement;
                }
            }

            return list;
        }

        private static bool ShouldApply(PlayerAvatar player)
        {
            return FeatureFlags.LastChanceMonstersVoiceEnemyOnlyEnabled &&
                   LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled() &&
                   LastChanceMonstersTargetProxyHelper.IsHeadProxyActive(player);
        }

        private static bool GetEffectiveHeadSpectated(PlayerDeathHead? head)
        {
            if (head == null)
            {
                return false;
            }

            // Vanilla State.Head path.
            if (head.spectated)
            {
                return true;
            }

            // Outside LastChance, keep vanilla behavior unchanged.
            if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled() || !FeatureFlags.LastChanceMonstersVoiceEnemyOnlyEnabled)
            {
                return false;
            }

            // DHH path: SpectateCamera Head is blocked, but DHH controller can still be spectated.
            if (head.TryGetComponent<DeathHeadController>(out var controller) && controller != null && controller.spectated)
            {
                return true;
            }

            return false;
        }

        private static void ApplyEnemyOnlyVoiceMix(PlayerVoiceChat voiceChat, int viewId)
        {
            var audioSource = voiceChat.audioSource;
            if (audioSource != null)
            {
                if (!OriginalAudioSourceVolumeByViewId.ContainsKey(viewId))
                {
                    OriginalAudioSourceVolumeByViewId[viewId] = audioSource.volume;
                }

                audioSource.volume = 0f;
            }

            var ttsAudioSource = voiceChat.ttsAudioSource;
            if (ttsAudioSource != null)
            {
                if (!OriginalTtsVolumeByViewId.ContainsKey(viewId))
                {
                    OriginalTtsVolumeByViewId[viewId] = ttsAudioSource.volume;
                }

                ttsAudioSource.volume = 0f;
            }
        }

        private static void ForceTalkAnimationEnabled(PlayerVoiceChat voiceChat)
        {
            voiceChat.overrideNoTalkAnimationTimer = 0f;
        }

        private static void RestoreVolumes(PlayerVoiceChat voiceChat, int viewId)
        {
            var audioSource = voiceChat.audioSource;
            if (audioSource != null && OriginalAudioSourceVolumeByViewId.TryGetValue(viewId, out var originalVolume))
            {
                audioSource.volume = originalVolume;
                OriginalAudioSourceVolumeByViewId.Remove(viewId);
            }

            var ttsAudioSource = voiceChat.ttsAudioSource;
            if (ttsAudioSource != null && OriginalTtsVolumeByViewId.TryGetValue(viewId, out var originalTtsVolume))
            {
                ttsAudioSource.volume = originalTtsVolume;
                OriginalTtsVolumeByViewId.Remove(viewId);
            }
        }
    }
}


#nullable enable

using System;
using System.Collections.Generic;
using DHHFLastChanceMode.Modules.Config;
using DeathHeadHopper.DeathHead;
using HarmonyLib;
using UnityEngine;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Interactions
{
    [HarmonyPatch(typeof(PlayerVoiceChat), nameof(PlayerVoiceChat.Update))]
    internal static class LastChanceMonstersVoiceEnemyOnlyModule
    {
        private static readonly Dictionary<int, float> OriginalAudioSourceVolumeByViewId = new();
        private static readonly Dictionary<int, float> OriginalTtsVolumeByViewId = new();
        private static readonly Dictionary<int, PlayerDeathHead> TemporaryHeadSpectatedOverrideByViewId = new();

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
            TemporaryHeadSpectatedOverrideByViewId.Clear();
        }

        [HarmonyPrefix]
        private static void Prefix(PlayerVoiceChat __instance)
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

            TryApplyTemporaryHeadSpectatedOverride(playerAvatar, photonView.ViewID);
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

        [HarmonyFinalizer]
        private static Exception? Finalizer(PlayerVoiceChat __instance, Exception? __exception)
        {
            if (__instance == null)
            {
                return __exception;
            }

            var photonView = __instance.photonView;
            if (photonView == null)
            {
                return __exception;
            }

            RestoreTemporaryHeadSpectatedOverride(photonView.ViewID);
            return __exception;
        }

        private static bool ShouldApply(PlayerAvatar player)
        {
            return LastChanceMonstersTargetProxyHelper.IsRuntimeFeatureEnabled(FeatureFlags.LastChanceMonstersVoiceEnemyOnlyEnabled) &&
                   LastChanceMonstersTargetProxyHelper.IsHeadProxyActive(player);
        }

        private static void TryApplyTemporaryHeadSpectatedOverride(PlayerAvatar player, int viewId)
        {
            if (TemporaryHeadSpectatedOverrideByViewId.ContainsKey(viewId))
            {
                return;
            }

            if (!LastChanceMonstersTargetProxyHelper.IsRuntimeFeatureEnabled(FeatureFlags.LastChanceMonstersVoiceEnemyOnlyEnabled))
            {
                return;
            }

            var head = player.playerDeathHead;
            if (head == null || head.spectated)
            {
                return;
            }

            if (head.TryGetComponent<DeathHeadController>(out var controller) && controller != null && controller.spectated)
            {
                head.spectated = true;
                TemporaryHeadSpectatedOverrideByViewId[viewId] = head;
            }
        }

        private static void RestoreTemporaryHeadSpectatedOverride(int viewId)
        {
            if (!TemporaryHeadSpectatedOverrideByViewId.TryGetValue(viewId, out var head) || head == null)
            {
                return;
            }

            head.spectated = false;
            TemporaryHeadSpectatedOverrideByViewId.Remove(viewId);
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


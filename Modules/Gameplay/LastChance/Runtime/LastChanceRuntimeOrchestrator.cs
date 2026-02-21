#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Runtime
{
    internal enum LastChanceRuntimeState
    {
        Inactive = 0,
        Arming = 1,
        Active = 2,
        Cooldown = 3,
        Teardown = 4
    }

    internal static class LastChanceRuntimeOrchestrator
    {
        private static readonly Dictionary<string, float> s_behaviorCooldownUntil = new();
        private static LastChanceRuntimeState s_state = LastChanceRuntimeState.Inactive;
        private static string s_lastTransitionReason = "init";
        private static float s_lastTransitionAt;

        internal static LastChanceRuntimeState State => s_state;
        internal static bool IsRuntimeActive => s_state == LastChanceRuntimeState.Active;
        internal static string LastTransitionReason => s_lastTransitionReason;
        internal static float LastTransitionAt => s_lastTransitionAt;

        internal static void EnterActiveRuntime()
        {
            s_state = LastChanceRuntimeState.Active;
            s_lastTransitionReason = "enter-active";
            s_lastTransitionAt = Time.realtimeSinceStartup;
        }

        internal static void ExitRuntime(string reason)
        {
            s_state = LastChanceRuntimeState.Teardown;
            s_lastTransitionReason = string.IsNullOrWhiteSpace(reason) ? "exit-runtime" : reason.Trim();
            s_lastTransitionAt = Time.realtimeSinceStartup;
            s_behaviorCooldownUntil.Clear();
            s_state = LastChanceRuntimeState.Inactive;
        }

        internal static void OnLevelTransition()
        {
            s_state = LastChanceRuntimeState.Teardown;
            s_lastTransitionReason = "level-transition";
            s_lastTransitionAt = Time.realtimeSinceStartup;
            s_behaviorCooldownUntil.Clear();
            s_state = LastChanceRuntimeState.Inactive;
        }

        internal static bool CanApplyMonsterBehavior(string kind, int sourceId)
        {
            if (!IsRuntimeActive)
            {
                return false;
            }

            var key = BuildKey(kind, sourceId);
            if (!s_behaviorCooldownUntil.TryGetValue(key, out var until))
            {
                return true;
            }

            return Time.unscaledTime >= until;
        }

        internal static void SetMonsterBehaviorCooldown(string kind, int sourceId, float cooldownSeconds)
        {
            var key = BuildKey(kind, sourceId);
            if (cooldownSeconds <= 0f)
            {
                s_behaviorCooldownUntil.Remove(key);
                return;
            }

            s_behaviorCooldownUntil[key] = Time.unscaledTime + cooldownSeconds;
        }

        private static string BuildKey(string kind, int sourceId)
        {
            var normalizedKind = string.IsNullOrWhiteSpace(kind) ? "unknown" : kind.Trim();
            return $"{normalizedKind}:{sourceId}";
        }
    }
}

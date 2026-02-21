#nullable enable

using System.Collections.Generic;
using DHHFLastChanceMode.Modules.Config;
using UnityEngine;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Pipeline
{
    internal static class LastChanceMonstersCeilingEyeLockCoordinator
    {
        private sealed class LockState
        {
            internal float LockStartAt = -1f;
            internal float LastSeenAt = -1f;
            internal float CooldownUntil = -1f;
            internal float LastTouchAt = -1f;
        }

        private static readonly Dictionary<int, LockState> s_stateByPlayer = new();
        private static float s_nextCleanupAt;

        internal static void ResetRuntimeState()
        {
            s_stateByPlayer.Clear();
            s_nextCleanupAt = 0f;
        }

        internal static bool EvaluateVisionLock(PlayerAvatar? player, bool seen, float now, out string reason)
        {
            reason = "NoPlayer";
            var key = GetPlayerKey(player);
            if (key <= 0)
            {
                return false;
            }

            CleanupOldStates(now);
            var state = GetOrCreateState(key);
            state.LastTouchAt = now;

            if (state.CooldownUntil > now)
            {
                reason = "CooldownActive";
                return false;
            }

            var grace = Mathf.Max(0.05f, InternalConfig.LastChanceMonstersCameraLockKeepAliveGraceSeconds);
            if (!seen)
            {
                if (state.LastSeenAt < 0f || now - state.LastSeenAt > grace)
                {
                    state.LockStartAt = -1f;
                }

                reason = "NotSeen";
                return false;
            }

            if (state.LockStartAt < 0f || state.LastSeenAt < 0f || now - state.LastSeenAt > grace)
            {
                state.LockStartAt = now;
            }

            state.LastSeenAt = now;
            var maxLock = Mathf.Max(0.1f, InternalConfig.LastChanceMonstersCameraLockMaxSeconds);
            if (now - state.LockStartAt >= maxLock)
            {
                var cooldown = Mathf.Max(0.1f, InternalConfig.LastChanceMonstersCameraLockCooldownSeconds);
                state.CooldownUntil = now + cooldown;
                state.LockStartAt = -1f;
                state.LastSeenAt = -1f;
                reason = "ReachedMaxLock_SetCooldown";
                return false;
            }

            reason = "SeenAndAllowed";
            return true;
        }

        internal static bool CanForceCamera(PlayerAvatar? player, float now, out string reason)
        {
            reason = "NoPlayer";
            var key = GetPlayerKey(player);
            if (key <= 0)
            {
                return false;
            }

            CleanupOldStates(now);
            if (!s_stateByPlayer.TryGetValue(key, out var state))
            {
                reason = "NoVisionState";
                return false;
            }

            state.LastTouchAt = now;
            if (state.CooldownUntil > now)
            {
                reason = "CooldownActive";
                return false;
            }

            var grace = Mathf.Max(0.05f, InternalConfig.LastChanceMonstersCameraLockKeepAliveGraceSeconds);
            if (state.LastSeenAt < 0f || now - state.LastSeenAt > grace)
            {
                reason = "NoRecentVision";
                return false;
            }

            if (state.LockStartAt < 0f)
            {
                state.LockStartAt = now;
            }

            var maxLock = Mathf.Max(0.1f, InternalConfig.LastChanceMonstersCameraLockMaxSeconds);
            if (now - state.LockStartAt >= maxLock)
            {
                var cooldown = Mathf.Max(0.1f, InternalConfig.LastChanceMonstersCameraLockCooldownSeconds);
                state.CooldownUntil = now + cooldown;
                state.LockStartAt = -1f;
                state.LastSeenAt = -1f;
                reason = "ReachedMaxLock_SetCooldown";
                return false;
            }

            reason = "Allowed";
            return true;
        }

        private static LockState GetOrCreateState(int key)
        {
            if (s_stateByPlayer.TryGetValue(key, out var state))
            {
                return state;
            }

            state = new LockState();
            s_stateByPlayer[key] = state;
            return state;
        }

        private static int GetPlayerKey(PlayerAvatar? player)
        {
            if (player == null)
            {
                return 0;
            }

            return player.photonView != null ? player.photonView.ViewID : player.GetInstanceID();
        }

        private static void CleanupOldStates(float now)
        {
            if (now < s_nextCleanupAt)
            {
                return;
            }

            s_nextCleanupAt = now + 5f;
            if (s_stateByPlayer.Count == 0)
            {
                return;
            }

            var stale = new List<int>();
            foreach (var kvp in s_stateByPlayer)
            {
                var state = kvp.Value;
                if (state == null)
                {
                    stale.Add(kvp.Key);
                    continue;
                }

                var lastRelevant = Mathf.Max(state.LastTouchAt, state.CooldownUntil);
                if (lastRelevant < 0f || now - lastRelevant > 30f)
                {
                    stale.Add(kvp.Key);
                }
            }

            for (var i = 0; i < stale.Count; i++)
            {
                s_stateByPlayer.Remove(stale[i]);
            }
        }
    }
}

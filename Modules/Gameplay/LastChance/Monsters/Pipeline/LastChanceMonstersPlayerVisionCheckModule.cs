#nullable enable

using System.Collections.Generic;
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
        private const string PatchId = "DHHFLastChanceMode.Gameplay.LastChance.MonstersPlayerVisionCheck";
        private static readonly ManualLogSource Log = Logger.CreateLogSource("DHHFLastChanceMode.LastChance.CeilingEye");
        private static readonly HashSet<System.Reflection.MethodBase> s_patchedMethods = new();
        private static readonly List<System.Reflection.MethodBase> s_targetMethods =
            LastChanceMonstersPatchTargetHelper.BuildTargetList(AddTargetMethods);
        private static Harmony? s_harmony;

        private static readonly System.Reflection.MethodInfo? s_playerVisionCheckVanilla = AccessTools.Method(
            typeof(SemiFunc),
            nameof(SemiFunc.PlayerVisionCheck),
            new[] { typeof(Vector3), typeof(float), typeof(PlayerAvatar), typeof(bool) });

        private static readonly System.Reflection.MethodInfo? s_playerVisionCheckPositionVanilla = AccessTools.Method(
            typeof(SemiFunc),
            nameof(SemiFunc.PlayerVisionCheckPosition),
            new[] { typeof(Vector3), typeof(Vector3), typeof(float), typeof(PlayerAvatar), typeof(bool) });

        private static readonly System.Reflection.MethodInfo? s_playerVisionCheckProxy = AccessTools.Method(
            typeof(LastChanceMonstersPlayerVisionCheckModule),
            nameof(PlayerVisionCheckLastChanceAware));

        private static readonly System.Reflection.MethodInfo? s_playerVisionCheckPositionProxy = AccessTools.Method(
            typeof(LastChanceMonstersPlayerVisionCheckModule),
            nameof(PlayerVisionCheckPositionLastChanceAware));

        internal static void ResetRuntimeState()
        {
            LastChanceMonstersCeilingEyeLockCoordinator.ResetRuntimeState();
        }

        internal static void Apply()
        {
            if (s_harmony != null)
            {
                return;
            }

            s_harmony = new Harmony(PatchId);
            var transpiler = new HarmonyMethod(typeof(LastChanceMonstersPlayerVisionCheckModule), nameof(ReplaceVisionChecks));
            foreach (var method in s_targetMethods)
            {
                if (method == null || s_patchedMethods.Contains(method))
                {
                    continue;
                }

                s_harmony.Patch(method, transpiler: transpiler);
                s_patchedMethods.Add(method);
            }
        }

        internal static void Unapply()
        {
            if (s_harmony == null)
            {
                return;
            }

            try
            {
                s_harmony.UnpatchSelf();
            }
            catch
            {
                // Best-effort unpatch.
            }

            s_patchedMethods.Clear();
            s_harmony = null;
            ResetRuntimeState();
        }

        private static void AddTargetMethods(List<System.Reflection.MethodBase> methods)
        {
            LastChanceMonstersPatchTargetHelper.AddDeclaredMethod(methods, typeof(EnemyCeilingEye), nameof(EnemyCeilingEye.StateHasTarget));
            LastChanceMonstersPatchTargetHelper.AddDeclaredMethod(methods, typeof(EnemyCeilingEye), nameof(EnemyCeilingEye.OnVisionTrigger));
            LastChanceMonstersPatchTargetHelper.AddDeclaredMethod(methods, typeof(EnemySpinny), nameof(EnemySpinny.HasLineOfSight));
        }

        private static IEnumerable<CodeInstruction> ReplaceVisionChecks(IEnumerable<CodeInstruction> instructions)
        {
            if (s_playerVisionCheckVanilla == null || s_playerVisionCheckPositionVanilla == null || s_playerVisionCheckProxy == null || s_playerVisionCheckPositionProxy == null)
            {
                return instructions;
            }

            var list = new List<CodeInstruction>(instructions);
            for (var i = 0; i < list.Count; i++)
            {
                var ins = list[i];
                if (ins.opcode != System.Reflection.Emit.OpCodes.Call && ins.opcode != System.Reflection.Emit.OpCodes.Callvirt)
                {
                    continue;
                }

                if (ins.operand is not System.Reflection.MethodInfo called)
                {
                    continue;
                }

                if (called == s_playerVisionCheckVanilla)
                {
                    ins.opcode = System.Reflection.Emit.OpCodes.Call;
                    ins.operand = s_playerVisionCheckProxy;
                    continue;
                }

                if (called == s_playerVisionCheckPositionVanilla)
                {
                    ins.opcode = System.Reflection.Emit.OpCodes.Call;
                    ins.operand = s_playerVisionCheckPositionProxy;
                }
            }

            return list;
        }

        internal static bool PlayerVisionCheckLastChanceAware(Vector3 position, float range, PlayerAvatar player, bool previouslySeen)
        {
            if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
            {
                return SemiFunc.PlayerVisionCheck(position, range, player, previouslySeen);
            }

            if (LastChanceMonstersTargetProxyHelper.TryGetHeadProxyVisionTarget(player, out var headCenter))
            {
                return PlayerVisionCheckPositionLastChanceAware(position, headCenter, range, player, previouslySeen);
            }

            return SemiFunc.PlayerVisionCheck(position, range, player, previouslySeen);
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

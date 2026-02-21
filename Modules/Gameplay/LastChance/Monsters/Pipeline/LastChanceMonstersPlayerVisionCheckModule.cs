#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using DHHFLastChanceMode.Modules.Config;
using HarmonyLib;
using UnityEngine;
using BepInEx.Logging;
using DHHFLastChanceMode.Modules.Utilities;
using Logger = BepInEx.Logging.Logger;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Pipeline
{
    [HarmonyPatch]
    internal static class LastChanceMonstersPlayerVisionCheckModule
    {
        private const string PatchId = "DHHFLastChanceMode.Gameplay.LastChance.MonstersPlayerVisionCheck";
        private static readonly ManualLogSource Log = Logger.CreateLogSource("DHHFLastChanceMode.LastChance.CeilingEye");
        private static readonly HashSet<MethodBase> s_patchedMethods = new();
        private static Harmony? s_harmony;

        internal static void ResetRuntimeState()
        {
            LastChanceMonstersCeilingEyeLockCoordinator.ResetRuntimeState();
        }

        private static readonly MethodInfo? s_playerVisionCheckVanilla = AccessTools.Method(
            typeof(SemiFunc),
            "PlayerVisionCheck",
            new[] { typeof(Vector3), typeof(float), typeof(PlayerAvatar), typeof(bool) });

        private static readonly MethodInfo? s_playerVisionCheckPositionVanilla = AccessTools.Method(
            typeof(SemiFunc),
            "PlayerVisionCheckPosition",
            new[] { typeof(Vector3), typeof(Vector3), typeof(float), typeof(PlayerAvatar), typeof(bool) });

        private static readonly MethodInfo? s_playerVisionCheckProxy = AccessTools.Method(
            typeof(LastChanceMonstersPlayerVisionCheckModule),
            nameof(PlayerVisionCheckLastChanceAware));

        private static readonly MethodInfo? s_playerVisionCheckPositionProxy = AccessTools.Method(
            typeof(LastChanceMonstersPlayerVisionCheckModule),
            nameof(PlayerVisionCheckPositionLastChanceAware));

        [HarmonyPrepare]
        private static bool Prepare()
        {
            return false;
        }

        internal static void Apply()
        {
            if (s_harmony != null)
            {
                return;
            }

            s_harmony = new Harmony(PatchId);
            var transpiler = new HarmonyMethod(typeof(LastChanceMonstersPlayerVisionCheckModule), nameof(ReplaceVisionChecks));
            var methods = TargetMethods();
            foreach (var method in methods)
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

        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            var methods = new List<MethodBase>();
            Type[] types;
            try
            {
                types = typeof(Enemy).Assembly.GetTypes();
            }
            catch
            {
                return methods;
            }

            for (var i = 0; i < types.Length; i++)
            {
                var type = types[i];
                if (type == null || type.Name.IndexOf("Enemy", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                var typeMethods = type.GetMethods(flags);
                for (var m = 0; m < typeMethods.Length; m++)
                {
                    var method = typeMethods[m];
                    if (method == null || method.IsAbstract || method.GetMethodBody() == null)
                    {
                        continue;
                    }

                    if (MethodCallsVisionChecks(method))
                    {
                        methods.Add(method);
                    }
                }
            }

            return methods;
        }

        [HarmonyTranspiler]
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
                if (ins.opcode != OpCodes.Call && ins.opcode != OpCodes.Callvirt)
                {
                    continue;
                }

                if (ins.operand is not MethodInfo called)
                {
                    continue;
                }

                if (called == s_playerVisionCheckVanilla)
                {
                    ins.opcode = OpCodes.Call;
                    ins.operand = s_playerVisionCheckProxy;
                    continue;
                }

                if (called == s_playerVisionCheckPositionVanilla)
                {
                    ins.opcode = OpCodes.Call;
                    ins.operand = s_playerVisionCheckPositionProxy;
                }
            }

            return list;
        }

        private static bool MethodCallsVisionChecks(MethodBase method)
        {
            if (method == null || s_playerVisionCheckVanilla == null || s_playerVisionCheckPositionVanilla == null)
            {
                return false;
            }

            try
            {
                var body = method.GetMethodBody();
                var il = body?.GetILAsByteArray();
                if (il == null || il.Length < 5)
                {
                    return false;
                }

                var checkToken = s_playerVisionCheckVanilla.MetadataToken;
                var checkPosToken = s_playerVisionCheckPositionVanilla.MetadataToken;
                for (var i = 0; i <= il.Length - 5; i++)
                {
                    var op = il[i];
                    if (op != 0x28 && op != 0x6F)
                    {
                        continue;
                    }

                    var token = BitConverter.ToInt32(il, i + 1);
                    if (token == checkToken || token == checkPosToken)
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
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
            // Ceiling-eye specific resilience: test a few vertical samples so near-floor head proxies
            // are still detectable when floor geometry clips the direct center ray.
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

                // If the obstruction is extremely close to target point, treat it as endpoint grazing
                // (common when target is on/near floor).
                var hitToTarget = Vector3.Distance(hits[i].point, endPosition);
                if (hitToTarget <= 0.35f)
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private static void DebugVision(
            string reason,
            Vector3 startPosition,
            Vector3 endPosition,
            PlayerAvatar player,
            float now,
            bool decision)
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


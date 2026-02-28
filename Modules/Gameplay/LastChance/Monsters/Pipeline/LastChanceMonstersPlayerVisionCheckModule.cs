#nullable enable

using System.Collections.Generic;
using DHHFLastChanceMode.Modules.Config;
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
        private static readonly System.Reflection.Emit.OpCode[] s_singleByteOpcodes = BuildSingleByteOpcodeMap();
        private static readonly System.Reflection.Emit.OpCode[] s_doubleByteOpcodes = BuildDoubleByteOpcodeMap();
        private static readonly ManualLogSource Log = Logger.CreateLogSource("DHHFLastChanceMode.LastChance.CeilingEye");
        private static readonly HashSet<System.Reflection.MethodBase> s_patchedMethods = new();
        private static readonly List<System.Reflection.MethodBase> s_targetMethods = CreateTargetMethods();
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

                if (!MethodCallsVisionCheck(method))
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

        private static List<System.Reflection.MethodBase> CreateTargetMethods()
        {
            var methods = new List<System.Reflection.MethodBase>();
            AddMethod(methods, typeof(EnemyCeilingEye), "StateHasTarget");
            AddMethod(methods, typeof(EnemyCeilingEye), nameof(EnemyCeilingEye.OnVisionTrigger));
            AddMethod(methods, typeof(EnemySpinny), "HasLineOfSight");
            return methods;
        }

        private static void AddMethod(List<System.Reflection.MethodBase> methods, System.Type declaringType, string methodName, params System.Type[] argumentTypes)
        {
            var method = argumentTypes.Length == 0
                ? AccessTools.DeclaredMethod(declaringType, methodName)
                : AccessTools.DeclaredMethod(declaringType, methodName, argumentTypes);
            if (method != null)
            {
                methods.Add(method);
            }
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

        private static bool MethodCallsVisionCheck(System.Reflection.MethodBase method)
        {
            if (s_playerVisionCheckVanilla == null || s_playerVisionCheckPositionVanilla == null)
            {
                return false;
            }

            var body = method.GetMethodBody();
            var il = body?.GetILAsByteArray();
            if (il == null || il.Length == 0)
            {
                return false;
            }

            var position = 0;
            while (position < il.Length)
            {
                var opcode = ReadOpcode(il, ref position);
                if (opcode.Equals(default(System.Reflection.Emit.OpCode)))
                {
                    return false;
                }

                if (opcode.OperandType == System.Reflection.Emit.OperandType.InlineMethod)
                {
                    if (position + 4 > il.Length)
                    {
                        return false;
                    }

                    var token = System.BitConverter.ToInt32(il, position);
                    position += 4;
                    if (TryResolveMethod(method, token, out var called) &&
                        (called == s_playerVisionCheckVanilla || called == s_playerVisionCheckPositionVanilla))
                    {
                        return true;
                    }

                    continue;
                }

                if (!AdvanceOperand(il, ref position, opcode.OperandType))
                {
                    return false;
                }
            }

            return false;
        }

        private static bool TryResolveMethod(System.Reflection.MethodBase sourceMethod, int metadataToken, out System.Reflection.MethodBase? calledMethod)
        {
            calledMethod = null;
            try
            {
                var typeArgs = sourceMethod.DeclaringType?.GetGenericArguments();
                var methodArgs = sourceMethod is System.Reflection.MethodInfo info ? info.GetGenericArguments() : null;
                calledMethod = sourceMethod.Module.ResolveMethod(metadataToken, typeArgs, methodArgs);
                return calledMethod != null;
            }
            catch
            {
                return false;
            }
        }

        private static System.Reflection.Emit.OpCode ReadOpcode(byte[] il, ref int position)
        {
            if (position >= il.Length)
            {
                return default;
            }

            var b = il[position++];
            if (b != 0xFE)
            {
                return s_singleByteOpcodes[b];
            }

            if (position >= il.Length)
            {
                return default;
            }

            var b2 = il[position++];
            return s_doubleByteOpcodes[b2];
        }

        private static bool AdvanceOperand(byte[] il, ref int position, System.Reflection.Emit.OperandType operandType)
        {
            switch (operandType)
            {
                case System.Reflection.Emit.OperandType.InlineNone:
                    return true;
                case System.Reflection.Emit.OperandType.ShortInlineBrTarget:
                case System.Reflection.Emit.OperandType.ShortInlineI:
                case System.Reflection.Emit.OperandType.ShortInlineVar:
                    position += 1;
                    return position <= il.Length;
                case System.Reflection.Emit.OperandType.InlineVar:
                    position += 2;
                    return position <= il.Length;
                case System.Reflection.Emit.OperandType.InlineBrTarget:
                case System.Reflection.Emit.OperandType.InlineField:
                case System.Reflection.Emit.OperandType.InlineI:
                case System.Reflection.Emit.OperandType.InlineSig:
                case System.Reflection.Emit.OperandType.InlineString:
                case System.Reflection.Emit.OperandType.InlineTok:
                case System.Reflection.Emit.OperandType.InlineType:
                    position += 4;
                    return position <= il.Length;
                case System.Reflection.Emit.OperandType.InlineI8:
                case System.Reflection.Emit.OperandType.InlineR:
                    position += 8;
                    return position <= il.Length;
                case System.Reflection.Emit.OperandType.ShortInlineR:
                    position += 4;
                    return position <= il.Length;
                case System.Reflection.Emit.OperandType.InlineSwitch:
                    if (position + 4 > il.Length)
                    {
                        return false;
                    }

                    var count = System.BitConverter.ToInt32(il, position);
                    position += 4 + (count * 4);
                    return position <= il.Length;
                default:
                    return false;
            }
        }

        private static System.Reflection.Emit.OpCode[] BuildSingleByteOpcodeMap()
        {
            var map = new System.Reflection.Emit.OpCode[256];
            var fields = typeof(System.Reflection.Emit.OpCodes).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            for (var i = 0; i < fields.Length; i++)
            {
                if (fields[i].GetValue(null) is not System.Reflection.Emit.OpCode opcode)
                {
                    continue;
                }

                var value = (ushort)opcode.Value;
                if (value <= 0xFF)
                {
                    map[value] = opcode;
                }
            }

            return map;
        }

        private static System.Reflection.Emit.OpCode[] BuildDoubleByteOpcodeMap()
        {
            var map = new System.Reflection.Emit.OpCode[256];
            var fields = typeof(System.Reflection.Emit.OpCodes).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            for (var i = 0; i < fields.Length; i++)
            {
                if (fields[i].GetValue(null) is not System.Reflection.Emit.OpCode opcode)
                {
                    continue;
                }

                var value = (ushort)opcode.Value;
                if ((value & 0xFF00) == 0xFE00)
                {
                    map[value & 0xFF] = opcode;
                }
            }

            return map;
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

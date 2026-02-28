#nullable enable

using System;
using System.Collections.Generic;
using DHHFLastChanceMode.Modules.Config;
using HarmonyLib;
using UnityEngine;
using BepInEx.Logging;
using DHHFLastChanceMode.Modules.Utilities;
using Logger = BepInEx.Logging.Logger;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Pipeline
{
    [HarmonyPatch]
    internal static class LastChanceMonstersCameraForceLockModule
    {
        private const string PatchId = "DHHFLastChanceMode.Gameplay.LastChance.MonstersCameraForceLock";
        private static readonly System.Reflection.Emit.OpCode[] s_singleByteOpcodes = BuildSingleByteOpcodeMap();
        private static readonly System.Reflection.Emit.OpCode[] s_doubleByteOpcodes = BuildDoubleByteOpcodeMap();
        private static readonly ManualLogSource Log = Logger.CreateLogSource("DHHFLastChanceMode.LastChance.CeilingEye");
        private static readonly HashSet<System.Reflection.MethodBase> s_patchedMethods = new();
        private static Harmony? s_harmony;

        private static readonly System.Reflection.MethodInfo? s_aimTargetSoftSetVanilla =
            AccessTools.Method(typeof(CameraAim), "AimTargetSoftSet", new[] { typeof(Vector3), typeof(float), typeof(float), typeof(float), typeof(GameObject), typeof(int) });

        private static readonly System.Reflection.MethodInfo? s_aimTargetSetVanilla =
            AccessTools.Method(typeof(CameraAim), "AimTargetSet", new[] { typeof(Vector3), typeof(float), typeof(float), typeof(GameObject), typeof(int) });

        private static readonly System.Reflection.MethodInfo? s_aimTargetSoftSetProxy =
            AccessTools.Method(typeof(LastChanceMonstersCameraForceLockModule), nameof(AimTargetSoftSetLastChanceAware));

        private static readonly System.Reflection.MethodInfo? s_aimTargetSetProxy =
            AccessTools.Method(typeof(LastChanceMonstersCameraForceLockModule), nameof(AimTargetSetLastChanceAware));

        internal static void Apply()
        {
            if (s_harmony != null)
            {
                return;
            }

            s_harmony = new Harmony(PatchId);
            var transpiler = new HarmonyMethod(typeof(LastChanceMonstersCameraForceLockModule), nameof(ReplaceCameraAimCalls));
            var methods = TargetMethods();
            foreach (var method in methods)
            {
                if (method == null || s_patchedMethods.Contains(method))
                {
                    continue;
                }

                if (!MethodCallsCameraAim(method))
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
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            var methods = new System.Reflection.MethodBase?[]
            {
                AccessTools.DeclaredMethod(typeof(EnemyHeartHugger), "JumpScareAtChompStartForceLookAtHead"),
                AccessTools.DeclaredMethod(typeof(EnemyThinManAnim), "Scream"),
                AccessTools.DeclaredMethod(typeof(EnemySlowMouthAttaching), "Attach"),
                AccessTools.DeclaredMethod(typeof(EnemyOogly), "Update"),
                AccessTools.DeclaredMethod(typeof(EnemyCeilingEye), "Logic"),
                AccessTools.DeclaredMethod(typeof(EnemyCeilingEye), "StateAttack"),
                AccessTools.DeclaredMethod(typeof(EnemySpinny), "OverrideTargetPlayerCameraAim"),
                AccessTools.DeclaredMethod(typeof(EnemyUpscream), "Update")
            };

            for (var i = 0; i < methods.Length; i++)
            {
                if (methods[i] != null)
                {
                    yield return methods[i]!;
                }
            }
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> ReplaceCameraAimCalls(IEnumerable<CodeInstruction> instructions)
        {
            if (s_aimTargetSoftSetVanilla == null || s_aimTargetSetVanilla == null || s_aimTargetSoftSetProxy == null || s_aimTargetSetProxy == null)
            {
                return instructions;
            }

            var list = new List<CodeInstruction>(instructions);
            for (var i = 0; i < list.Count; i++)
            {
                var ins = list[i];
                if ((ins.opcode != System.Reflection.Emit.OpCodes.Call && ins.opcode != System.Reflection.Emit.OpCodes.Callvirt) || ins.operand is not System.Reflection.MethodInfo called)
                {
                    continue;
                }

                if (called == s_aimTargetSoftSetVanilla)
                {
                    ins.opcode = System.Reflection.Emit.OpCodes.Call;
                    ins.operand = s_aimTargetSoftSetProxy;
                    continue;
                }

                if (called == s_aimTargetSetVanilla)
                {
                    ins.opcode = System.Reflection.Emit.OpCodes.Call;
                    ins.operand = s_aimTargetSetProxy;
                }
            }

            return list;
        }

        private static bool MethodCallsCameraAim(System.Reflection.MethodBase method)
        {
            if (s_aimTargetSoftSetVanilla == null || s_aimTargetSetVanilla == null)
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
                        (called == s_aimTargetSoftSetVanilla || called == s_aimTargetSetVanilla))
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

        internal static void AimTargetSoftSetLastChanceAware(CameraAim? cameraAim, Vector3 position, float inSpeed, float outSpeed, float strengthNoAim, GameObject source, int prio)
        {
            var target = cameraAim ?? CameraAim.Instance;
            if (target == null)
            {
                return;
            }

            if (!IsLastChanceCameraContextActive())
            {
                target.AimTargetSoftSet(position, inSpeed, outSpeed, strengthNoAim, source, prio);
                return;
            }

            if (!ShouldApplyCameraForce(source))
            {
                return;
            }

            TryForceSpectateAimTo(position, source);
            target.AimTargetSoftSet(position, inSpeed, outSpeed, strengthNoAim, source, prio);
        }

        internal static void AimTargetSetLastChanceAware(CameraAim? cameraAim, Vector3 position, float inSpeed, float outSpeed, GameObject source, int prio)
        {
            var target = cameraAim ?? CameraAim.Instance;
            if (target == null)
            {
                return;
            }

            if (!IsLastChanceCameraContextActive())
            {
                target.AimTargetSet(position, inSpeed, outSpeed, source, prio);
                return;
            }

            if (!ShouldApplyCameraForce(source))
            {
                return;
            }

            TryForceSpectateAimTo(position, source);
            target.AimTargetSet(position, inSpeed, outSpeed, source, prio);
        }

        private static bool IsLastChanceCameraContextActive()
        {
            if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
            {
                return false;
            }

            return LastChanceMonstersTargetProxyHelper.IsHeadProxyActive(PlayerAvatar.instance);
        }

        private static bool ShouldApplyCameraForce(GameObject? source)
        {
            if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
            {
                return false;
            }

            var local = PlayerAvatar.instance;
            if (!LastChanceMonstersTargetProxyHelper.IsHeadProxyActive(local))
            {
                return false;
            }

            var now = Time.unscaledTime;
            var key = source != null ? source.GetInstanceID() : 0;
            if (!LastChanceMonstersCeilingEyeLockCoordinator.CanForceCamera(local, now, out var reason))
            {
                DebugDecision(source, key, reason, now, false);
                return false;
            }

            // Gameplay stays active regardless; this only controls camera forcing.
            var allow = InternalConfig.LastChanceMonstersForceCameraOnLock;
            DebugDecision(source, key, allow ? "AllowForceCamera" : "ForceCameraDisabledByConfig", now, allow);
            return allow;
        }

        private static void DebugDecision(GameObject? source, int key, string reason, float now, bool decision)
        {
            if (!InternalDebugFlags.DebugLastChanceCeilingEyeFlow || !LogLimiter.ShouldLog($"CeilingEye.CameraForce.{reason}.{key}", 90))
            {
                return;
            }

            var sourceName = source != null ? source.name : "null-source";
            Log.LogInfo(
                $"[CeilingEye][CameraForce][{reason}] source='{sourceName}' key={key} decision={decision} " +
                $"now={now:F2} cfgForce={InternalConfig.LastChanceMonstersForceCameraOnLock}");
        }

        private static void TryForceSpectateAimTo(Vector3 targetPosition, GameObject? source)
        {
            var spectate = SpectateCamera.instance;
            if (spectate == null)
            {
                return;
            }

            var local = PlayerAvatar.instance;
            var spectated = spectate.player;
            if (local == null || spectated == null || !ReferenceEquals(local, spectated))
            {
                return;
            }

            var pivot = spectate.transform;
            var direction = targetPosition - pivot.position;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            var flat = new Vector2(direction.x, direction.z).magnitude;
            var pitch = -Mathf.Atan2(direction.y, Mathf.Max(0.0001f, flat)) * Mathf.Rad2Deg;

            spectate.normalAimHorizontal = yaw;
            spectate.normalAimVertical = Mathf.Clamp(pitch, -80f, 80f);

            if (InternalDebugFlags.DebugLastChanceCeilingEyeFlow && LogLimiter.ShouldLog("CeilingEye.SpectateBridge", 90))
            {
                Log.LogInfo($"[CeilingEye][SpectateBridge] source='{(source != null ? source.name : "null-source")}' yaw={yaw:F1} pitch={pitch:F1} target={targetPosition}");
            }
        }

        internal static void ResetRuntimeState()
        {
            LastChanceMonstersCeilingEyeLockCoordinator.ResetRuntimeState();
        }
    }
}


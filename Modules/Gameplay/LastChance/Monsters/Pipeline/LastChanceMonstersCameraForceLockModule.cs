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
    internal static class LastChanceMonstersCameraForceLockModule
    {
        private const string PatchId = "DHHFLastChanceMode.Gameplay.LastChance.MonstersCameraForceLock";
        private static readonly ManualLogSource Log = Logger.CreateLogSource("DHHFLastChanceMode.LastChance.CeilingEye");
        private static readonly HashSet<MethodBase> s_patchedMethods = new();
        private static Harmony? s_harmony;

        private static readonly MethodInfo? s_aimTargetSoftSetVanilla =
            AccessTools.Method(typeof(CameraAim), "AimTargetSoftSet", new[] { typeof(Vector3), typeof(float), typeof(float), typeof(float), typeof(GameObject), typeof(int) });

        private static readonly MethodInfo? s_aimTargetSetVanilla =
            AccessTools.Method(typeof(CameraAim), "AimTargetSet", new[] { typeof(Vector3), typeof(float), typeof(float), typeof(GameObject), typeof(int) });

        private static readonly MethodInfo? s_aimTargetSoftSetProxy =
            AccessTools.Method(typeof(LastChanceMonstersCameraForceLockModule), nameof(AimTargetSoftSetLastChanceAware));

        private static readonly MethodInfo? s_aimTargetSetProxy =
            AccessTools.Method(typeof(LastChanceMonstersCameraForceLockModule), nameof(AimTargetSetLastChanceAware));
        private static readonly FieldInfo? s_spectatePlayerField = AccessTools.Field(typeof(SpectateCamera), "player");
        private static readonly FieldInfo? s_normalAimHorizontalField = AccessTools.Field(typeof(SpectateCamera), "normalAimHorizontal");
        private static readonly FieldInfo? s_normalAimVerticalField = AccessTools.Field(typeof(SpectateCamera), "normalAimVertical");

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

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            for (var i = 0; i < types.Length; i++)
            {
                var type = types[i];
                if (type == null || type.Name.IndexOf("Enemy", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                var typeMethods = type.GetMethods(flags);
                for (var m = 0; m < typeMethods.Length; m++)
                {
                    var method = typeMethods[m];
                    if (method == null || method.IsAbstract || method.GetMethodBody() == null)
                    {
                        continue;
                    }

                    if (MethodCallsCameraAim(method))
                    {
                        methods.Add(method);
                    }
                }
            }

            return methods;
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
                if ((ins.opcode != OpCodes.Call && ins.opcode != OpCodes.Callvirt) || ins.operand is not MethodInfo called)
                {
                    continue;
                }

                if (called == s_aimTargetSoftSetVanilla)
                {
                    ins.opcode = OpCodes.Call;
                    ins.operand = s_aimTargetSoftSetProxy;
                    continue;
                }

                if (called == s_aimTargetSetVanilla)
                {
                    ins.opcode = OpCodes.Call;
                    ins.operand = s_aimTargetSetProxy;
                }
            }

            return list;
        }

        private static bool MethodCallsCameraAim(MethodBase method)
        {
            if (method == null || s_aimTargetSoftSetVanilla == null || s_aimTargetSetVanilla == null)
            {
                return false;
            }

            try
            {
                var il = method.GetMethodBody()?.GetILAsByteArray();
                if (il == null || il.Length < 5)
                {
                    return false;
                }

                var softToken = s_aimTargetSoftSetVanilla.MetadataToken;
                var setToken = s_aimTargetSetVanilla.MetadataToken;
                for (var i = 0; i <= il.Length - 5; i++)
                {
                    var op = il[i];
                    if (op != 0x28 && op != 0x6F)
                    {
                        continue;
                    }

                    var token = BitConverter.ToInt32(il, i + 1);
                    if (token == softToken || token == setToken)
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
            if (spectate == null || s_spectatePlayerField == null || s_normalAimHorizontalField == null || s_normalAimVerticalField == null)
            {
                return;
            }

            var local = PlayerAvatar.instance;
            var spectated = s_spectatePlayerField.GetValue(spectate) as PlayerAvatar;
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

            s_normalAimHorizontalField.SetValue(spectate, yaw);
            s_normalAimVerticalField.SetValue(spectate, Mathf.Clamp(pitch, -80f, 80f));

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


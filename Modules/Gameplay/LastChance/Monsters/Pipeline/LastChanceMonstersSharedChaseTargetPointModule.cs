#nullable enable

using System.Collections.Generic;
using BepInEx.Logging;
using DHHFLastChanceMode.Modules.Config;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Adapters;
using DHHFLastChanceMode.Modules.Utilities;
using HarmonyLib;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Pipeline
{
    [HarmonyPatch]
    internal static class LastChanceMonstersSharedChaseTargetPointModule
    {
        private static readonly ManualLogSource Log = Logger.CreateLogSource("DHHFLastChanceMode.LastChance.Headman");
        private static readonly System.Reflection.MethodInfo? s_transformGetPositionMethod =
            AccessTools.PropertyGetter(typeof(Transform), nameof(Transform.position));

        private static readonly System.Reflection.MethodInfo? s_effectiveTransformPositionMethod =
            AccessTools.Method(typeof(LastChanceMonstersSharedChaseTargetPointModule), nameof(GetEffectiveTransformPosition));

        [HarmonyTargetMethods]
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            var methods = new List<System.Reflection.MethodBase>();

            AddIfFound(methods, typeof(EnemyStateChase), nameof(EnemyStateChase.Update));
            AddIfFound(methods, typeof(EnemyStateChaseBegin), nameof(EnemyStateChaseBegin.Update));
            AddIfFound(methods, typeof(EnemyStateChaseSlow), nameof(EnemyStateChaseSlow.Update));

            return methods;
        }

        [HarmonyPrepare]
        private static bool Prepare()
        {
            return s_transformGetPositionMethod != null && s_effectiveTransformPositionMethod != null;
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> ReplaceTargetPositionReads(IEnumerable<CodeInstruction> instructions)
        {
            if (s_transformGetPositionMethod == null || s_effectiveTransformPositionMethod == null)
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

                if (called == s_transformGetPositionMethod)
                {
                    ins.opcode = System.Reflection.Emit.OpCodes.Call;
                    ins.operand = s_effectiveTransformPositionMethod;
                }
            }

            return list;
        }

        private static Vector3 GetEffectiveTransformPosition(Transform transform)
        {
            if (transform == null)
            {
                return Vector3.zero;
            }

            if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
            {
                return transform.position;
            }

            LastChanceMonstersTargetProxyHelper.TryResolvePlayerAvatarFromTransform(transform, out var player);
            if (player != null && LastChanceMonstersTargetProxyHelper.TryGetHeadProxyTarget(player, out var headCenter))
            {
                if (InternalDebugFlags.DebugLastChanceHeadmanFlow)
                {
                    var key = $"Headman.TargetPointProxy.{player.GetInstanceID()}";
                    if (InternalDebugFlags.DebugLastChanceHeadmanVerbose || LogLimiter.ShouldLog(key, 15))
                    {
                        Log.LogInfo(
                            $"[Headman][TargetPointProxy] runtime=True player={player.name} playerId={player.GetInstanceID()} " +
                            $"body={transform.position} head={headCenter}");
                    }
                }

                return headCenter;
            }

            return transform.position;
        }

        private static void AddIfFound(List<System.Reflection.MethodBase> methods, System.Type type, string methodName)
        {
            var method = AccessTools.DeclaredMethod(type, methodName);
            if (method != null)
            {
                methods.Add(method);
            }
        }
    }
}

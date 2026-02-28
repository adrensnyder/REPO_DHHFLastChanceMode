#nullable enable

using System.Collections.Generic;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Support;
using HarmonyLib;
using UnityEngine;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Pipeline
{
    [HarmonyPatch]
    internal static class LastChanceMonstersSharedChaseTargetPointModule
    {
        private static readonly List<System.Reflection.MethodBase> s_targetMethods =
            LastChanceMonstersPatchTargetHelper.BuildTargetList(AddTargetMethods);

        private static readonly System.Reflection.MethodInfo? s_transformGetPositionMethod =
            AccessTools.PropertyGetter(typeof(Transform), nameof(Transform.position));

        private static readonly System.Reflection.MethodInfo? s_effectiveTransformPositionMethod =
            AccessTools.Method(typeof(LastChanceMonstersSharedChaseTargetPointModule), nameof(GetEffectiveTransformPosition));

        [HarmonyTargetMethods]
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            return s_targetMethods;
        }

        private static void AddTargetMethods(List<System.Reflection.MethodBase> methods)
        {
            LastChanceMonstersPatchTargetHelper.AddDeclaredMethod(methods, typeof(EnemyStateChase), nameof(EnemyStateChase.Update));
            LastChanceMonstersPatchTargetHelper.AddDeclaredMethod(methods, typeof(EnemyStateChaseBegin), nameof(EnemyStateChaseBegin.Update));
            LastChanceMonstersPatchTargetHelper.AddDeclaredMethod(methods, typeof(EnemyStateChaseSlow), nameof(EnemyStateChaseSlow.Update));
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
            return LastChanceMonstersTargetingOrchestrator.ResolveEffectiveTransformTargetPoint(transform);
        }

    }
}

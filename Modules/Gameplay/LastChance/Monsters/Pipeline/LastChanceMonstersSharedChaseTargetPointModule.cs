#nullable enable

using System.Collections.Generic;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Adapters;
using HarmonyLib;
using UnityEngine;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Pipeline
{
    [HarmonyPatch]
    internal static class LastChanceMonstersSharedChaseTargetPointModule
    {
        private static readonly System.Reflection.MethodBase? s_enemyStateChaseUpdate =
            AccessTools.DeclaredMethod(typeof(EnemyStateChase), nameof(EnemyStateChase.Update));

        private static readonly System.Reflection.MethodBase? s_enemyStateChaseBeginUpdate =
            AccessTools.DeclaredMethod(typeof(EnemyStateChaseBegin), nameof(EnemyStateChaseBegin.Update));

        private static readonly System.Reflection.MethodBase? s_enemyStateChaseSlowUpdate =
            AccessTools.DeclaredMethod(typeof(EnemyStateChaseSlow), nameof(EnemyStateChaseSlow.Update));

        private static readonly System.Reflection.MethodInfo? s_transformGetPositionMethod =
            AccessTools.PropertyGetter(typeof(Transform), nameof(Transform.position));

        private static readonly System.Reflection.MethodInfo? s_effectiveTransformPositionMethod =
            AccessTools.Method(typeof(LastChanceMonstersSharedChaseTargetPointModule), nameof(GetEffectiveTransformPosition));

        [HarmonyTargetMethods]
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            if (s_enemyStateChaseUpdate != null)
            {
                yield return s_enemyStateChaseUpdate;
            }

            if (s_enemyStateChaseBeginUpdate != null)
            {
                yield return s_enemyStateChaseBeginUpdate;
            }

            if (s_enemyStateChaseSlowUpdate != null)
            {
                yield return s_enemyStateChaseSlowUpdate;
            }
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
            return LastChanceMonstersTargetProxyHelper.ResolveEffectiveTransformTargetPosition(transform);
        }

    }
}

#nullable enable

using System.Collections.Generic;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Adapters;
using HarmonyLib;
using UnityEngine;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Interactions
{
    [HarmonyPatch]
    internal static class LastChanceMonstersCarryTargetPositionModule
    {
        private static readonly System.Reflection.FieldInfo? s_enemyHiddenPlayerTargetField =
            AccessTools.Field(typeof(EnemyHidden), nameof(EnemyHidden.playerTarget));

        private static readonly System.Reflection.MethodInfo? s_componentGetTransform =
            AccessTools.PropertyGetter(typeof(Component), nameof(Component.transform));

        private static readonly System.Reflection.MethodInfo? s_transformGetPosition =
            AccessTools.PropertyGetter(typeof(Transform), nameof(Transform.position));

        private static readonly System.Reflection.MethodInfo? s_getEffectiveTargetPosition =
            AccessTools.Method(typeof(LastChanceMonstersCarryTargetPositionModule), nameof(GetEffectivePlayerTargetPosition));

        [HarmonyTargetMethods]
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            var methods = new System.Reflection.MethodBase?[]
            {
                AccessTools.DeclaredMethod(typeof(EnemyHidden), nameof(EnemyHidden.StatePlayerGoTo)),
                AccessTools.DeclaredMethod(typeof(EnemyHidden), nameof(EnemyHidden.StatePlayerMove)),
                AccessTools.DeclaredMethod(typeof(EnemyHidden), nameof(EnemyHidden.StatePlayerRelease)),
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
        private static IEnumerable<CodeInstruction> ReplacePlayerTargetPositionReads(System.Reflection.MethodBase __originalMethod, IEnumerable<CodeInstruction> instructions)
        {
            if (__originalMethod == null || s_componentGetTransform == null || s_transformGetPosition == null || s_getEffectiveTargetPosition == null)
            {
                return instructions;
            }

            if (s_enemyHiddenPlayerTargetField == null)
            {
                return instructions;
            }

            var list = new List<CodeInstruction>(instructions);
            for (var i = 0; i <= list.Count - 3; i++)
            {
                var a = list[i];
                var b = list[i + 1];
                var c = list[i + 2];

                if ((a.opcode != System.Reflection.Emit.OpCodes.Ldfld && a.opcode != System.Reflection.Emit.OpCodes.Ldflda) ||
                    a.operand is not System.Reflection.FieldInfo loadedField ||
                    loadedField != s_enemyHiddenPlayerTargetField)
                {
                    continue;
                }

                if (!CallsMethod(b, s_componentGetTransform))
                {
                    continue;
                }

                if (!CallsMethod(c, s_transformGetPosition))
                {
                    continue;
                }

                b.opcode = System.Reflection.Emit.OpCodes.Nop;
                b.operand = null;
                c.opcode = System.Reflection.Emit.OpCodes.Call;
                c.operand = s_getEffectiveTargetPosition;
            }

            return list;
        }

        private static bool CallsMethod(CodeInstruction instruction, System.Reflection.MethodInfo target)
        {
            return (instruction.opcode == System.Reflection.Emit.OpCodes.Call || instruction.opcode == System.Reflection.Emit.OpCodes.Callvirt) &&
                   instruction.operand is System.Reflection.MethodInfo called &&
                   called == target;
        }

        internal static Vector3 GetEffectivePlayerTargetPosition(PlayerAvatar? player)
        {
            if (player == null)
            {
                return Vector3.zero;
            }

            if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
            {
                return player.transform.position;
            }

            if (LastChanceMonstersTargetProxyHelper.TryGetHeadProxyTarget(player, out var headCenter))
            {
                return headCenter;
            }

            return player.transform.position;
        }
    }
}

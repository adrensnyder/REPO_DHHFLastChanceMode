#nullable enable

using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Adapters;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Interactions
{
    [HarmonyPatch]
    internal static class LastChanceMonstersGasVictimPositionModule
    {
        private static readonly System.Reflection.MethodBase? s_playersInGasLogicMethod =
            AccessTools.DeclaredMethod(typeof(EnemyHeartHugger), nameof(EnemyHeartHugger.PlayersInGasLogic));

        private static readonly System.Reflection.MethodBase? s_playerInGasMethod =
            AccessTools.DeclaredMethod(typeof(EnemyHeartHugger), nameof(EnemyHeartHugger.PlayerInGas), new[] { typeof(PlayerAvatar) });

        private static readonly System.Reflection.MethodInfo? s_componentGetTransform =
            AccessTools.PropertyGetter(typeof(Component), nameof(Component.transform));

        private static readonly System.Reflection.MethodInfo? s_transformGetPosition =
            AccessTools.PropertyGetter(typeof(Transform), nameof(Transform.position));

        private static readonly System.Reflection.MethodInfo? s_getEffectivePosition =
            AccessTools.Method(typeof(LastChanceMonstersGasVictimPositionModule), nameof(GetEffectivePlayerPosition));

        [HarmonyTargetMethods]
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            if (s_playersInGasLogicMethod != null)
            {
                yield return s_playersInGasLogicMethod;
            }

            if (s_playerInGasMethod != null)
            {
                yield return s_playerInGasMethod;
            }
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            if (s_componentGetTransform == null || s_transformGetPosition == null || s_getEffectivePosition == null)
            {
                return instructions;
            }

            var list = new List<CodeInstruction>(instructions);
            for (var i = 0; i <= list.Count - 2; i++)
            {
                var a = list[i];
                var b = list[i + 1];
                if (!CallsMethod(a, s_componentGetTransform) || !CallsMethod(b, s_transformGetPosition))
                {
                    continue;
                }

                a.opcode = System.Reflection.Emit.OpCodes.Nop;
                a.operand = null;
                b.opcode = System.Reflection.Emit.OpCodes.Call;
                b.operand = s_getEffectivePosition;
            }

            return list;
        }

        private static bool CallsMethod(CodeInstruction instruction, System.Reflection.MethodInfo method)
        {
            return (instruction.opcode == System.Reflection.Emit.OpCodes.Call || instruction.opcode == System.Reflection.Emit.OpCodes.Callvirt) &&
                   instruction.operand is System.Reflection.MethodInfo called &&
                   called == method;
        }

        internal static Vector3 GetEffectivePlayerPosition(PlayerAvatar? player)
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

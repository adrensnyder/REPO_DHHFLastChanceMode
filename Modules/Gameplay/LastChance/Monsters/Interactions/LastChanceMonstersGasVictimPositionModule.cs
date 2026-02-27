#nullable enable

using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Adapters;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Interactions
{
    [HarmonyPatch]
    internal static class LastChanceMonstersGasVictimPositionModule
    {
        private static readonly MethodInfo? s_componentGetTransform =
            AccessTools.PropertyGetter(typeof(Component), nameof(Component.transform));

        private static readonly MethodInfo? s_transformGetPosition =
            AccessTools.PropertyGetter(typeof(Transform), nameof(Transform.position));

        private static readonly MethodInfo? s_getEffectivePosition =
            AccessTools.Method(typeof(LastChanceMonstersGasVictimPositionModule), nameof(GetEffectivePlayerPosition));

        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.DeclaredMethod(typeof(EnemyHeartHugger), "PlayersInGasLogic");
            yield return AccessTools.DeclaredMethod(typeof(EnemyHeartHugger), "PlayerInGas", new[] { typeof(PlayerAvatar) });
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

                a.opcode = OpCodes.Nop;
                a.operand = null;
                b.opcode = OpCodes.Call;
                b.operand = s_getEffectivePosition;
            }

            return list;
        }

        private static bool CallsMethod(CodeInstruction instruction, MethodInfo method)
        {
            return (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) &&
                   instruction.operand is MethodInfo called &&
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

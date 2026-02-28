#nullable enable

using System;
using System.Collections.Generic;
using HarmonyLib;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Interactions
{
    internal static class LastChanceMonstersVisualCouplingDecoupleModule
    {
        private static readonly System.Reflection.FieldInfo? s_tumbleWingPinkTimerField =
            AccessTools.Field(typeof(ItemUpgradePlayerTumbleWingsLogic), nameof(ItemUpgradePlayerTumbleWingsLogic.tumbleWingPinkTimer));

        private static readonly System.Reflection.MethodInfo? s_upgradeTumbleWingsVisualsActiveMethod =
            AccessTools.Method(typeof(PlayerAvatar), nameof(PlayerAvatar.UpgradeTumbleWingsVisualsActive), new[] { typeof(bool), typeof(bool) });

        private static readonly System.Reflection.MethodInfo? s_setTumbleWingPinkTimerSafeMethod =
            AccessTools.Method(typeof(LastChanceMonstersVisualCouplingDecoupleModule), nameof(SetTumbleWingPinkTimerSafe));

        private static readonly System.Reflection.MethodInfo? s_upgradeTumbleWingsVisualsActiveSafeMethod =
            AccessTools.Method(typeof(LastChanceMonstersVisualCouplingDecoupleModule), nameof(UpgradeTumbleWingsVisualsActiveSafe));

        [HarmonyPatch(typeof(EnemyHeartHugger), "PlayersInGasLogic")]
        private static class EnemyHeartHuggerPlayersInGasLogicPatch
        {
            [HarmonyTranspiler]
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                return RewriteInstructions(instructions);
            }
        }

        [HarmonyPatch(typeof(EnemyHeartHuggerGasChecker), "Update")]
        private static class EnemyHeartHuggerGasCheckerUpdatePatch
        {
            [HarmonyTranspiler]
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                return RewriteInstructions(instructions);
            }
        }

        private static IEnumerable<CodeInstruction> RewriteInstructions(IEnumerable<CodeInstruction> instructions)
        {
            var list = new List<CodeInstruction>(instructions);

            if (s_tumbleWingPinkTimerField != null && s_setTumbleWingPinkTimerSafeMethod != null)
            {
                for (var i = 0; i < list.Count; i++)
                {
                    var ins = list[i];
                    if (ins.opcode == System.Reflection.Emit.OpCodes.Stfld &&
                        ins.operand is System.Reflection.FieldInfo field &&
                        field == s_tumbleWingPinkTimerField)
                    {
                        ins.opcode = System.Reflection.Emit.OpCodes.Call;
                        ins.operand = s_setTumbleWingPinkTimerSafeMethod;
                    }
                }
            }

            if (s_upgradeTumbleWingsVisualsActiveMethod != null && s_upgradeTumbleWingsVisualsActiveSafeMethod != null)
            {
                for (var i = 0; i < list.Count; i++)
                {
                    var ins = list[i];
                    if ((ins.opcode == System.Reflection.Emit.OpCodes.Call || ins.opcode == System.Reflection.Emit.OpCodes.Callvirt) &&
                        ins.operand is System.Reflection.MethodInfo method &&
                        method == s_upgradeTumbleWingsVisualsActiveMethod)
                    {
                        ins.opcode = System.Reflection.Emit.OpCodes.Call;
                        ins.operand = s_upgradeTumbleWingsVisualsActiveSafeMethod;
                    }
                }
            }

            return list;
        }

        private static void SetTumbleWingPinkTimerSafe(ItemUpgradePlayerTumbleWingsLogic? logic, float value)
        {
            if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
            {
                if (logic == null)
                {
                    throw new System.NullReferenceException("upgradeTumbleWingsLogic is null");
                }

                logic.tumbleWingPinkTimer = value;
                return;
            }

            if (logic == null)
            {
                return;
            }

            logic.tumbleWingPinkTimer = value;
        }

        private static void UpgradeTumbleWingsVisualsActiveSafe(PlayerAvatar player, bool visualsActive, bool pink)
        {
            if (player == null)
            {
                return;
            }

            if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
            {
                player.UpgradeTumbleWingsVisualsActive(visualsActive, pink);
                return;
            }

            if (player.upgradeTumbleWingsLogic == null)
            {
                return;
            }

            player.UpgradeTumbleWingsVisualsActive(visualsActive, pink);
        }
    }
}


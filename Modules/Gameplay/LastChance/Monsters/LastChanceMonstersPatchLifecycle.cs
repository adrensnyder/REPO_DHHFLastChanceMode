#nullable enable

using System.Reflection;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Interactions;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Pipeline;
using HarmonyLib;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters
{
    internal static class LastChanceMonstersPatchLifecycle
    {
        private static bool s_pipelineApplied;

        internal static void ReconcilePipeline(bool enable, Harmony harmony, Assembly asm)
        {
            if (enable)
            {
                ApplyPipeline(harmony, asm);
                return;
            }

            UnapplyPipeline();
        }

        internal static void ApplyPipeline(Harmony harmony, Assembly asm)
        {
            if (s_pipelineApplied)
            {
                return;
            }

            LastChanceMonstersSearchModule.Apply(harmony, asm);
            LastChanceMonstersNoiseAggroModule.Apply(harmony, asm);
            LastChanceMonstersPlayerVisionCheckModule.Apply();
            LastChanceMonstersCameraForceLockModule.Apply();
            s_pipelineApplied = true;
        }

        internal static void UnapplyPipeline()
        {
            if (!s_pipelineApplied)
            {
                return;
            }

            LastChanceMonstersCameraForceLockModule.Unapply();
            LastChanceMonstersPlayerVisionCheckModule.Unapply();
            LastChanceMonstersNoiseAggroModule.Unapply();
            LastChanceMonstersSearchModule.Unapply();
            s_pipelineApplied = false;
        }
    }
}

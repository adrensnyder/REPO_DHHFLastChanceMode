#nullable enable

using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Interactions;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Pipeline;
using HarmonyLib;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters
{
    internal static class LastChanceMonstersPatchLifecycle
    {
        private static bool s_pipelineApplied;

        internal static void ReconcilePipeline(bool enable, Harmony harmony)
        {
            if (enable)
            {
                ApplyPipeline(harmony);
                return;
            }

            UnapplyPipeline();
        }

        internal static void ApplyPipeline(Harmony harmony)
        {
            if (s_pipelineApplied)
            {
                return;
            }

            LastChanceMonstersSearchModule.Apply(harmony);
            LastChanceMonstersNoiseAggroModule.Apply(harmony);
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
            LastChanceMonstersAnimalHeadVisionFallbackModule.ResetRuntimeState();
            LastChanceMonstersCarryProxyModule.ResetRuntimeState();
            LastChanceMonstersOnScreenCameraModule.ResetRuntimeState();
            LastChanceMonstersThinManStandModule.ResetRuntimeState();
            s_pipelineApplied = false;
        }
    }
}

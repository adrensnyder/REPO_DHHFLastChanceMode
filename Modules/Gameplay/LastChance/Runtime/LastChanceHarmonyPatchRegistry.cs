#nullable enable

using System;
using BepInEx.Logging;
using DHHFLastChanceMode.Modules.Config;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Guards;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Adapters;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Interactions;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Pipeline;
using HarmonyLib;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Runtime
{
    internal static class LastChanceHarmonyPatchRegistry
    {
        private static readonly Type[] PatchTypes =
        {
            typeof(CompatibilityGateStartHookPatch),
            typeof(StatsManagerSaveFileDeleteLastChancePatch),
            typeof(MenuPageSavesOnDeleteGameLastChancePatch),
            typeof(LastChanceHeadEyesOverrideBypassModule),
            typeof(LastChanceHeadPupilVisualModule),
            typeof(RunManagerUpdateLastChanceTimerPatch),

            typeof(LastChanceMonstersVoiceEnemyOnlyModule),
            typeof(LastChanceMonstersBodyPositionProxyModule),
            typeof(LastChanceMonstersVisionModule),
            typeof(LastChanceMonstersHurtColliderHeadProxyModule),
            typeof(LastChanceMonstersOnScreenCameraModule),
            typeof(LastChanceMonstersOnScreenSafeLookupPatch),
            typeof(LastChanceMonstersTriggerAttackModule),
            typeof(LastChanceMonstersVisualCouplingDecoupleModule.EnemyHeartHuggerPlayersInGasLogicPatch),
            typeof(LastChanceMonstersVisualCouplingDecoupleModule.EnemyHeartHuggerGasCheckerUpdatePatch),
            typeof(LastChanceMonstersHeadPlayerProxyColliderModule),
            typeof(LastChanceMonstersGasGuiderHeadProxyModule),
            typeof(LastChanceMonstersAnimalWreakHavocHeadRoomProxyModule),
            typeof(LastChanceMonstersThinManStandModule),
            typeof(LastChanceMonstersSharedPlayerSearchModule),
            typeof(LastChanceMonstersEffectiveTargetPointModule),
            typeof(LastChanceMonstersChaseNavmeshProxyModule),
            typeof(LastChanceMonstersDeathTimerBonusModule),
            typeof(LastChanceMonstersGasCaptureModule),
            typeof(LastChanceMonstersAnimalHeadVisionFallbackModule),
            typeof(LastChanceMonstersSharedChaseTargetPointModule),
            typeof(LastChanceMonstersGasVictimPositionModule),
            typeof(LastChanceMonstersCarryTargetPositionModule),
            typeof(LastChanceMonstersHiddenDestinationModule),
            typeof(LastChanceMonstersCarryProxyModule),
            typeof(LastChanceMonstersPathBlockingModule),
            typeof(LastChanceMonstersSpinnyLockBridgeModule),
            typeof(LastChanceMonstersBeamerHeadAimModule),
            typeof(LastChanceMonstersCameraForceLockModule)
        };

        internal static void ApplyAll(Harmony harmony, ManualLogSource? log)
        {
            if (harmony == null)
            {
                return;
            }

            var patchedCount = 0;
            for (var i = 0; i < PatchTypes.Length; i++)
            {
                var type = PatchTypes[i];
                try
                {
                    var patched = harmony.CreateClassProcessor(type).Patch();
                    patchedCount += patched?.Count ?? 0;
                }
                catch (Exception ex)
                {
                    log?.LogWarning($"[LastChance] Patch registry failed for {type.FullName}: {ex.GetType().Name}");
                }
            }

            if (FeatureFlags.DebugLogging)
            {
                log?.LogInfo($"[LastChance] Explicit patch registry applied. types={PatchTypes.Length} methods={patchedCount}.");
            }
        }
    }
}

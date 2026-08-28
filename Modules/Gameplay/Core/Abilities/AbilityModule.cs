#nullable enable

using DeathHeadHopperFix.API.Abilities;
using DHHFLastChanceMode.Modules.Config;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Runtime;
using DHHFLastChanceMode.Modules.Utilities;
using UnityEngine;

namespace DHHFLastChanceMode.Modules.Gameplay.Core.Abilities
{
    public static class AbilityModule
    {
        private const string DirectionOwnerId = "AdrenSnyder.DHHFLastChanceMode.Direction";
        private const string DirectionAbilityName = "DirectionAbility";
        private const float AssetRetryIntervalSeconds = 2f;

        private static Sprite? s_directionSprite;
        private static float s_nextAssetRetryAt;
        private static float s_activationProgress01;
        private static bool s_registered;
        private static bool s_registrationAttemptedForVisibleCycle;

        internal static void RefreshDirectionSlotVisuals()
        {
            var visible = LastChanceTimerController.IsDirectionIndicatorUiVisible &&
                          LastChanceRuntimeOrchestrator.IsRuntimeActive;
            if (!visible)
            {
                ReleaseDirectionSlot();
                return;
            }

            EnsureDirectionSpriteLoaded();
            EnsureRegistered();
            PublishDirectionState();
        }

        internal static void SetDirectionSlotActivationProgress(float progress01)
        {
            s_activationProgress01 = Mathf.Clamp01(progress01);
            if (s_registered)
            {
                PublishDirectionState();
            }
        }

        internal static void TriggerDirectionSlotCooldown(float cooldownSeconds)
        {
            s_activationProgress01 = 0f;
            if (!s_registered)
            {
                return;
            }

            AbilitySlotOrchestrator.TryUpdate(DirectionOwnerId, BuildState());
            AbilitySlotOrchestrator.TryStartCooldown(DirectionOwnerId, Mathf.Max(0f, cooldownSeconds));
        }

        internal static void ReleaseDirectionSlot()
        {
            s_activationProgress01 = 0f;
            if (s_registered)
            {
                AbilitySlotOrchestrator.Unregister(DirectionOwnerId);
            }

            s_registered = false;
            s_registrationAttemptedForVisibleCycle = false;
        }

        private static void EnsureRegistered()
        {
            if (s_registered || s_registrationAttemptedForVisibleCycle)
            {
                return;
            }

            s_registrationAttemptedForVisibleCycle = true;
            var registration = new AbilitySlotRegistration
            {
                OwnerId = DirectionOwnerId,
                Slot = ExtensibleAbilitySlot.Slot2,
                AbilityName = DirectionAbilityName,
                Icon = s_directionSprite,
                OnDown = LastChanceTimerController.OnDirectionAbilityInputDown,
                OnHold = LastChanceTimerController.OnDirectionAbilityInputHold,
                OnUp = LastChanceTimerController.OnDirectionAbilityInputUp,
                OnCancel = LastChanceTimerController.OnDirectionAbilityInputCancel
            };

            s_registered = AbilitySlotOrchestrator.TryRegister(registration);
        }

        private static void PublishDirectionState()
        {
            if (!s_registered)
            {
                return;
            }

            AbilitySlotOrchestrator.TryUpdate(DirectionOwnerId, BuildState());
        }

        private static AbilitySlotState BuildState()
        {
            var penaltySeconds = Mathf.Max(0f, LastChanceTimerController.GetDirectionIndicatorPenaltySecondsPreview());
            var seconds = Mathf.RoundToInt(penaltySeconds);
            return new AbilitySlotState
            {
                Visible = LastChanceTimerController.IsDirectionIndicatorUiVisible && LastChanceRuntimeOrchestrator.IsRuntimeActive,
                Available = LastChanceTimerController.IsDirectionIndicatorEnergySufficientPreview(),
                ActivationProgress01 = s_activationProgress01,
                Label = $"{seconds}s",
                Icon = s_directionSprite
            };
        }

        private static void EnsureDirectionSpriteLoaded()
        {
            if (s_directionSprite != null || Time.unscaledTime < s_nextAssetRetryAt)
            {
                return;
            }

            s_nextAssetRetryAt = Time.unscaledTime + AssetRetryIntervalSeconds;
            ImageAssetLoader.TryLoadSprite(
                "Direction.png",
                ImageAssetLoader.GetDefaultAssetsDirectory(),
                out s_directionSprite,
                out _);
        }
    }
}

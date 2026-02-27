#nullable enable

using System;
using DeathHeadHopper.Abilities;
using DeathHeadHopper.UI;
using DeathHeadHopperFix.Modules.Gameplay.Spectate;
using DHHFLastChanceMode.Modules.Config;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Runtime;
using DHHFLastChanceMode.Modules.Utilities;
using UnityEngine;
using DhhFixAbilityModule = DeathHeadHopperFix.Modules.Gameplay.Core.Abilities.AbilityModule;

namespace DHHFLastChanceMode.Modules.Gameplay.Core.Abilities
{
    public static class AbilityModule
    {
        private const int DirectionIndicatorSlotIndex = 1;
        private const string AbilityBarDemandSourceId = "DHHFLastChanceMode.Direction";
        private static Sprite? s_directionSprite;
        private static float s_nextDirectionIconFallbackApplyAt;
        private static DirectionIndicatorAbility? s_directionAbility;

        internal static void RefreshDirectionSlotVisuals()
        {
            EnsureDirectionAbilitySlotState();
            PushDirectionAbilityBarDemand();
            DhhFixAbilityModule.RefreshDirectionSlotVisuals();
            TryApplyDirectionIconFallback();
        }

        internal static void SetDirectionSlotActivationProgress(float progress01)
        {
            DhhFixAbilityModule.SetDirectionSlotActivationProgress(progress01);
        }

        internal static void TriggerDirectionSlotCooldown(float cooldownSeconds)
        {
            DhhFixAbilityModule.TriggerDirectionSlotCooldown(cooldownSeconds);
        }

        private static void PushDirectionAbilityBarDemand()
        {
            var visible = LastChanceTimerController.IsDirectionIndicatorUiVisible;
            try
            {
                AbilityBarVisibilityAnchor.SetExternalDemand(AbilityBarDemandSourceId, visible);
            }
            catch
            {
                // Keep LastChance flow resilient if DHHFix is not present or updated.
            }
        }

        private static void TryApplyDirectionIconFallback()
        {
            if (Time.unscaledTime < s_nextDirectionIconFallbackApplyAt)
            {
                return;
            }
            s_nextDirectionIconFallbackApplyAt = Time.unscaledTime + 0.5f;

            if (s_directionSprite == null &&
                !ImageAssetLoader.TryLoadSprite(
                    "Direction.png",
                    ImageAssetLoader.GetDefaultAssetsDirectory(),
                    out s_directionSprite,
                    out _))
            {
                return;
            }

            if (s_directionSprite == null)
            {
                return;
            }

            var spots = UnityEngine.Object.FindObjectsOfType<AbilitySpot>();
            if (spots == null || spots.Length == 0)
            {
                return;
            }

            for (var i = 0; i < spots.Length; i++)
            {
                var spot = spots[i];
                if (spot == null)
                {
                    continue;
                }

                if (spot.abilitySpotIndex != DirectionIndicatorSlotIndex)
                {
                    continue;
                }

                try
                {
                    ApplyDirectionIcon(spot, s_directionSprite);
                }
                catch
                {
                    // Ability UI can be rebuilt during scene transitions.
                }
            }
        }

        private static void EnsureDirectionAbilitySlotState()
        {
            var shouldBeVisible = LastChanceTimerController.IsDirectionIndicatorUiVisible;
            var spots = UnityEngine.Object.FindObjectsOfType<AbilitySpot>();
            if (spots == null || spots.Length == 0)
            {
                return;
            }

            AbilitySpot? directionSpot = null;
            for (var i = 0; i < spots.Length; i++)
            {
                var spot = spots[i];
                if (spot == null)
                {
                    continue;
                }

                if (GetSpotIndex(spot) == DirectionIndicatorSlotIndex)
                {
                    directionSpot = spot;
                    break;
                }
            }

            if (directionSpot == null)
            {
                return;
            }

            if (!shouldBeVisible)
            {
                if (s_directionAbility != null && ReferenceEquals(directionSpot.CurrentAbility, s_directionAbility))
                {
                    directionSpot.RemoveAbility();
                }

                return;
            }

            s_directionAbility ??= CreateDirectionAbilityInstance();
            if (s_directionAbility == null)
            {
                return;
            }

            if (s_directionSprite != null)
            {
                s_directionAbility.icon = s_directionSprite;
            }

            if (directionSpot.CurrentAbility == null)
            {
                directionSpot.EquipAbility(s_directionAbility);
                return;
            }

            if (ReferenceEquals(directionSpot.CurrentAbility, s_directionAbility))
            {
                return;
            }

            var hasDirectionElsewhere = false;
            for (var i = 0; i < spots.Length; i++)
            {
                var spot = spots[i];
                if (spot == null)
                {
                    continue;
                }

                if (ReferenceEquals(spot.CurrentAbility, s_directionAbility))
                {
                    hasDirectionElsewhere = true;
                    break;
                }
            }

            if (hasDirectionElsewhere && TryRemoveDirectionAbilityFromSpots(spots))
            {
                directionSpot.EquipAbility(s_directionAbility);
            }
        }

        private static bool TryRemoveDirectionAbilityFromSpots(AbilitySpot[] spots)
        {
            var removed = false;
            for (var i = 0; i < spots.Length; i++)
            {
                var spot = spots[i];
                if (spot == null)
                {
                    continue;
                }

                if (!ReferenceEquals(spot.CurrentAbility, s_directionAbility))
                {
                    continue;
                }

                try
                {
                    spot.RemoveAbility();
                    removed = true;
                }
                catch
                {
                    // Ignore transient UI teardown issues.
                }
            }

            return removed;
        }

        private static DirectionIndicatorAbility CreateDirectionAbilityInstance()
        {
            var ability = ScriptableObject.CreateInstance<DirectionIndicatorAbility>();
            if (s_directionSprite != null)
            {
                ability.icon = s_directionSprite;
            }
            return ability;
        }

        public static string DirectionAbility_GetName()
        {
            return "DirectionAbility";
        }

        public static float DirectionAbility_GetCooldown()
        {
            return Mathf.Max(1f, FeatureFlags.LastChanceIndicatorDirectionCooldownSeconds);
        }

        public static float DirectionAbility_GetEnergyCost()
        {
            return Mathf.Max(0f, LastChanceTimerController.GetDirectionIndicatorPenaltySecondsPreview());
        }

        public static int DirectionAbility_GetLevel()
        {
            return 1;
        }

        public static void DirectionAbility_OnDown()
        {
            LastChanceTimerController.OnDirectionAbilityInputDown();
        }

        public static void DirectionAbility_OnHold()
        {
            LastChanceTimerController.OnDirectionAbilityInputHold();
        }

        public static void DirectionAbility_OnUp()
        {
            LastChanceTimerController.OnDirectionAbilityInputUp();
        }

        public static void DirectionAbility_OnCancel()
        {
            LastChanceTimerController.OnDirectionAbilityInputCancel();
        }

        private static int GetSpotIndex(AbilitySpot spot)
        {
            return spot == null ? -1 : spot.abilitySpotIndex;
        }

        private static void ApplyDirectionIcon(AbilitySpot spot, Sprite sprite)
        {
            if (spot == null || sprite == null)
            {
                return;
            }

            spot.backgroundIcon.enabled = true;
            spot.backgroundIcon.sprite = sprite;
            spot.cooldownIcon.enabled = true;
            spot.cooldownIcon.sprite = sprite;
            spot.noAbility.enabled = false;
        }

        private sealed class DirectionIndicatorAbility : AbilityBase
        {
            public override string AbilityName => DirectionAbility_GetName();

            public override float Cooldown => DirectionAbility_GetCooldown();

            public override float EnergyCost => DirectionAbility_GetEnergyCost();

            public override int AbilityLevel => DirectionAbility_GetLevel();

            public override void OnAbilityDown()
            {
                DirectionAbility_OnDown();
            }

            public override void OnAbilityHold()
            {
                DirectionAbility_OnHold();
            }

            public override void OnAbilityUp()
            {
                DirectionAbility_OnUp();
            }

            public override void OnAbilityCancel()
            {
                DirectionAbility_OnCancel();
            }
        }
    }
}

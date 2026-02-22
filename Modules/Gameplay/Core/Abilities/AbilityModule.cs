#nullable enable

using System;
using System.Reflection;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Runtime;
using DHHFLastChanceMode.Modules.Utilities;
using UnityEngine;

namespace DHHFLastChanceMode.Modules.Gameplay.Core.Abilities
{
    internal static class AbilityModule
    {
        private const int DirectionIndicatorSlotIndex = 1;
        private const string AbilityBarDemandSourceId = "DHHFLastChanceMode.Direction";
        private static Type? s_runtimeType;
        private static MethodInfo? s_refreshDirectionSlotVisuals;
        private static MethodInfo? s_setDirectionSlotActivationProgress;
        private static MethodInfo? s_triggerDirectionSlotCooldown;
        private static MethodInfo? s_applyDirectionIconMethod;
        private static Type? s_abilityBarVisibilityAnchorType;
        private static MethodInfo? s_setExternalDemandMethod;
        private static Type? s_abilitySpotType;
        private static FieldInfo? s_abilitySpotIndexField;
        private static Sprite? s_directionSprite;
        private static float s_nextDirectionIconFallbackApplyAt;

        internal static void RefreshDirectionSlotVisuals()
        {
            EnsureResolved();
            PushDirectionAbilityBarDemand();
            s_refreshDirectionSlotVisuals?.Invoke(null, null);
            TryApplyDirectionIconFallback();
        }

        internal static void SetDirectionSlotActivationProgress(float progress01)
        {
            EnsureResolved();
            s_setDirectionSlotActivationProgress?.Invoke(null, new object[] { progress01 });
        }

        internal static void TriggerDirectionSlotCooldown(float cooldownSeconds)
        {
            EnsureResolved();
            s_triggerDirectionSlotCooldown?.Invoke(null, new object[] { cooldownSeconds });
        }

        private static void EnsureResolved()
        {
            if (s_runtimeType == null)
            {
                s_runtimeType = ResolveType();
            }

            if (s_runtimeType == null)
            {
                return;
            }

            s_refreshDirectionSlotVisuals ??= s_runtimeType.GetMethod("RefreshDirectionSlotVisuals", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            s_setDirectionSlotActivationProgress ??= s_runtimeType.GetMethod("SetDirectionSlotActivationProgress", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            s_triggerDirectionSlotCooldown ??= s_runtimeType.GetMethod("TriggerDirectionSlotCooldown", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            if (s_applyDirectionIconMethod == null)
            {
                var slotVisualType = s_runtimeType.GetNestedType("SlotVisualOverrides", BindingFlags.NonPublic);
                s_applyDirectionIconMethod = slotVisualType?.GetMethod("ApplyDirectionIcon", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            }

            s_abilityBarVisibilityAnchorType ??= ResolveType("DeathHeadHopperFix.Modules.Gameplay.Spectate.AbilityBarVisibilityAnchor");
            s_setExternalDemandMethod ??= s_abilityBarVisibilityAnchorType?.GetMethod("SetExternalDemand", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        }

        private static Type? ResolveType()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = asm.GetType("DeathHeadHopperFix.Modules.Gameplay.Core.Abilities.AbilityModule", throwOnError: false);
                if (type != null && type.Assembly.GetName().Name == "DeathHeadHopperFix")
                {
                    return type;
                }
            }

            return null;
        }

        private static Type? ResolveType(string fullName)
        {
            var type = Type.GetType(fullName, throwOnError: false);
            if (type != null)
            {
                return type;
            }

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = asm.GetType(fullName, throwOnError: false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static void PushDirectionAbilityBarDemand()
        {
            if (s_setExternalDemandMethod == null)
            {
                return;
            }

            var visible = LastChanceTimerController.IsDirectionIndicatorUiVisible;
            try
            {
                s_setExternalDemandMethod.Invoke(null, new object[] { AbilityBarDemandSourceId, visible });
            }
            catch
            {
                // Keep LastChance flow resilient if DHHFix is not present or updated.
            }
        }

        private static void TryApplyDirectionIconFallback()
        {
            if (s_applyDirectionIconMethod == null)
            {
                return;
            }

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

            s_abilitySpotType ??= ResolveAbilitySpotType();
            if (s_abilitySpotType == null)
            {
                return;
            }

            s_abilitySpotIndexField ??= s_abilitySpotType.GetField("abilitySpotIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (s_abilitySpotIndexField == null)
            {
                return;
            }

            var spots = UnityEngine.Object.FindObjectsOfType(s_abilitySpotType);
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

                var index = s_abilitySpotIndexField.GetValue(spot) as int? ?? -1;
                if (index != DirectionIndicatorSlotIndex)
                {
                    continue;
                }

                try
                {
                    s_applyDirectionIconMethod.Invoke(null, new object[] { spot, s_directionSprite });
                }
                catch
                {
                    // Ability UI can be rebuilt during scene transitions.
                }
            }
        }

        private static Type? ResolveAbilitySpotType()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = asm.GetType("DeathHeadHopper.UI.AbilitySpot", throwOnError: false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}

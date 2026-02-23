#nullable enable

using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using DeathHeadHopper.UI;
using DHHFLastChanceMode.Modules.Config;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Runtime;
using DHHFLastChanceMode.Modules.Utilities;
using UnityEngine;

namespace DHHFLastChanceMode.Modules.Gameplay.Core.Abilities
{
    public static class AbilityModule
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
        private static object? s_directionAbility;
        private static Type? s_runtimeDirectionAbilityType;

        internal static void RefreshDirectionSlotVisuals()
        {
            EnsureResolved();
            EnsureDirectionAbilitySlotState();
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

            if (directionSpot.CurrentAbility == null)
            {
                TryEquipAbility(directionSpot, s_directionAbility);
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
                TryEquipAbility(directionSpot, s_directionAbility);
            }
        }

        private static bool TryEquipAbility(AbilitySpot spot, object ability)
        {
            if (spot == null || ability == null)
            {
                return false;
            }

            try
            {
                var method = typeof(AbilitySpot).GetMethod("EquipAbility", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (method == null)
                {
                    return false;
                }

                method.Invoke(spot, new[] { ability });
                return true;
            }
            catch
            {
                return false;
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

        private static object? CreateDirectionAbilityInstance()
        {
            var runtimeType = EnsureRuntimeDirectionAbilityType();
            if (runtimeType == null)
            {
                return null;
            }

            try
            {
                return ScriptableObject.CreateInstance(runtimeType);
            }
            catch
            {
                return null;
            }
        }

        private static Type? EnsureRuntimeDirectionAbilityType()
        {
            if (s_runtimeDirectionAbilityType != null)
            {
                return s_runtimeDirectionAbilityType;
            }

            var baseType = ResolveAbilityBaseType();
            if (baseType == null)
            {
                return null;
            }

            var asmName = new AssemblyName("DHHFLastChanceMode.RuntimeDirectionAbility");
            var asmBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.Run);
            var moduleBuilder = asmBuilder.DefineDynamicModule(asmName.Name);
            var typeBuilder = moduleBuilder.DefineType(
                "DHHFLastChanceMode.Modules.Gameplay.Core.Abilities.RuntimeDirectionAbility",
                TypeAttributes.Class | TypeAttributes.NotPublic,
                baseType);

            foreach (var abstractMethod in baseType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(x => x.IsAbstract))
            {
                DefineDirectionAbilityOverride(typeBuilder, abstractMethod);
            }

            s_runtimeDirectionAbilityType = typeBuilder.CreateType();
            return s_runtimeDirectionAbilityType;
        }

        private static Type? ResolveAbilityBaseType()
        {
            var property = typeof(AbilitySpot).GetProperty("CurrentAbility", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return property?.PropertyType;
        }

        private static void DefineDirectionAbilityOverride(TypeBuilder typeBuilder, MethodInfo abstractMethod)
        {
            var parameters = abstractMethod.GetParameters();
            var parameterTypes = parameters.Select(p => p.ParameterType).ToArray();
            var methodBuilder = typeBuilder.DefineMethod(
                abstractMethod.Name,
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                abstractMethod.ReturnType,
                parameterTypes);
            var il = methodBuilder.GetILGenerator();

            if (abstractMethod.ReturnType == typeof(string))
            {
                if (abstractMethod.Name.IndexOf("AbilityName", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    il.Emit(OpCodes.Call, typeof(AbilityModule).GetMethod(nameof(DirectionAbility_GetName), BindingFlags.Static | BindingFlags.Public));
                }
                else
                {
                    il.Emit(OpCodes.Ldstr, "DirectionAbility");
                }
            }
            else if (abstractMethod.ReturnType == typeof(float))
            {
                if (abstractMethod.Name.IndexOf("Energy", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    abstractMethod.Name.IndexOf("Cost", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    il.Emit(OpCodes.Call, typeof(AbilityModule).GetMethod(nameof(DirectionAbility_GetEnergyCost), BindingFlags.Static | BindingFlags.Public));
                }
                else if (abstractMethod.Name.IndexOf("Cooldown", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    il.Emit(OpCodes.Call, typeof(AbilityModule).GetMethod(nameof(DirectionAbility_GetCooldown), BindingFlags.Static | BindingFlags.Public));
                }
                else
                {
                    il.Emit(OpCodes.Ldc_R4, 0f);
                }
            }
            else if (abstractMethod.ReturnType == typeof(int))
            {
                if (abstractMethod.Name.IndexOf("AbilityLevel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    abstractMethod.Name.IndexOf("Level", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    il.Emit(OpCodes.Call, typeof(AbilityModule).GetMethod(nameof(DirectionAbility_GetLevel), BindingFlags.Static | BindingFlags.Public));
                }
                else
                {
                    il.Emit(OpCodes.Ldc_I4_1);
                }
            }
            else
            {
                if (abstractMethod.Name.IndexOf("OnAbilityDown", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    il.Emit(OpCodes.Call, typeof(AbilityModule).GetMethod(nameof(DirectionAbility_OnDown), BindingFlags.Static | BindingFlags.Public));
                }
                else if (abstractMethod.Name.IndexOf("OnAbilityHold", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    il.Emit(OpCodes.Call, typeof(AbilityModule).GetMethod(nameof(DirectionAbility_OnHold), BindingFlags.Static | BindingFlags.Public));
                }
                else if (abstractMethod.Name.IndexOf("OnAbilityUp", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    il.Emit(OpCodes.Call, typeof(AbilityModule).GetMethod(nameof(DirectionAbility_OnUp), BindingFlags.Static | BindingFlags.Public));
                }
                else if (abstractMethod.Name.IndexOf("OnAbilityCancel", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    il.Emit(OpCodes.Call, typeof(AbilityModule).GetMethod(nameof(DirectionAbility_OnCancel), BindingFlags.Static | BindingFlags.Public));
                }
            }

            il.Emit(OpCodes.Ret);
            typeBuilder.DefineMethodOverride(methodBuilder, abstractMethod);
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
            if (spot == null)
            {
                return -1;
            }

            s_abilitySpotType ??= ResolveAbilitySpotType();
            if (s_abilitySpotType == null || !s_abilitySpotType.IsInstanceOfType(spot))
            {
                return -1;
            }

            s_abilitySpotIndexField ??= s_abilitySpotType.GetField("abilitySpotIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (s_abilitySpotIndexField == null)
            {
                return -1;
            }

            return s_abilitySpotIndexField.GetValue(spot) as int? ?? -1;
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

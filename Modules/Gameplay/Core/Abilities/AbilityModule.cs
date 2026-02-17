#nullable enable

using System;
using System.Reflection;

namespace DeathHeadHopperFix.Modules.Gameplay.Core.Abilities
{
    internal static class AbilityModule
    {
        private static readonly Type? s_runtimeType = ResolveType();
        private static readonly MethodInfo? s_refreshDirectionSlotVisuals = s_runtimeType?.GetMethod("RefreshDirectionSlotVisuals", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly MethodInfo? s_setDirectionSlotActivationProgress = s_runtimeType?.GetMethod("SetDirectionSlotActivationProgress", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly MethodInfo? s_triggerDirectionSlotCooldown = s_runtimeType?.GetMethod("TriggerDirectionSlotCooldown", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        internal static void RefreshDirectionSlotVisuals()
        {
            s_refreshDirectionSlotVisuals?.Invoke(null, null);
        }

        internal static void SetDirectionSlotActivationProgress(float progress01)
        {
            s_setDirectionSlotActivationProgress?.Invoke(null, new object[] { progress01 });
        }

        internal static void TriggerDirectionSlotCooldown(float cooldownSeconds)
        {
            s_triggerDirectionSlotCooldown?.Invoke(null, new object[] { cooldownSeconds });
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
    }
}

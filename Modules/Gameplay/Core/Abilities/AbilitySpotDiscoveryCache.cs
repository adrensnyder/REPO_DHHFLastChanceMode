#nullable enable

using DeathHeadHopper.UI;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Runtime;

namespace DHHFLastChanceMode.Modules.Gameplay.Core.Abilities
{
    internal static class AbilitySpotDiscoveryCache
    {
        internal static AbilitySpot[] GetCached(float refreshSeconds)
        {
            _ = refreshSeconds;
            return LastChanceRuntimeObjectRegistry.GetAbilitySpotsSnapshot();
        }

        internal static void Invalidate()
        {
            LastChanceRuntimeObjectRegistry.ClearAbilitySpots();
        }
    }
}

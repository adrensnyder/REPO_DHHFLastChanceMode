#nullable enable

using System;
using DeathHeadHopper.UI;
using UnityEngine;

namespace DHHFLastChanceMode.Modules.Gameplay.Core.Abilities
{
    internal static class AbilitySpotDiscoveryCache
    {
        private static AbilitySpot[] s_cachedAbilitySpots = Array.Empty<AbilitySpot>();
        private static float s_nextAbilitySpotCacheRefreshAt;

        internal static AbilitySpot[] GetCached(float refreshSeconds)
        {
            if (Time.unscaledTime < s_nextAbilitySpotCacheRefreshAt)
            {
                return s_cachedAbilitySpots;
            }

            s_cachedAbilitySpots = UnityEngine.Object.FindObjectsOfType<AbilitySpot>();
            s_nextAbilitySpotCacheRefreshAt = Time.unscaledTime + Mathf.Max(0.05f, refreshSeconds);
            return s_cachedAbilitySpots;
        }

        internal static void Invalidate()
        {
            s_cachedAbilitySpots = Array.Empty<AbilitySpot>();
            s_nextAbilitySpotCacheRefreshAt = 0f;
        }
    }
}

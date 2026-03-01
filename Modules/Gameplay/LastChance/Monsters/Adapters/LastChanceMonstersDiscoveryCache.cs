#nullable enable

using System;
using UnityEngine;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Adapters
{
    internal static class LastChanceMonstersDiscoveryCache
    {
        private static Enemy[] s_cachedEnemies = Array.Empty<Enemy>();
        private static EnemyAnimal[] s_cachedAnimals = Array.Empty<EnemyAnimal>();
        private static PlayerVoiceChat[] s_cachedVoiceChats = Array.Empty<PlayerVoiceChat>();
        private static float s_nextEnemiesRefreshAt;
        private static float s_nextAnimalsRefreshAt;
        private static float s_nextVoiceChatsRefreshAt;

        internal static Enemy[] GetEnemies(float refreshSeconds)
        {
            if (Time.unscaledTime < s_nextEnemiesRefreshAt)
            {
                return s_cachedEnemies;
            }

            s_cachedEnemies = UnityEngine.Object.FindObjectsOfType<Enemy>();
            s_nextEnemiesRefreshAt = Time.unscaledTime + Mathf.Max(0.05f, refreshSeconds);
            return s_cachedEnemies;
        }

        internal static EnemyAnimal[] GetEnemyAnimals(float refreshSeconds)
        {
            if (Time.unscaledTime < s_nextAnimalsRefreshAt)
            {
                return s_cachedAnimals;
            }

            s_cachedAnimals = UnityEngine.Object.FindObjectsOfType<EnemyAnimal>();
            s_nextAnimalsRefreshAt = Time.unscaledTime + Mathf.Max(0.05f, refreshSeconds);
            return s_cachedAnimals;
        }

        internal static PlayerVoiceChat[] GetPlayerVoiceChats(float refreshSeconds)
        {
            if (Time.unscaledTime < s_nextVoiceChatsRefreshAt)
            {
                return s_cachedVoiceChats;
            }

            s_cachedVoiceChats = UnityEngine.Object.FindObjectsOfType<PlayerVoiceChat>();
            s_nextVoiceChatsRefreshAt = Time.unscaledTime + Mathf.Max(0.05f, refreshSeconds);
            return s_cachedVoiceChats;
        }

        internal static void InvalidateAll()
        {
            InvalidateEnemies();
            InvalidateVoiceChats();
        }

        internal static void InvalidateEnemies()
        {
            s_cachedEnemies = Array.Empty<Enemy>();
            s_cachedAnimals = Array.Empty<EnemyAnimal>();
            s_nextEnemiesRefreshAt = 0f;
            s_nextAnimalsRefreshAt = 0f;
        }

        internal static void InvalidateVoiceChats()
        {
            s_cachedVoiceChats = Array.Empty<PlayerVoiceChat>();
            s_nextVoiceChatsRefreshAt = 0f;
        }
    }
}

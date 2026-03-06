#nullable enable

using DHHFLastChanceMode.Modules.Gameplay.LastChance.Runtime;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Adapters
{
    internal static class LastChanceMonstersDiscoveryCache
    {
        internal static Enemy[] GetEnemies(float refreshSeconds)
        {
            _ = refreshSeconds;
            return LastChanceRuntimeObjectRegistry.GetEnemiesSnapshot();
        }

        internal static EnemyAnimal[] GetEnemyAnimals(float refreshSeconds)
        {
            _ = refreshSeconds;
            return LastChanceRuntimeObjectRegistry.GetEnemyAnimalsSnapshot();
        }

        internal static PlayerVoiceChat[] GetPlayerVoiceChats(float refreshSeconds)
        {
            _ = refreshSeconds;
            return LastChanceRuntimeObjectRegistry.GetPlayerVoiceChatsSnapshot();
        }

        internal static void InvalidateAll()
        {
            InvalidateEnemies();
            InvalidateVoiceChats();
        }

        internal static void InvalidateEnemies()
        {
            LastChanceRuntimeObjectRegistry.ClearEnemies();
        }

        internal static void InvalidateVoiceChats()
        {
            LastChanceRuntimeObjectRegistry.ClearVoiceChats();
        }
    }
}

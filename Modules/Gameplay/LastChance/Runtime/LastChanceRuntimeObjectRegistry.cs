#nullable enable

using System.Collections.Generic;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Runtime
{
    // Central runtime registry used by LastChance modules to share typed object discovery without global scans.
    internal static class LastChanceRuntimeObjectRegistry
    {
        private static readonly List<Enemy> Enemies = new();
        private static readonly HashSet<int> EnemyIds = new();

        private static readonly List<EnemyAnimal> EnemyAnimals = new();
        private static readonly HashSet<int> EnemyAnimalIds = new();

        private static readonly List<PlayerVoiceChat> PlayerVoiceChats = new();
        private static readonly HashSet<int> PlayerVoiceChatIds = new();

        internal static void RegisterEnemy(Enemy? enemy)
        {
            Register(Enemies, EnemyIds, enemy);
        }

        internal static void UnregisterEnemy(Enemy? enemy)
        {
            Unregister(Enemies, EnemyIds, enemy);
        }

        internal static Enemy[] GetEnemiesSnapshot()
        {
            return Snapshot(Enemies, EnemyIds);
        }

        internal static void RegisterEnemyAnimal(EnemyAnimal? enemyAnimal)
        {
            Register(EnemyAnimals, EnemyAnimalIds, enemyAnimal);
        }

        internal static void UnregisterEnemyAnimal(EnemyAnimal? enemyAnimal)
        {
            Unregister(EnemyAnimals, EnemyAnimalIds, enemyAnimal);
        }

        internal static EnemyAnimal[] GetEnemyAnimalsSnapshot()
        {
            return Snapshot(EnemyAnimals, EnemyAnimalIds);
        }

        internal static void RegisterPlayerVoiceChat(PlayerVoiceChat? voiceChat)
        {
            Register(PlayerVoiceChats, PlayerVoiceChatIds, voiceChat);
        }

        internal static void UnregisterPlayerVoiceChat(PlayerVoiceChat? voiceChat)
        {
            Unregister(PlayerVoiceChats, PlayerVoiceChatIds, voiceChat);
        }

        internal static PlayerVoiceChat[] GetPlayerVoiceChatsSnapshot()
        {
            return Snapshot(PlayerVoiceChats, PlayerVoiceChatIds);
        }

        internal static void ClearEnemies()
        {
            Enemies.Clear();
            EnemyIds.Clear();
            EnemyAnimals.Clear();
            EnemyAnimalIds.Clear();
        }

        internal static void ClearVoiceChats()
        {
            PlayerVoiceChats.Clear();
            PlayerVoiceChatIds.Clear();
        }

        internal static void ClearAll()
        {
            ClearEnemies();
            ClearVoiceChats();
        }

        internal static void ResetForRoomExit()
        {
            ClearAll();
        }

        internal static void ResetForSceneChange()
        {
            ClearAll();
            RepopulateFromKnownManagers();
        }

        internal static void ResetForRuntimeDeactivated()
        {
            ClearAll();
            RepopulateFromKnownManagers();
        }

        private static void RepopulateFromKnownManagers()
        {
            var enemyParents = EnemyDirector.instance?.enemiesSpawned;
            if (enemyParents != null)
            {
                for (var i = 0; i < enemyParents.Count; i++)
                {
                    var enemy = enemyParents[i]?.Enemy;
                    RegisterEnemy(enemy);
                    if (enemy != null && enemy.TryGetComponent<EnemyAnimal>(out var enemyAnimal))
                    {
                        RegisterEnemyAnimal(enemyAnimal);
                    }
                }
            }

            var voiceChats = RunManager.instance?.voiceChats;
            if (voiceChats != null)
            {
                for (var i = 0; i < voiceChats.Count; i++)
                {
                    RegisterPlayerVoiceChat(voiceChats[i]);
                }
            }
        }

        private static void Register<T>(List<T> items, HashSet<int> ids, T? item)
            where T : UnityEngine.Object
        {
            if (item == null)
            {
                return;
            }

            var id = item.GetInstanceID();
            if (!ids.Add(id))
            {
                return;
            }

            items.Add(item);
        }

        private static void Unregister<T>(List<T> items, HashSet<int> ids, T? item)
            where T : UnityEngine.Object
        {
            if (item == null)
            {
                return;
            }

            var id = item.GetInstanceID();
            if (!ids.Remove(id))
            {
                return;
            }

            for (var i = items.Count - 1; i >= 0; i--)
            {
                var current = items[i];
                if (current == null || current.GetInstanceID() == id)
                {
                    items.RemoveAt(i);
                }
            }
        }

        private static T[] Snapshot<T>(List<T> items, HashSet<int> ids)
            where T : UnityEngine.Object
        {
            for (var i = items.Count - 1; i >= 0; i--)
            {
                var current = items[i];
                if (current != null)
                {
                    continue;
                }

                items.RemoveAt(i);
            }

            ids.Clear();
            for (var i = 0; i < items.Count; i++)
            {
                ids.Add(items[i].GetInstanceID());
            }

            return items.ToArray();
        }
    }
}

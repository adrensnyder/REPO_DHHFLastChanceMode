#nullable enable

using System;
using System.Collections.Generic;
using BepInEx.Logging;
using DHHFLastChanceMode.Modules.Config;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Runtime;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Support;
using HarmonyLib;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Pipeline
{
    internal static class LastChanceMonstersSearchModule
    {
        private const string PatchId = "DHHFLastChanceMode.Gameplay.LastChance.MonstersSearch";
        private static readonly System.Reflection.FieldInfo? s_playerIsDisabledField =
            AccessTools.Field(typeof(PlayerAvatar), nameof(PlayerAvatar.isDisabled));
        private static readonly AccessTools.FieldRef<PlayerAvatar, bool> s_playerIsDisabledGetter =
            AccessTools.FieldRefAccess<PlayerAvatar, bool>(nameof(PlayerAvatar.isDisabled));
        private static readonly ManualLogSource Log = Logger.CreateLogSource("DHHFLastChanceMode.LastChance.MonstersSearch");
        private static readonly HashSet<System.Reflection.MethodBase> s_patchedMethods = new();
        private static readonly List<System.Reflection.MethodBase> s_patchTargets = CreatePatchTargets();
        private static Harmony? s_harmony;
        private static float s_runtimeStateCachedAt;
        private static bool s_runtimeStateEnabled;
        private static bool s_loggedActivationSnapshot;
        private static float s_lastSelfCheckAt;

        internal static void ResetRuntimeState()
        {
            s_runtimeStateCachedAt = 0f;
            s_runtimeStateEnabled = false;
            s_loggedActivationSnapshot = false;
            s_lastSelfCheckAt = 0f;
        }

        internal static void Apply(Harmony harmony)
        {
            if (s_harmony != null || harmony == null)
            {
                return;
            }

            s_harmony = new Harmony(PatchId);
            PatchExplicitTargets();
        }

        internal static void Unapply()
        {
            if (s_harmony == null)
            {
                return;
            }

            try
            {
                s_harmony.UnpatchSelf();
            }
            catch
            {
                // Best-effort unpatch.
            }

            s_patchedMethods.Clear();
            s_harmony = null;
            ResetRuntimeState();
        }

        internal static int GetAliveSearchMonsterCount()
        {
            if (!FeatureFlags.LastChanceMonstersSearchEnabled || !FeatureFlags.LastChangeMode || !LastChanceRuntimeOrchestrator.IsRuntimeActive)
            {
                return 0;
            }

            var director = EnemyDirector.instance;
            if (director == null || director.enemiesSpawned == null)
            {
                return 0;
            }

            var count = 0;
            foreach (var enemyParent in director.enemiesSpawned)
            {
                if (enemyParent == null || !IsActiveEnemy(enemyParent))
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        private static void PatchExplicitTargets()
        {
            if (s_harmony == null)
            {
                return;
            }

            var transpiler = new HarmonyMethod(typeof(LastChanceMonstersSearchModule), nameof(ReplaceDisabledChecksTranspiler));
            var patchedNow = 0;
            foreach (var method in s_patchTargets)
            {
                if (method == null || s_patchedMethods.Contains(method))
                {
                    continue;
                }

                s_harmony.Patch(method, transpiler: transpiler);
                s_patchedMethods.Add(method);
                patchedNow++;
            }

            if (patchedNow > 0 && FeatureFlags.DebugLogging)
            {
                Log.LogInfo($"[LastChance] MonstersSearch patched explicit methods: {patchedNow}.");
            }
        }

        private static bool IsActiveEnemy(EnemyParent enemyParent)
        {
            if (!enemyParent.Spawned)
            {
                return false;
            }

            return !enemyParent.forceLeave;
        }

        private static List<System.Reflection.MethodBase> CreatePatchTargets()
        {
            var methods = new List<System.Reflection.MethodBase>();

            AddCoreEnemyTargets(methods);
            AddStateMachineTargets(methods);

            return LastChanceMonstersPatchTargetHelper.Deduplicate(methods);
        }

        private static void AddCoreEnemyTargets(List<System.Reflection.MethodBase> methods)
        {
            LastChanceMonstersPatchTargetHelper.AddDeclaredMethod(methods, typeof(Enemy), nameof(Enemy.SetChaseTarget), typeof(PlayerAvatar));
            LastChanceMonstersPatchTargetHelper.AddDeclaredMethod(methods, typeof(Enemy), nameof(Enemy.OnPhotonSerializeView), typeof(Photon.Pun.PhotonStream), typeof(Photon.Pun.PhotonMessageInfo));
            LastChanceMonstersPatchTargetHelper.AddDeclaredMethod(methods, typeof(EnemyParent), nameof(EnemyParent.PlayerCloseLogic));
            LastChanceMonstersPatchTargetHelper.AddDeclaredMethod(methods, typeof(EnemyPlayerDistance), nameof(EnemyPlayerDistance.Logic));
            LastChanceMonstersPatchTargetHelper.AddDeclaredMethod(methods, typeof(EnemyPlayerRoom), nameof(EnemyPlayerRoom.Logic));
            LastChanceMonstersPatchTargetHelper.AddDeclaredMethod(methods, typeof(EnemyTriggerAttack), nameof(EnemyTriggerAttack.OnTriggerStay), typeof(Collider));
            LastChanceMonstersPatchTargetHelper.AddDeclaredMethod(methods, typeof(EnemyVision), nameof(EnemyVision.Vision));
        }

        private static void AddStateMachineTargets(List<System.Reflection.MethodBase> methods)
        {
            LastChanceMonstersPatchTargetHelper.AddDeclaredMethod(methods, typeof(EnemyStateChase), nameof(EnemyStateChase.Update));
            LastChanceMonstersPatchTargetHelper.AddDeclaredMethod(methods, typeof(EnemyStateChaseBegin), nameof(EnemyStateChaseBegin.Update));
            LastChanceMonstersPatchTargetHelper.AddDeclaredMethod(methods, typeof(EnemyStateRoaming), nameof(EnemyStateRoaming.PlayerTurn));
            LastChanceMonstersPatchTargetHelper.AddDeclaredMethod(methods, typeof(EnemyStateSneak), nameof(EnemyStateSneak.Update));
        }

        private static IEnumerable<CodeInstruction> ReplaceDisabledChecksTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var list = new List<CodeInstruction>(instructions);
            var remapMethod = AccessTools.Method(typeof(LastChanceMonstersSearchModule), nameof(RemapMonsterDisabledCheck));
            if (remapMethod == null)
            {
                return list;
            }

            for (var i = 0; i < list.Count; i++)
            {
                var instruction = list[i];
                if ((instruction.opcode == System.Reflection.Emit.OpCodes.Ldfld || instruction.opcode == System.Reflection.Emit.OpCodes.Ldflda) &&
                    instruction.operand is System.Reflection.FieldInfo field &&
                    field == s_playerIsDisabledField)
                {
                    instruction.opcode = System.Reflection.Emit.OpCodes.Call;
                    instruction.operand = remapMethod;
                }
            }

            return list;
        }

        private static bool RemapMonsterDisabledCheck(PlayerAvatar? player)
        {
            if (player == null)
            {
                return false;
            }

            if (IsMonstersSearchRuntimeEnabled())
            {
                return false;
            }

            return s_playerIsDisabledGetter(player);
        }

        private static bool IsMonstersSearchRuntimeEnabled()
        {
            // Fast cache for the hot enemy-AI path; refresh often enough for responsive toggles.
            var now = Time.unscaledTime;
            if (now - s_runtimeStateCachedAt < 0.1f)
            {
                return s_runtimeStateEnabled;
            }

            s_runtimeStateCachedAt = now;
            var wasEnabled = s_runtimeStateEnabled;
            s_runtimeStateEnabled =
                FeatureFlags.LastChanceMonstersSearchEnabled &&
                FeatureFlags.LastChangeMode &&
                LastChanceRuntimeOrchestrator.IsRuntimeActive;

            if (!s_runtimeStateEnabled)
            {
                s_loggedActivationSnapshot = false;
            }
            else if (!wasEnabled && !s_loggedActivationSnapshot)
            {
                TryLogActivationSnapshot();
            }

            TryRunRemapSelfCheck(now);

            return s_runtimeStateEnabled;
        }

        private static void TryRunRemapSelfCheck(float now)
        {
            if (!FeatureFlags.DebugLogging || now - s_lastSelfCheckAt < 2f)
            {
                return;
            }

            s_lastSelfCheckAt = now;
            var players = GameDirector.instance?.PlayerList;
            if (players == null || players.Count == 0)
            {
                return;
            }

            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player == null)
                {
                    continue;
                }

                var rawDisabled = s_playerIsDisabledGetter(player);
                var remapped = RemapMonsterDisabledCheck(player);
                var expected = s_runtimeStateEnabled ? false : rawDisabled;
                if (remapped != expected)
                {
                    Log.LogWarning(
                        $"[LastChance] MonstersSearch self-check mismatch: player={player.photonView?.ViewID ?? player.GetInstanceID()} " +
                        $"runtime={s_runtimeStateEnabled} rawDisabled={rawDisabled} remapped={remapped} expected={expected}");
                }
            }
        }

        private static void TryLogActivationSnapshot()
        {
            if (!FeatureFlags.DebugLogging || !FeatureFlags.LastChanceMonstersSearchEnabled)
            {
                return;
            }

            s_loggedActivationSnapshot = true;

            var director = EnemyDirector.instance;
            if (director == null || director.enemiesSpawned == null)
            {
                Log.LogInfo("[LastChance] MonstersSearch activation snapshot: EnemyDirector/enemiesSpawned not available.");
                return;
            }

            var enemies = director.enemiesSpawned;
            Log.LogInfo($"[LastChance] MonstersSearch activation snapshot: total={enemies.Count}.");
            for (var i = 0; i < enemies.Count; i++)
            {
                var parent = enemies[i];
                if (parent == null)
                {
                    Log.LogInfo($"[LastChance] MonstersSearch enemy[{i}] = null");
                    continue;
                }

                var enemy = parent.Enemy;
                var typeName = GetConcreteEnemyTypeName(enemy);
                Log.LogInfo($"[LastChance] MonstersSearch enemy[{i}] type={typeName} spawned={parent.Spawned} forceLeave={parent.forceLeave}");
            }
        }

        private static string GetConcreteEnemyTypeName(object? enemyObj)
        {
            if (enemyObj == null)
            {
                return "null";
            }

            if (enemyObj is not Component component)
            {
                return enemyObj.GetType().Name;
            }

            var baseName = enemyObj.GetType().Name;
            var behaviours = component.GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour == null)
                {
                    continue;
                }

                var name = behaviour.GetType().Name;
                if (name.StartsWith("Enemy", StringComparison.Ordinal) && !string.Equals(name, "Enemy", StringComparison.Ordinal))
                {
                    return $"{baseName}/{name}";
                }
            }

            return baseName;
        }
    }
}

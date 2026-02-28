#nullable enable

using System;
using System.Collections.Generic;
using BepInEx.Logging;
using DHHFLastChanceMode.Modules.Config;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Runtime;
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
            AddHeavyMonsterTargets(methods);
            AddStateMachineTargets(methods);

            return methods;
        }

        private static void AddCoreEnemyTargets(List<System.Reflection.MethodBase> methods)
        {
            AddMethod(methods, typeof(Enemy), nameof(Enemy.SetChaseTarget), typeof(PlayerAvatar));
            AddMethod(methods, typeof(Enemy), nameof(Enemy.OnPhotonSerializeView), typeof(Photon.Pun.PhotonStream), typeof(Photon.Pun.PhotonMessageInfo));
            AddMethod(methods, typeof(EnemyParent), "PlayerCloseLogic");
            AddMethod(methods, typeof(EnemyPlayerDistance), "Logic");
            AddMethod(methods, typeof(EnemyPlayerRoom), "Logic");
            AddMethod(methods, typeof(EnemyTriggerAttack), "OnTriggerStay", typeof(Collider));
            AddMethod(methods, typeof(EnemyVision), "Vision");
        }

        private static void AddHeavyMonsterTargets(List<System.Reflection.MethodBase> methods)
        {
            AddMethod(methods, typeof(EnemyBangDirector), "StateAttackPlayer");
            AddMethod(methods, typeof(EnemyBirthdayBoy), "Update");
            AddMethod(methods, typeof(EnemyBirthdayBoy), "CheckIfPlayersNearbyPop", typeof(Vector3));
            AddMethod(methods, typeof(EnemyBirthdayBoy), "PlayerNearby");
            AddMethod(methods, typeof(EnemyBombThrower), "StateGotoPlayer");
            AddMethod(methods, typeof(EnemyBombThrower), "StateBackAwayPlayer");
            AddMethod(methods, typeof(EnemyBombThrower), "StateBackAwayHead");
            AddMethod(methods, typeof(EnemyBombThrowerAnim), "HeadLookAtLogic");
            AddMethod(methods, typeof(EnemyBombThrowerHead), "StateSpawn", typeof(bool));
            AddMethod(methods, typeof(EnemyBombThrowerHead), "StateActive", typeof(bool));
            AddMethod(methods, typeof(EnemyBombThrowerHead), "EyeLogic");
            AddMethod(methods, typeof(EnemyCeilingEye), "StateHasTarget");
            AddMethod(methods, typeof(EnemyCeilingEye), nameof(EnemyCeilingEye.TargetFailSafe));
            AddMethod(methods, typeof(EnemyDuck), "StateGoToPlayer");
            AddMethod(methods, typeof(EnemyDuck), "StateGoToPlayerUnder");
            AddMethod(methods, typeof(EnemyDuck), "StateGoToPlayerOver");
            AddMethod(methods, typeof(EnemyDuck), "StateStun");
            AddMethod(methods, typeof(EnemyDuck), "HeadLookAtLogic");
            AddMethod(methods, typeof(EnemyDuck), "ChaseStop");
            AddMethod(methods, typeof(EnemyElsa), "Update");
            AddMethod(methods, typeof(EnemyElsa), "StateGoToPlayerSmall");
            AddMethod(methods, typeof(EnemyElsa), "StateGoToPlayerUnderSmall");
            AddMethod(methods, typeof(EnemyElsa), "StateGoToPlayerOverBig");
            AddMethod(methods, typeof(EnemyElsa), "StateLookUnderBig");
            AddMethod(methods, typeof(EnemyElsa), "ChaseStop");
            AddMethod(methods, typeof(EnemyGnomeDirector), "StateAttackPlayer");
            AddMethod(methods, typeof(EnemyHeadGrabber), "GotoLogic");
            AddMethod(methods, typeof(EnemyHeadGrabber), "GotoOverLogic");
            AddMethod(methods, typeof(EnemyHeadGrabber), "DeathHeadLogic");
            AddMethod(methods, typeof(EnemyHeadGrabber), "GetClosestDeathHead", typeof(float));
            AddMethod(methods, typeof(EnemyHeadUp), "Update");
            AddMethod(methods, typeof(EnemyHidden), "StatePlayerGoTo");
            AddMethod(methods, typeof(EnemyHidden), "StatePlayerPickup");
            AddMethod(methods, typeof(EnemyHidden), "StatePlayerMove");
            AddMethod(methods, typeof(EnemyHidden), "StatePlayerRelease");
            AddMethod(methods, typeof(EnemyHidden), "PlayerTumbleLogic");
            AddMethod(methods, typeof(EnemyOnScreen), "Logic");
            AddMethod(methods, typeof(EnemyOogly), "FixedUpdate");
            AddMethod(methods, typeof(EnemyOogly), "CheckSinglePlayerNearby");
            AddMethod(methods, typeof(EnemyOogly), "StatePlayerSpotted");
            AddMethod(methods, typeof(EnemyOogly), "StateWrestlePlayer");
            AddMethod(methods, typeof(EnemyParent), "PlayerCloseLogic");
            AddMethod(methods, typeof(EnemyPlayerDistance), "Logic");
            AddMethod(methods, typeof(EnemyPlayerRoom), "Logic");
            AddMethod(methods, typeof(EnemyRobe), "StateLookUnderAttack");
            AddMethod(methods, typeof(EnemyRunner), nameof(EnemyRunner.StateAttackPlayer));
            AddMethod(methods, typeof(EnemyRunner), nameof(EnemyRunner.StateAttackPlayerOver));
            AddMethod(methods, typeof(EnemyShadow), "Update");
            AddMethod(methods, typeof(EnemyShadow), "StateChooseTarget");
            AddMethod(methods, typeof(EnemyShadow), "PlayerTargetTell");
            AddMethod(methods, typeof(EnemyShadow), "PlayerTargetStopTell");
            AddMethod(methods, typeof(EnemySlowMouth), "DetatchLogic");
            AddMethod(methods, typeof(EnemySlowMouth), "StateAttached", typeof(bool));
            AddMethod(methods, typeof(EnemySlowMouth), "StateDetach", typeof(bool));
            AddMethod(methods, typeof(EnemySlowMouth), "StateGoToPlayerOver", typeof(bool));
            AddMethod(methods, typeof(EnemySlowMouth), "StateGoToPlayerUnder", typeof(bool));
            AddMethod(methods, typeof(EnemySlowMouth), "TargettingPlayer");
            AddMethod(methods, typeof(EnemySlowMouth), "IsPossessedBySeveral");
            AddMethod(methods, typeof(EnemySlowMouthAttaching), "Update");
            AddMethod(methods, typeof(EnemySlowMouthAttaching), "AttachToPlayer");
            AddMethod(methods, typeof(EnemySlowMouthPlayerAvatarAttached), "OnDisable");
            AddMethod(methods, typeof(EnemySlowWalker), nameof(EnemySlowWalker.StateLookUnderAttack));
            AddMethod(methods, typeof(EnemySpinny), "Update");
            AddMethod(methods, typeof(EnemyThinMan), "StateStand");
            AddMethod(methods, typeof(EnemyTick), "Update");
            AddMethod(methods, typeof(EnemyTricycle), "StateStateBeforeAttack");
            AddMethod(methods, typeof(EnemyTricycle), "StateAttackDive");
            AddMethod(methods, typeof(EnemyTricycle), "FixedUpdateAttackDive");
            AddMethod(methods, typeof(EnemyTricycle), "FixedUpdateAttack");
            AddMethod(methods, typeof(EnemyTricycle), "RotationFollowTargetOrVelocity");
            AddMethod(methods, typeof(EnemyValuableThrower), "TargetFailsafe");
        }

        private static void AddStateMachineTargets(List<System.Reflection.MethodBase> methods)
        {
            AddMethod(methods, typeof(EnemyStateChase), "Update");
            AddMethod(methods, typeof(EnemyStateChaseBegin), "Update");
            AddMethod(methods, typeof(EnemyStateRoaming), "PlayerTurn");
            AddMethod(methods, typeof(EnemyStateSneak), "Update");
        }

        private static void AddMethod(List<System.Reflection.MethodBase> methods, Type declaringType, string methodName, params Type[] argumentTypes)
        {
            var method = argumentTypes.Length == 0
                ? AccessTools.DeclaredMethod(declaringType, methodName)
                : AccessTools.DeclaredMethod(declaringType, methodName, argumentTypes);
            if (method != null)
            {
                methods.Add(method);
            }
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

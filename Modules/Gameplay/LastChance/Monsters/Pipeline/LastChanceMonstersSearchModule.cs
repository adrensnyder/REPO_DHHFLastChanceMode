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
        private static readonly System.Reflection.Emit.OpCode[] s_singleByteOpcodes = BuildSingleByteOpcodeMap();
        private static readonly System.Reflection.Emit.OpCode[] s_doubleByteOpcodes = BuildDoubleByteOpcodeMap();
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
            var skippedNoField = 0;
            foreach (var method in s_patchTargets)
            {
                if (method == null || s_patchedMethods.Contains(method))
                {
                    continue;
                }

                if (s_playerIsDisabledField != null && !MethodLoadsPlayerDisabledField(method, s_playerIsDisabledField))
                {
                    skippedNoField++;
                    continue;
                }

                s_harmony.Patch(method, transpiler: transpiler);
                s_patchedMethods.Add(method);
                patchedNow++;
            }

            if (patchedNow > 0 && FeatureFlags.DebugLogging)
            {
                Log.LogInfo($"[LastChance] MonstersSearch patched explicit methods: {patchedNow}.");
                if (skippedNoField > 0)
                {
                    Log.LogInfo($"[LastChance] MonstersSearch skipped methods without direct {nameof(PlayerAvatar.isDisabled)} load: {skippedNoField}.");
                }
            }
        }

        private static bool MethodLoadsPlayerDisabledField(System.Reflection.MethodBase method, System.Reflection.FieldInfo targetField)
        {
            var body = method.GetMethodBody();
            var il = body?.GetILAsByteArray();
            if (il == null || il.Length == 0)
            {
                return false;
            }

            var position = 0;
            while (position < il.Length)
            {
                var opcode = ReadOpcode(il, ref position);
                if (opcode.Equals(default(System.Reflection.Emit.OpCode)))
                {
                    return false;
                }

                if (opcode.OperandType == System.Reflection.Emit.OperandType.InlineField)
                {
                    if (position + 4 > il.Length)
                    {
                        return false;
                    }

                    var token = BitConverter.ToInt32(il, position);
                    position += 4;

                    if ((opcode == System.Reflection.Emit.OpCodes.Ldfld || opcode == System.Reflection.Emit.OpCodes.Ldflda) &&
                        TryResolveField(method, token, out var field) &&
                        field == targetField)
                    {
                        return true;
                    }

                    continue;
                }

                if (!AdvanceOperand(il, ref position, opcode.OperandType))
                {
                    return false;
                }
            }

            return false;
        }

        private static bool TryResolveField(System.Reflection.MethodBase method, int metadataToken, out System.Reflection.FieldInfo? field)
        {
            field = null;
            try
            {
                var typeArgs = method.DeclaringType?.GetGenericArguments();
                var methodArgs = method is System.Reflection.MethodInfo info ? info.GetGenericArguments() : null;
                field = method.Module.ResolveField(metadataToken, typeArgs, methodArgs);
                return field != null;
            }
            catch
            {
                return false;
            }
        }

        private static System.Reflection.Emit.OpCode ReadOpcode(byte[] il, ref int position)
        {
            if (position >= il.Length)
            {
                return default;
            }

            var b = il[position++];
            if (b != 0xFE)
            {
                return s_singleByteOpcodes[b];
            }

            if (position >= il.Length)
            {
                return default;
            }

            var b2 = il[position++];
            return s_doubleByteOpcodes[b2];
        }

        private static bool AdvanceOperand(byte[] il, ref int position, System.Reflection.Emit.OperandType operandType)
        {
            switch (operandType)
            {
                case System.Reflection.Emit.OperandType.InlineNone:
                    return true;
                case System.Reflection.Emit.OperandType.ShortInlineBrTarget:
                case System.Reflection.Emit.OperandType.ShortInlineI:
                case System.Reflection.Emit.OperandType.ShortInlineVar:
                    position += 1;
                    return position <= il.Length;
                case System.Reflection.Emit.OperandType.InlineVar:
                    position += 2;
                    return position <= il.Length;
                case System.Reflection.Emit.OperandType.InlineBrTarget:
                case System.Reflection.Emit.OperandType.InlineI:
                case System.Reflection.Emit.OperandType.InlineMethod:
                case System.Reflection.Emit.OperandType.InlineSig:
                case System.Reflection.Emit.OperandType.InlineString:
                case System.Reflection.Emit.OperandType.InlineTok:
                case System.Reflection.Emit.OperandType.InlineType:
                    position += 4;
                    return position <= il.Length;
                case System.Reflection.Emit.OperandType.InlineI8:
                case System.Reflection.Emit.OperandType.InlineR:
                    position += 8;
                    return position <= il.Length;
                case System.Reflection.Emit.OperandType.ShortInlineR:
                    position += 4;
                    return position <= il.Length;
                case System.Reflection.Emit.OperandType.InlineSwitch:
                    if (position + 4 > il.Length)
                    {
                        return false;
                    }

                    var count = BitConverter.ToInt32(il, position);
                    position += 4 + (count * 4);
                    return position <= il.Length;
                default:
                    return false;
            }
        }

        private static System.Reflection.Emit.OpCode[] BuildSingleByteOpcodeMap()
        {
            var map = new System.Reflection.Emit.OpCode[256];
            var fields = typeof(System.Reflection.Emit.OpCodes).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            for (var i = 0; i < fields.Length; i++)
            {
                if (fields[i].GetValue(null) is not System.Reflection.Emit.OpCode opcode)
                {
                    continue;
                }

                var value = (ushort)opcode.Value;
                if (value <= 0xFF)
                {
                    map[value] = opcode;
                }
            }

            return map;
        }

        private static System.Reflection.Emit.OpCode[] BuildDoubleByteOpcodeMap()
        {
            var map = new System.Reflection.Emit.OpCode[256];
            var fields = typeof(System.Reflection.Emit.OpCodes).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            for (var i = 0; i < fields.Length; i++)
            {
                if (fields[i].GetValue(null) is not System.Reflection.Emit.OpCode opcode)
                {
                    continue;
                }

                var value = (ushort)opcode.Value;
                if ((value & 0xFF00) == 0xFE00)
                {
                    map[value & 0xFF] = opcode;
                }
            }

            return map;
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

            return DeduplicateTargets(methods);
        }

        private static List<System.Reflection.MethodBase> DeduplicateTargets(List<System.Reflection.MethodBase> methods)
        {
            var unique = new List<System.Reflection.MethodBase>(methods.Count);
            var seen = new HashSet<System.Reflection.MethodBase>();
            for (var i = 0; i < methods.Count; i++)
            {
                var method = methods[i];
                if (method == null || !seen.Add(method))
                {
                    continue;
                }

                unique.Add(method);
            }

            return unique;
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

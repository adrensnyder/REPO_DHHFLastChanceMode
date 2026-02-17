#nullable enable

using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Logging;
using DHHFLastChanceMode.Modules.Config;
using DeathHeadHopperFix.Modules.Utilities;
using HarmonyLib;
using Logger = BepInEx.Logging.Logger;

namespace DeathHeadHopperFix.Modules.Gameplay.LastChance.Guards
{
    internal static class AllPlayersDeadGuard
    {
        private const string ModuleTag = "[DeathHeadHopperFix] [Gameplay]";
        private const string LogKey = "SuppressAllDeadTransition";
        private static readonly ManualLogSource Log = Logger.CreateLogSource("DeathHeadHopperFix.Gameplay");
        private static readonly FieldInfo? AllPlayersDeadField = AccessTools.Field(typeof(RunManager), "allPlayersDead");
        private static readonly FieldInfo? PlayerIsDisabledField = AccessTools.Field(typeof(PlayerAvatar), "isDisabled");
        private static Harmony? _harmony;
        private static bool s_suppressedLogged;
        private static bool s_allowAllPlayersDead;

        internal static void EnsureEnabled()
        {
            if (_harmony != null)
            {
                return;
            }

            var changeLevelMethod = AccessTools.Method(typeof(RunManager), nameof(RunManager.ChangeLevel), new[] { typeof(bool), typeof(bool), typeof(RunManager.ChangeLevelType) });
            if (changeLevelMethod == null)
            {
                if (FeatureFlags.DebugLogging && LogLimiter.ShouldLog(LogKey))
                {
                    Log.LogWarning($"{ModuleTag} Cannot find RunManager methods for patching.");
                }
                return;
            }

            _harmony = new Harmony("DeathHeadHopperFix.Gameplay.AllPlayersDeadGuard");
            // This transpiler intentionally coexists with RunManagerUpdateLastChanceTimerPatch.Postfix.
            // It owns only the guard of vanilla allPlayersDead assignment flow.
            _harmony.Patch(
                AccessTools.Method(typeof(RunManager), "Update"),
                transpiler: new HarmonyMethod(typeof(AllPlayersDeadGuard), nameof(UpdateTranspiler)));
            _harmony.Patch(changeLevelMethod, prefix: new HarmonyMethod(typeof(AllPlayersDeadGuard), nameof(ChangeLevelPrefix)));

            if (FeatureFlags.DebugLogging && LogLimiter.ShouldLog(LogKey))
            {
                Log.LogInfo($"{ModuleTag} Override enabled (debug players count).");
            }
        }

        internal static void Disable()
        {
            if (_harmony == null)
            {
                return;
            }

            _harmony.UnpatchSelf();
            _harmony = null;
        }

        internal static void AllowVanillaAllPlayersDead()
        {
            s_allowAllPlayersDead = true;
        }

        internal static void ResetVanillaAllPlayersDead()
        {
            s_allowAllPlayersDead = false;
        }

        private static bool ChangeLevelPrefix(RunManager __instance, bool _completedLevel, bool _levelFailed, RunManager.ChangeLevelType _changeLevelType)
        {
            if (!FeatureFlags.LastChangeMode)
            {
                return true;
            }

            if (!CompatibilityGate.IsFeatureUsable(ModFeatureGate.LastChanceCluster))
            {
                return true;
            }

            if (LastChanceTimerController.IsSuppressedForRoom)
            {
                return true;
            }

            if (!IsProceduralMonsterLevelContext())
            {
                s_suppressedLogged = false;
                s_allowAllPlayersDead = false;
                return true;
            }

            if (s_allowAllPlayersDead)
            {
                s_suppressedLogged = false;
                s_allowAllPlayersDead = false;
                return true;
            }

            if (!_levelFailed || _changeLevelType != RunManager.ChangeLevelType.Normal)
            {
                return true;
            }

            if (!AllPlayersDisabled())
            {
                s_suppressedLogged = false;
                return true;
            }

            if (FeatureFlags.DebugLogging && !s_suppressedLogged && LogLimiter.ShouldLog(LogKey, 600))
            {
                Log.LogInfo($"{ModuleTag} Suppressed change level caused by all players dead.");
                s_suppressedLogged = true;
            }

            return false;
        }

        private static IEnumerable<CodeInstruction> UpdateTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            if (AllPlayersDeadField == null)
            {
                foreach (var instruction in instructions)
                {
                    yield return instruction;
                }
                yield break;
            }

            var guardMethod = AccessTools.Method(typeof(AllPlayersDeadGuard), nameof(GuardAllPlayersDead));
            if (guardMethod == null)
            {
                foreach (var instruction in instructions)
                {
                    yield return instruction;
                }
                yield break;
            }

            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Stfld && instruction.operand is FieldInfo field && field == AllPlayersDeadField)
                {
                    yield return new CodeInstruction(OpCodes.Call, guardMethod);
                }

                yield return instruction;
            }
        }

        private static bool GuardAllPlayersDead(bool value)
        {
            if (!value)
                return false;

            if (!FeatureFlags.LastChangeMode)
                return true;

            if (!CompatibilityGate.IsFeatureUsable(ModFeatureGate.LastChanceCluster))
                return true;

            if (LastChanceTimerController.IsSuppressedForRoom)
                return true;

            if (!IsProceduralMonsterLevelContext())
                return true;

            if (s_allowAllPlayersDead)
                return true;
            
            // Prevent vanilla all-players-dead transitions while LastChance mode is enabled.
            // Vanilla flow is re-enabled explicitly via AllowVanillaAllPlayersDead().
            return false;
        }

        private static bool IsProceduralMonsterLevelContext()
        {
            if (!SemiFunc.RunIsLevel())
            {
                return false;
            }

            var runManager = RunManager.instance;
            if (runManager == null || runManager.levelCurrent == null)
            {
                return false;
            }

            // Whitelist: only levels in the procedural run list.
            if (runManager.levels == null || !runManager.levels.Contains(runManager.levelCurrent))
            {
                return false;
            }

            // LastChance should run only where monsters are expected.
            return runManager.levelCurrent.HasEnemies;
        }

        internal static bool AllPlayersDisabled()
        {
            var director = GameDirector.instance;
            if (director == null || director.PlayerList == null)
            {
                return false;
            }

            if (director.PlayerList.Count == 0)
            {
                return true;
            }

            foreach (var player in director.PlayerList)
            {
                if (player == null)
                {
                    continue;
                }

                if (PlayerIsDisabledField == null)
                {
                    return false;
                }

                if (PlayerIsDisabledField.GetValue(player) is bool disabled && !disabled)
                {
                    return false;
                }
            }

            return true;
        }
    }
}


#nullable enable

using BepInEx.Logging;
using DHHFLastChanceMode.Modules.Config;
using DHHFLastChanceMode.Modules.Utilities;
using HarmonyLib;
using Logger = BepInEx.Logging.Logger;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Guards
{
    internal static class AllPlayersDeadGuard
    {
        private const string ModuleTag = "[DHHFLastChanceMode] [Gameplay]";
        private const string LogKey = "SuppressAllDeadTransition";
        private const string SetGuardLogKey = "SuppressAllDeadFlag";
        private static readonly ManualLogSource Log = Logger.CreateLogSource("DHHFLastChanceMode.Gameplay");
        private static bool s_enabledLogged;
        private static bool s_suppressedLogged;
        private static bool s_allowAllPlayersDead;

        internal static void EnsureEnabled()
        {
            if (s_enabledLogged)
            {
                return;
            }

            s_enabledLogged = true;

            if (FeatureFlags.DebugLogging && LogLimiter.ShouldLog(LogKey))
            {
                Log.LogInfo($"{ModuleTag} Guard enabled via typed all-dead prefix patches.");
            }
        }

        internal static void Disable()
        {
            s_enabledLogged = false;
            s_suppressedLogged = false;
            s_allowAllPlayersDead = false;
        }

        internal static void AllowVanillaAllPlayersDead()
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer())
            {
                s_allowAllPlayersDead = false;
                return;
            }

            s_allowAllPlayersDead = true;
        }

        internal static void ResetVanillaAllPlayersDead()
        {
            s_allowAllPlayersDead = false;
        }

        private static bool ChangeLevelPrefix(RunManager __instance, bool _completedLevel, bool _levelFailed, RunManager.ChangeLevelType _changeLevelType)
        {
            if (!ShouldSuppressAllPlayersDeadFlow())
            {
                s_suppressedLogged = false;
                s_allowAllPlayersDead = false;
                return true;
            }

            if (s_allowAllPlayersDead)
            {
                s_suppressedLogged = false;
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

        private static bool AllPlayersDeadSetPrefix(bool _set)
        {
            if (!_set)
            {
                // Any vanilla reset/new-scene initialization also clears a stale one-shot allowance.
                s_allowAllPlayersDead = false;
                return true;
            }

            if (!ShouldSuppressAllPlayersDeadFlow())
            {
                s_allowAllPlayersDead = false;
                return true;
            }

            if (s_allowAllPlayersDead)
            {
                s_allowAllPlayersDead = false;
                return true;
            }

            if (FeatureFlags.DebugLogging && LogLimiter.ShouldLog(SetGuardLogKey, 120))
            {
                Log.LogDebug($"{ModuleTag} Suppressed RunManager.AllPlayersDeadSet(true) during LastChance flow.");
            }

            return false;
        }

        private static bool ShouldSuppressAllPlayersDeadFlow()
        {
            if (!FeatureFlags.LastChangeMode)
            {
                return false;
            }

            if (!CompatibilityGate.IsFeatureUsable(ModFeatureGate.LastChanceCluster))
            {
                return false;
            }

            if (LastChanceTimerController.IsSuppressedForRoom)
            {
                return false;
            }

            if (IsVanillaOnlyContext())
            {
                return false;
            }

            return true;
        }

        private static bool IsVanillaOnlyContext()
        {
            // Preserve vanilla behavior in non-gameplay flows to avoid lobby/password regressions.
            return SemiFunc.RunIsArena() ||
                   SemiFunc.RunIsShop() ||
                   SemiFunc.RunIsLobbyMenu() ||
                   SemiFunc.RunIsLobby() ||
                   SemiFunc.RunIsTutorial() ||
                   SemiFunc.MenuLevel();
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

                if (!player.isDisabled)
                {
                    return false;
                }
            }

            return true;
        }

        [HarmonyPatch(typeof(RunManager), nameof(RunManager.ChangeLevel), new[] { typeof(bool), typeof(bool), typeof(RunManager.ChangeLevelType) })]
        internal static class RunManagerChangeLevelPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(RunManager __instance, bool _completedLevel, bool _levelFailed, RunManager.ChangeLevelType _changeLevelType)
            {
                return ChangeLevelPrefix(__instance, _completedLevel, _levelFailed, _changeLevelType);
            }
        }

        [HarmonyPatch(typeof(RunManager), nameof(RunManager.AllPlayersDeadSet), new[] { typeof(bool) })]
        internal static class RunManagerAllPlayersDeadSetPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(bool _set)
            {
                return AllPlayersDeadSetPrefix(_set);
            }
        }
    }
}


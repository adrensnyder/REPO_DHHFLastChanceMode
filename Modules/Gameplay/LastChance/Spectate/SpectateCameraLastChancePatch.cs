#nullable enable

using System;
using DeathHeadHopper.DeathHead;
using DeathHeadHopper.Helpers;
using DHHFLastChanceMode.Modules.Config;
using DHHFLastChanceMode.Modules.Utilities;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Spectate
{
    internal static class LastChanceSpectateHelper
    {
        private const string ForceSpectateLogKey = "LastChance.ForceDeathHeadSpectate";
        private const string DebugStateLogKey = "LastChance.SpectateState";
        private static bool s_forceComplete;
        private static DeathHeadController? s_cachedController;
        private static string? s_lastSpectateDebugMessage;

        internal static bool AllPlayersDisabled()
        {
            var director = GameDirector.instance;
            if (director == null || director.PlayerList == null || director.PlayerList.Count == 0)
            {
                return false;
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

        internal static void ForceDeathHeadSpectateIfPossible()
        {
            if (!FeatureFlags.LastChangeMode)
            {
                return;
            }

            if (s_forceComplete)
            {
                return;
            }

            if (!LogLimiter.ShouldLog(ForceSpectateLogKey, 30))
            {
                return;
            }

            if (!DHHFunc.LocalDeathHeadActive())
            {
                return;
            }

            var localAvatar = PlayerAvatar.instance;
            if (localAvatar != null)
            {
                if (!DHHFunc.IsDeathHeadSpectatable(localAvatar))
                {
                    return;
                }
            }

            var controller = TryGetLocalDeathHeadController();
            if (controller == null)
            {
                return;
            }
            s_cachedController = controller;

            if (IsSpectated(controller))
            {
                s_forceComplete = true;
                return;
            }

            var spectate = SpectateCamera.instance;
            if (spectate != null)
            {
                if (PlayerAvatar.instance != null)
                {
                    spectate.player = PlayerAvatar.instance;
                }
            }

            controller.SetSpectated(true);
            controller.UpdateSpectated();

            if (IsSpectated(controller))
            {
                s_forceComplete = true;
            }
        }

        internal static void ResetForceState()
        {
            s_forceComplete = false;
        }

        private static bool IsSpectated(DeathHeadController controller)
        {
            return controller.spectated;
        }

        internal static bool IsDeathHeadSpectated()
        {
            var controller = s_cachedController ?? TryGetLocalDeathHeadController();
            if (controller == null)
            {
                return false;
            }

            s_cachedController = controller;
            return IsSpectated(controller);
        }

        internal static bool IsManualSwitchInputDown()
        {
            return SemiFunc.InputDown(InputKey.Jump) ||
                   SemiFunc.InputDown(InputKey.SpectateNext) ||
                   SemiFunc.InputDown(InputKey.SpectatePrevious);
        }

        internal static void EnsureSpectatePlayerLocal(SpectateCamera spectate)
        {
            if (spectate == null)
            {
                return;
            }

            var local = PlayerAvatar.instance;
            if (local != null)
            {
                spectate.player = local;
            }
        }

        internal static void DebugLogState(SpectateCamera? spectate)
        {
            if (!FeatureFlags.DebugLogging)
            {
                return;
            }

            var local = PlayerAvatar.instance;
            var spectatePlayer = spectate != null ? spectate.player : null;
            var isSpectateLocal = spectatePlayer != null && local != null && ReferenceEquals(spectatePlayer, local);

            bool? localActive = null;
            bool? spectatable = null;
            bool? spectated = null;

            localActive = DHHFunc.LocalDeathHeadActive();

            if (local != null)
            {
                spectatable = DHHFunc.IsDeathHeadSpectatable(local);
            }

            var controller = s_cachedController ?? TryGetLocalDeathHeadController();
            if (controller != null)
            {
                s_cachedController = controller;
                spectated = controller.spectated;
            }

            var spName = spectatePlayer != null ? spectatePlayer.GetType().Name : "null";
            var lpName = local != null ? local.GetType().Name : "null";
            var message =
                $"[LastChance] SpectateState: spectatePlayer={spName} local={lpName} isSpectateLocal={isSpectateLocal} " +
                $"DHH.LocalActive={localActive} DHH.Spectatable={spectatable} DHH.Spectated={spectated}";
            if (string.Equals(s_lastSpectateDebugMessage, message, StringComparison.Ordinal))
            {
                return;
            }

            s_lastSpectateDebugMessage = message;
            UnityEngine.Debug.Log(message);
        }

        internal static bool ShouldForceLocalDeathHeadSpectate()
        {
            // If dead-player spectate is explicitly enabled in Always mode,
            // do not continuously re-force local spectate while everyone is disabled.
            if (FeatureFlags.SpectateDeadPlayers)
            {
                var mode = (FeatureFlags.SpectateDeadPlayersMode ?? string.Empty).Trim();
                if (mode.Equals("Always", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private static DeathHeadController? TryGetLocalDeathHeadController()
        {
            var local = PlayerAvatar.instance;
            if (local == null || local.playerDeathHead == null)
            {
                return null;
            }

            return DHHFunc.GetLocalDeathHeadController();
        }
    }
}


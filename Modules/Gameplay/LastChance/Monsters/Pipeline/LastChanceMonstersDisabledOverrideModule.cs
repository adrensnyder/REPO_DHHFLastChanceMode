#nullable enable

using System;
using System.Collections.Generic;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Support;
using HarmonyLib;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Pipeline
{
    [HarmonyPatch]
    internal static class LastChanceMonstersDisabledOverrideModule
    {
        private readonly struct DisabledOverrideScope : IDisposable
        {
            private readonly PlayerAvatar[] _players;
            private readonly bool[] _originalDisabled;
            private readonly int _count;

            internal DisabledOverrideScope(PlayerAvatar[] players, bool[] originalDisabled, int count)
            {
                _players = players;
                _originalDisabled = originalDisabled;
                _count = count;
            }

            internal static DisabledOverrideScope Enter()
            {
                if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
                {
                    return default;
                }

                var players = GameDirector.instance?.PlayerList;
                if (players == null || players.Count == 0)
                {
                    return default;
                }

                var patchedPlayers = new List<PlayerAvatar>(players.Count);
                var originalDisabled = new List<bool>(players.Count);

                for (var i = 0; i < players.Count; i++)
                {
                    var player = players[i];
                    if (player == null || !player.isDisabled)
                    {
                        continue;
                    }

                    if (!LastChanceMonstersDisabledGateHelper.ShouldTreatDisabledAsActive(player))
                    {
                        continue;
                    }

                    patchedPlayers.Add(player);
                    originalDisabled.Add(player.isDisabled);
                    player.isDisabled = false;
                }

                if (patchedPlayers.Count == 0)
                {
                    return default;
                }

                return new DisabledOverrideScope(patchedPlayers.ToArray(), originalDisabled.ToArray(), patchedPlayers.Count);
            }

            public void Dispose()
            {
                if (_players == null || _originalDisabled == null || _count <= 0)
                {
                    return;
                }

                for (var i = 0; i < _count; i++)
                {
                    var player = _players[i];
                    if (player == null)
                    {
                        continue;
                    }

                    player.isDisabled = _originalDisabled[i];
                }
            }
        }

        private static DisabledOverrideScope EnterScope()
        {
            return DisabledOverrideScope.Enter();
        }

        private static Exception? ExitScope(DisabledOverrideScope __state, Exception? __exception)
        {
            __state.Dispose();
            return __exception;
        }

        [HarmonyPatch(typeof(EnemyBangDirector), nameof(EnemyBangDirector.StateAttackPlayer))]
        [HarmonyPrefix]
        private static void EnemyBangDirectorStateAttackPlayerPrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemyBangDirector), nameof(EnemyBangDirector.StateAttackPlayer))]
        [HarmonyFinalizer]
        private static Exception? EnemyBangDirectorStateAttackPlayerFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemyBirthdayBoy), nameof(EnemyBirthdayBoy.Update))]
        [HarmonyPrefix]
        private static void EnemyBirthdayBoyUpdatePrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemyBirthdayBoy), nameof(EnemyBirthdayBoy.Update))]
        [HarmonyFinalizer]
        private static Exception? EnemyBirthdayBoyUpdateFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemyBombThrower), nameof(EnemyBombThrower.StateGotoPlayer))]
        [HarmonyPrefix]
        private static void EnemyBombThrowerStateGotoPlayerPrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemyBombThrower), nameof(EnemyBombThrower.StateGotoPlayer))]
        [HarmonyFinalizer]
        private static Exception? EnemyBombThrowerStateGotoPlayerFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemyBombThrower), nameof(EnemyBombThrower.StateBackAwayPlayer))]
        [HarmonyPrefix]
        private static void EnemyBombThrowerStateBackAwayPlayerPrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemyBombThrower), nameof(EnemyBombThrower.StateBackAwayPlayer))]
        [HarmonyFinalizer]
        private static Exception? EnemyBombThrowerStateBackAwayPlayerFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemyBombThrower), nameof(EnemyBombThrower.StateBackAwayHead))]
        [HarmonyPrefix]
        private static void EnemyBombThrowerStateBackAwayHeadPrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemyBombThrower), nameof(EnemyBombThrower.StateBackAwayHead))]
        [HarmonyFinalizer]
        private static Exception? EnemyBombThrowerStateBackAwayHeadFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemyBombThrowerHead), nameof(EnemyBombThrowerHead.StateSpawn), new[] { typeof(bool) })]
        [HarmonyPrefix]
        private static void EnemyBombThrowerHeadStateSpawnPrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemyBombThrowerHead), nameof(EnemyBombThrowerHead.StateSpawn), new[] { typeof(bool) })]
        [HarmonyFinalizer]
        private static Exception? EnemyBombThrowerHeadStateSpawnFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemyBombThrowerHead), nameof(EnemyBombThrowerHead.StateActive), new[] { typeof(bool) })]
        [HarmonyPrefix]
        private static void EnemyBombThrowerHeadStateActivePrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemyBombThrowerHead), nameof(EnemyBombThrowerHead.StateActive), new[] { typeof(bool) })]
        [HarmonyFinalizer]
        private static Exception? EnemyBombThrowerHeadStateActiveFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemyBombThrowerHead), nameof(EnemyBombThrowerHead.EyeLogic))]
        [HarmonyPrefix]
        private static void EnemyBombThrowerHeadEyeLogicPrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemyBombThrowerHead), nameof(EnemyBombThrowerHead.EyeLogic))]
        [HarmonyFinalizer]
        private static Exception? EnemyBombThrowerHeadEyeLogicFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemyDuck), nameof(EnemyDuck.StateGoToPlayer))]
        [HarmonyPrefix]
        private static void EnemyDuckStateGoToPlayerPrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemyDuck), nameof(EnemyDuck.StateGoToPlayer))]
        [HarmonyFinalizer]
        private static Exception? EnemyDuckStateGoToPlayerFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemyDuck), nameof(EnemyDuck.StateGoToPlayerUnder))]
        [HarmonyPrefix]
        private static void EnemyDuckStateGoToPlayerUnderPrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemyDuck), nameof(EnemyDuck.StateGoToPlayerUnder))]
        [HarmonyFinalizer]
        private static Exception? EnemyDuckStateGoToPlayerUnderFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemyDuck), nameof(EnemyDuck.StateGoToPlayerOver))]
        [HarmonyPrefix]
        private static void EnemyDuckStateGoToPlayerOverPrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemyDuck), nameof(EnemyDuck.StateGoToPlayerOver))]
        [HarmonyFinalizer]
        private static Exception? EnemyDuckStateGoToPlayerOverFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemyDuck), nameof(EnemyDuck.HeadLookAtLogic))]
        [HarmonyPrefix]
        private static void EnemyDuckHeadLookAtLogicPrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemyDuck), nameof(EnemyDuck.HeadLookAtLogic))]
        [HarmonyFinalizer]
        private static Exception? EnemyDuckHeadLookAtLogicFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemyDuck), nameof(EnemyDuck.ChaseStop))]
        [HarmonyPrefix]
        private static void EnemyDuckChaseStopPrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemyDuck), nameof(EnemyDuck.ChaseStop))]
        [HarmonyFinalizer]
        private static Exception? EnemyDuckChaseStopFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemyElsa), nameof(EnemyElsa.Update))]
        [HarmonyPrefix]
        private static void EnemyElsaUpdatePrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemyElsa), nameof(EnemyElsa.Update))]
        [HarmonyFinalizer]
        private static Exception? EnemyElsaUpdateFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemyElsa), nameof(EnemyElsa.StateGoToPlayerSmall))]
        [HarmonyPrefix]
        private static void EnemyElsaStateGoToPlayerSmallPrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemyElsa), nameof(EnemyElsa.StateGoToPlayerSmall))]
        [HarmonyFinalizer]
        private static Exception? EnemyElsaStateGoToPlayerSmallFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemyElsa), nameof(EnemyElsa.StateGoToPlayerUnderSmall))]
        [HarmonyPrefix]
        private static void EnemyElsaStateGoToPlayerUnderSmallPrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemyElsa), nameof(EnemyElsa.StateGoToPlayerUnderSmall))]
        [HarmonyFinalizer]
        private static Exception? EnemyElsaStateGoToPlayerUnderSmallFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemyElsa), nameof(EnemyElsa.StateLookUnderBig))]
        [HarmonyPrefix]
        private static void EnemyElsaStateLookUnderBigPrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemyElsa), nameof(EnemyElsa.StateLookUnderBig))]
        [HarmonyFinalizer]
        private static Exception? EnemyElsaStateLookUnderBigFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemyElsa), nameof(EnemyElsa.ChaseStop))]
        [HarmonyPrefix]
        private static void EnemyElsaChaseStopPrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemyElsa), nameof(EnemyElsa.ChaseStop))]
        [HarmonyFinalizer]
        private static Exception? EnemyElsaChaseStopFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemyGnomeDirector), nameof(EnemyGnomeDirector.StateAttackPlayer))]
        [HarmonyPrefix]
        private static void EnemyGnomeDirectorStateAttackPlayerPrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemyGnomeDirector), nameof(EnemyGnomeDirector.StateAttackPlayer))]
        [HarmonyFinalizer]
        private static Exception? EnemyGnomeDirectorStateAttackPlayerFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemyHeadGrabber), nameof(EnemyHeadGrabber.GotoLogic))]
        [HarmonyPrefix]
        private static void EnemyHeadGrabberGotoLogicPrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemyHeadGrabber), nameof(EnemyHeadGrabber.GotoLogic))]
        [HarmonyFinalizer]
        private static Exception? EnemyHeadGrabberGotoLogicFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemyHeadGrabber), nameof(EnemyHeadGrabber.GotoOverLogic))]
        [HarmonyPrefix]
        private static void EnemyHeadGrabberGotoOverLogicPrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemyHeadGrabber), nameof(EnemyHeadGrabber.GotoOverLogic))]
        [HarmonyFinalizer]
        private static Exception? EnemyHeadGrabberGotoOverLogicFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemyHidden), nameof(EnemyHidden.StatePlayerGoTo))]
        [HarmonyPrefix]
        private static void EnemyHiddenStatePlayerGoToPrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemyHidden), nameof(EnemyHidden.StatePlayerGoTo))]
        [HarmonyFinalizer]
        private static Exception? EnemyHiddenStatePlayerGoToFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemyHidden), nameof(EnemyHidden.StatePlayerPickup))]
        [HarmonyPrefix]
        private static void EnemyHiddenStatePlayerPickupPrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemyHidden), nameof(EnemyHidden.StatePlayerPickup))]
        [HarmonyFinalizer]
        private static Exception? EnemyHiddenStatePlayerPickupFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemyHidden), nameof(EnemyHidden.StatePlayerMove))]
        [HarmonyPrefix]
        private static void EnemyHiddenStatePlayerMovePrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemyHidden), nameof(EnemyHidden.StatePlayerMove))]
        [HarmonyFinalizer]
        private static Exception? EnemyHiddenStatePlayerMoveFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemyHidden), nameof(EnemyHidden.StatePlayerRelease))]
        [HarmonyPrefix]
        private static void EnemyHiddenStatePlayerReleasePrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemyHidden), nameof(EnemyHidden.StatePlayerRelease))]
        [HarmonyFinalizer]
        private static Exception? EnemyHiddenStatePlayerReleaseFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemyShadow), nameof(EnemyShadow.Update))]
        [HarmonyPrefix]
        private static void EnemyShadowUpdatePrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemyShadow), nameof(EnemyShadow.Update))]
        [HarmonyFinalizer]
        private static Exception? EnemyShadowUpdateFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemyShadow), nameof(EnemyShadow.StateChooseTarget))]
        [HarmonyPrefix]
        private static void EnemyShadowStateChooseTargetPrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemyShadow), nameof(EnemyShadow.StateChooseTarget))]
        [HarmonyFinalizer]
        private static Exception? EnemyShadowStateChooseTargetFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemySlowMouth), nameof(EnemySlowMouth.DetatchLogic))]
        [HarmonyPrefix]
        private static void EnemySlowMouthDetatchLogicPrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemySlowMouth), nameof(EnemySlowMouth.DetatchLogic))]
        [HarmonyFinalizer]
        private static Exception? EnemySlowMouthDetatchLogicFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemySlowMouth), nameof(EnemySlowMouth.StateAttached), new[] { typeof(bool) })]
        [HarmonyPrefix]
        private static void EnemySlowMouthStateAttachedPrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemySlowMouth), nameof(EnemySlowMouth.StateAttached), new[] { typeof(bool) })]
        [HarmonyFinalizer]
        private static Exception? EnemySlowMouthStateAttachedFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemySlowMouth), nameof(EnemySlowMouth.StateIdlePuke), new[] { typeof(bool) })]
        [HarmonyPrefix]
        private static void EnemySlowMouthStateIdlePukePrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemySlowMouth), nameof(EnemySlowMouth.StateIdlePuke), new[] { typeof(bool) })]
        [HarmonyFinalizer]
        private static Exception? EnemySlowMouthStateIdlePukeFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemySlowMouth), nameof(EnemySlowMouth.StateGoToPlayerOver), new[] { typeof(bool) })]
        [HarmonyPrefix]
        private static void EnemySlowMouthStateGoToPlayerOverPrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemySlowMouth), nameof(EnemySlowMouth.StateGoToPlayerOver), new[] { typeof(bool) })]
        [HarmonyFinalizer]
        private static Exception? EnemySlowMouthStateGoToPlayerOverFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemySlowMouth), nameof(EnemySlowMouth.StateGoToPlayerUnder), new[] { typeof(bool) })]
        [HarmonyPrefix]
        private static void EnemySlowMouthStateGoToPlayerUnderPrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemySlowMouth), nameof(EnemySlowMouth.StateGoToPlayerUnder), new[] { typeof(bool) })]
        [HarmonyFinalizer]
        private static Exception? EnemySlowMouthStateGoToPlayerUnderFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemySlowMouth), nameof(EnemySlowMouth.TargettingPlayer))]
        [HarmonyPrefix]
        private static void EnemySlowMouthTargettingPlayerPrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemySlowMouth), nameof(EnemySlowMouth.TargettingPlayer))]
        [HarmonyFinalizer]
        private static Exception? EnemySlowMouthTargettingPlayerFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemyTricycle), nameof(EnemyTricycle.StateStateBeforeAttack))]
        [HarmonyPrefix]
        private static void EnemyTricycleStateStateBeforeAttackPrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemyTricycle), nameof(EnemyTricycle.StateStateBeforeAttack))]
        [HarmonyFinalizer]
        private static Exception? EnemyTricycleStateStateBeforeAttackFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemyTricycle), nameof(EnemyTricycle.StateAttackDive))]
        [HarmonyPrefix]
        private static void EnemyTricycleStateAttackDivePrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemyTricycle), nameof(EnemyTricycle.StateAttackDive))]
        [HarmonyFinalizer]
        private static Exception? EnemyTricycleStateAttackDiveFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemyTricycle), nameof(EnemyTricycle.FixedUpdateAttackDive))]
        [HarmonyPrefix]
        private static void EnemyTricycleFixedUpdateAttackDivePrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemyTricycle), nameof(EnemyTricycle.FixedUpdateAttackDive))]
        [HarmonyFinalizer]
        private static Exception? EnemyTricycleFixedUpdateAttackDiveFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemyValuableThrower), nameof(EnemyValuableThrower.TargetFailsafe))]
        [HarmonyPrefix]
        private static void EnemyValuableThrowerTargetFailsafePrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemyValuableThrower), nameof(EnemyValuableThrower.TargetFailsafe))]
        [HarmonyFinalizer]
        private static Exception? EnemyValuableThrowerTargetFailsafeFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemyCeilingEye), nameof(EnemyCeilingEye.StateHasTarget))]
        [HarmonyPrefix]
        private static void EnemyCeilingEyeStateHasTargetPrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemyCeilingEye), nameof(EnemyCeilingEye.StateHasTarget))]
        [HarmonyFinalizer]
        private static Exception? EnemyCeilingEyeStateHasTargetFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemyCeilingEye), nameof(EnemyCeilingEye.TargetFailSafe))]
        [HarmonyPrefix]
        private static void EnemyCeilingEyeTargetFailSafePrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemyCeilingEye), nameof(EnemyCeilingEye.TargetFailSafe))]
        [HarmonyFinalizer]
        private static Exception? EnemyCeilingEyeTargetFailSafeFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemySpinny), nameof(EnemySpinny.Update))]
        [HarmonyPrefix]
        private static void EnemySpinnyUpdatePrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemySpinny), nameof(EnemySpinny.Update))]
        [HarmonyFinalizer]
        private static Exception? EnemySpinnyUpdateFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemyOogly), nameof(EnemyOogly.StatePlayerSpotted))]
        [HarmonyPrefix]
        private static void EnemyOoglyStatePlayerSpottedPrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemyOogly), nameof(EnemyOogly.StatePlayerSpotted))]
        [HarmonyFinalizer]
        private static Exception? EnemyOoglyStatePlayerSpottedFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);

        [HarmonyPatch(typeof(EnemyOogly), nameof(EnemyOogly.StateWrestlePlayer))]
        [HarmonyPrefix]
        private static void EnemyOoglyStateWrestlePlayerPrefix(out DisabledOverrideScope __state) => __state = EnterScope();
        [HarmonyPatch(typeof(EnemyOogly), nameof(EnemyOogly.StateWrestlePlayer))]
        [HarmonyFinalizer]
        private static Exception? EnemyOoglyStateWrestlePlayerFinalizer(DisabledOverrideScope __state, Exception? __exception) => ExitScope(__state, __exception);
    }
}

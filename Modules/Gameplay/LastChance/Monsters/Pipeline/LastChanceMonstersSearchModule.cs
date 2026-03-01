#nullable enable

using BepInEx.Logging;
using DHHFLastChanceMode.Modules.Config;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Support;
using HarmonyLib;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Pipeline
{
    internal static class LastChanceMonstersSearchModule
    {
        private static readonly ManualLogSource Log = Logger.CreateLogSource("DHHFLastChanceMode.LastChance.MonstersSearch");
        private static bool s_typedPatchesApplied;

        internal static void ResetRuntimeState()
        {
            // No local runtime cache needed in typed-only mode.
        }

        internal static void Apply(Harmony harmony)
        {
            if (s_typedPatchesApplied || harmony == null)
            {
                return;
            }

            harmony.CreateClassProcessor(typeof(EnemySetChaseTargetPatch)).Patch();
            harmony.CreateClassProcessor(typeof(EnemyStateSneakUpdatePatch)).Patch();
            s_typedPatchesApplied = true;

            if (FeatureFlags.DebugLogging)
            {
                Log.LogInfo("[LastChance] MonstersSearch typed patches applied.");
            }
        }

        internal static void Unapply()
        {
            // Typed patches remain installed and are runtime-gated.
            ResetRuntimeState();
        }

        internal static int GetAliveSearchMonsterCount()
        {
            if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
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
                if (enemyParent == null || !enemyParent.Spawned || enemyParent.forceLeave)
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        [HarmonyPatch(typeof(Enemy), nameof(Enemy.SetChaseTarget), new[] { typeof(PlayerAvatar) })]
        internal static class EnemySetChaseTargetPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(Enemy __instance, PlayerAvatar playerAvatar)
            {
                if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
                {
                    return true;
                }

                if (!LastChanceMonstersDisabledGateHelper.ShouldTreatDisabledAsActive(playerAvatar))
                {
                    return true;
                }

                if (EnemyDirector.instance.debugNoVision ||
                    __instance.DisableChaseTimer > 0f ||
                    !__instance.HasVision)
                {
                    return false;
                }

                if (__instance.Vision.DisableTimer > 0f)
                {
                    return false;
                }

                __instance.Vision.VisionTrigger(playerAvatar.photonView.ViewID, playerAvatar, false, false);
                if (!__instance.HasStateChase)
                {
                    return false;
                }

                if (!__instance.CheckChase() || __instance.CurrentState == EnemyState.ChaseSlow)
                {
                    __instance.CurrentState = EnemyState.ChaseBegin;
                    __instance.TargetPlayerViewID = playerAvatar.photonView.ViewID;
                    __instance.TargetPlayerAvatar = playerAvatar;
                }

                return false;
            }
        }

        [HarmonyPatch(typeof(EnemyStateSneak), nameof(EnemyStateSneak.Update))]
        internal static class EnemyStateSneakUpdatePatch
        {
            [HarmonyPrefix]
            private static bool Prefix(EnemyStateSneak __instance)
            {
                if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
                {
                    return true;
                }

                ExecuteSneakUpdate(__instance);
                return false;
            }
        }

        private static void ExecuteSneakUpdate(EnemyStateSneak state)
        {
            if (state.Enemy.CurrentState != EnemyState.Sneak)
            {
                if (state.Active)
                {
                    state.Active = false;
                }

                return;
            }

            if (!state.Active)
            {
                state.TargetPlayer = PlayerController.instance.playerAvatarScript;
                if (GameManager.instance.gameMode == 1)
                {
                    foreach (var playerAvatar in GameDirector.instance.PlayerList)
                    {
                        if ((!playerAvatar.isDisabled || LastChanceMonstersDisabledGateHelper.ShouldTreatDisabledAsActive(playerAvatar)) &&
                            playerAvatar.photonView.ViewID == state.Enemy.TargetPlayerViewID)
                        {
                            state.TargetPlayer = playerAvatar;
                            break;
                        }
                    }
                }

                state.StateTimer = Random.Range(state.StateTimeMin, state.StateTimeMax);
                state.Active = true;
            }

            if (!state.Enemy.MasterClient)
            {
                return;
            }

            state.Enemy.NavMeshAgent.UpdateAgent(state.Speed, state.Acceleration);
            if (state.TargetPlayer != null)
            {
                var targetPosition = LastChanceMonstersTargetingOrchestrator.ResolveEffectiveTransformTargetPoint(state.TargetPlayer.transform);
                state.Enemy.NavMeshAgent.SetDestination(targetPosition);
            }

            if (state.Enemy.HasRigidbody)
            {
                state.Enemy.Rigidbody.IdleSet(0.1f);
            }

            if (state.Enemy.TargetPlayerAvatar != null &&
                state.Enemy.Vision.VisionsTriggered[state.Enemy.TargetPlayerAvatar.photonView.ViewID] >= state.Enemy.Vision.VisionsToTrigger)
            {
                state.StateTimer = Random.Range(state.StateTimeMin, state.StateTimeMax);
            }

            state.StateTimer -= Time.deltaTime;
            if (state.StateTimer <= 0f)
            {
                state.Enemy.CurrentState = EnemyState.Roaming;
            }
        }
    }
}

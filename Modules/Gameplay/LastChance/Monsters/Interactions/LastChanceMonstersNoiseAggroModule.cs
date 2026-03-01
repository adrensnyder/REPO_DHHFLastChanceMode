#nullable enable

using System;
using System.Collections.Generic;
using BepInEx.Logging;
using DHHFLastChanceMode.Modules.Config;
using DHHFLastChanceMode.Modules.Utilities;
using DeathHeadHopper.DeathHead;
using DeathHeadHopper.DeathHead.Handlers;
using HarmonyLib;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Interactions
{
    internal static class LastChanceMonstersNoiseAggroModule
    {
        private const float DefaultAggroRadius = 18f;
        private const float DefaultAggroCooldown = 0.75f;

        private static readonly ManualLogSource Log = Logger.CreateLogSource("DHHFLastChanceMode.LastChance.Monsters.NoiseAggro");
        private static readonly Dictionary<int, float> s_lastAggroByPlayerViewId = new();

        internal static void ResetRuntimeState()
        {
            s_lastAggroByPlayerViewId.Clear();
        }

        internal static void Apply(Harmony harmony)
        {
            // Typed patch is registered in LastChanceHarmonyPatchRegistry.
        }

        internal static void Unapply()
        {
            ResetRuntimeState();
        }

        [HarmonyPatch(typeof(ChargeHandler), nameof(ChargeHandler.ChargeWindup), new[] { typeof(Vector3) })]
        [HarmonyPostfix]
        private static void ChargeWindupPostfix(ChargeHandler __instance)
        {
            if (__instance == null ||
                !LastChanceMonstersTargetProxyHelper.IsRuntimeMasterContextEnabled())
            {
                return;
            }

            var player = TryGetOwnerPlayer(__instance);
            if (player == null)
            {
                return;
            }

            if (!LastChanceMonstersTargetProxyHelper.TryGetHeadProxyTarget(player, out var headCenter))
            {
                return;
            }

            var viewId = player.photonView.ViewID;
            var now = Time.unscaledTime;
            if (s_lastAggroByPlayerViewId.TryGetValue(viewId, out var last) && now - last < DefaultAggroCooldown)
            {
                return;
            }

            s_lastAggroByPlayerViewId[viewId] = now;

            foreach (var enemy in LastChanceMonstersTargetProxyHelper.EnumerateEnemies())
            {
                if (enemy == null)
                {
                    continue;
                }

                if (enemy.GetComponent<EnemyAnimal>() != null)
                {
                    continue;
                }

                var dist = Vector3.Distance(enemy.transform.position, headCenter);
                if (dist > DefaultAggroRadius)
                {
                    continue;
                }

                var hasInvestigate = enemy.HasStateInvestigate;
                var investigate = enemy.StateInvestigate;
                if (hasInvestigate && investigate != null)
                {
                    investigate.Set(headCenter, false);
                }

                var hasVision = enemy.HasVision;
                var vision = enemy.Vision;
                if (hasVision && vision != null)
                {
                    var near = dist <= vision.VisionDistanceClose;
                    LastChanceMonstersTargetProxyHelper.EnsureVisionTriggered(vision, player, near);
                }

                // SetChaseTarget has internal disabled checks remapped by MonstersSearch module during LastChance.
                enemy.SetChaseTarget(player);
            }

            if (FeatureFlags.DebugLogging && LogLimiter.ShouldLog("LastChance.NoiseAggro.Trigger", 30))
            {
                Log.LogInfo($"[LastChance] NoiseAggro triggered by charge windup. player={player.photonView.ViewID} pos={headCenter}");
            }
        }

        private static PlayerAvatar? TryGetOwnerPlayer(ChargeHandler chargeHandler)
        {
            var controller = chargeHandler.controller;
            if (controller == null)
            {
                return null;
            }

            var deathHead = controller.deathHead;
            return deathHead?.playerAvatar;
        }
    }
}


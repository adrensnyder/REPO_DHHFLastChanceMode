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
        private const string PatchId = "DHHFLastChanceMode.Gameplay.LastChance.Monsters.NoiseAggro";
        private const float DefaultAggroRadius = 18f;
        private const float DefaultAggroCooldown = 0.75f;

        private static readonly ManualLogSource Log = Logger.CreateLogSource("DHHFLastChanceMode.LastChance.Monsters.NoiseAggro");
        private static readonly Dictionary<int, float> s_lastAggroByPlayerViewId = new();
        private static Harmony? s_harmony;
        private static bool s_applied;

        internal static void ResetRuntimeState()
        {
            s_lastAggroByPlayerViewId.Clear();
        }

        internal static void Apply(Harmony harmony, System.Reflection.Assembly asm)
        {
            if (s_applied || harmony == null || asm == null)
            {
                return;
            }

            var windupMethod = AccessTools.Method(typeof(ChargeHandler), "ChargeWindup", new[] { typeof(Vector3) });

            if (windupMethod == null)
            {
                if (FeatureFlags.DebugLogging && LogLimiter.ShouldLog("LastChance.NoiseAggro.Missing", 120))
                {
                    Log.LogWarning("[LastChance] NoiseAggro skipped: ChargeHandler.ChargeWindup not found.");
                }
                return;
            }

            s_harmony = new Harmony(PatchId);
            var postfix = new HarmonyMethod(typeof(LastChanceMonstersNoiseAggroModule), nameof(ChargeWindupPostfix));
            s_harmony.Patch(windupMethod, postfix: postfix);
            s_applied = true;
        }

        internal static void Unapply()
        {
            if (!s_applied || s_harmony == null)
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

            s_applied = false;
            s_harmony = null;
            ResetRuntimeState();
        }

        private static void ChargeWindupPostfix(ChargeHandler __instance)
        {
            if (__instance == null ||
                !LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled() ||
                !LastChanceMonstersTargetProxyHelper.IsMasterContext())
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


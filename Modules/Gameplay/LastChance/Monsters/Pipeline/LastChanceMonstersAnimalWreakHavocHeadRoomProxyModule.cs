#nullable enable

using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using DHHFLastChanceMode.Modules.Config;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Interactions.Debugging;
using DHHFLastChanceMode.Modules.Utilities;
using HarmonyLib;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Pipeline
{
    [HarmonyPatch(typeof(SemiFunc), "LevelPointInTargetRoomGet")]
    internal static class LastChanceMonstersAnimalWreakHavocHeadRoomProxyModule
    {
        private static readonly ManualLogSource Log = Logger.CreateLogSource("DHHFLastChanceMode.LastChance.AnimalRoomProxy");
        private static readonly FieldInfo? RoomVolumeCheckPlayerField = AccessTools.Field(typeof(RoomVolumeCheck), "player");
        private static readonly FieldInfo? PlayerNameField = AccessTools.Field(typeof(PlayerAvatar), "playerName");
        private static int s_animalWreakHavocDepth;

        [HarmonyPatch(typeof(EnemyAnimal), "StateWreakHavoc")]
        [HarmonyPrefix]
        private static void EnemyAnimalStateWreakHavocPrefix()
        {
            s_animalWreakHavocDepth++;
        }

        [HarmonyPatch(typeof(EnemyAnimal), "StateWreakHavoc")]
        [HarmonyPostfix]
        private static void EnemyAnimalStateWreakHavocPostfix()
        {
            if (s_animalWreakHavocDepth > 0)
            {
                s_animalWreakHavocDepth--;
            }
        }

        [HarmonyPrefix]
        private static bool Prefix(RoomVolumeCheck _target, float _minDistance, float _maxDistance, LevelPoint ignorePoint, ref LevelPoint __result)
        {
            __result = default!;

            if (_target == null ||
                !LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled() ||
                !LastChanceMonstersTargetProxyHelper.IsMasterContext())
            {
                return true;
            }

            // Keep this proxy strictly scoped to EnemyAnimal WreakHavoc to avoid side effects
            // on other monsters that also use LevelPointInTargetRoomGet.
            if (s_animalWreakHavocDepth <= 0)
            {
                return true;
            }

            var player = ResolveTargetPlayer(_target);
            if (player == null || !LastChanceMonstersTargetProxyHelper.IsHeadProxyActive(player))
            {
                return true;
            }

            if (!LastChanceMonstersTargetProxyHelper.TryGetHeadProxyTarget(player, out var headCenter))
            {
                return true;
            }

            var points = LevelGenerator.Instance?.LevelPathPoints;
            if (points == null || points.Count == 0)
            {
                return true;
            }

            var rooms = CollectHeadRooms(headCenter);
            if (rooms.Count == 0 && _target.CurrentRooms != null)
            {
                for (var i = 0; i < _target.CurrentRooms.Count; i++)
                {
                    var room = _target.CurrentRooms[i];
                    if (room != null)
                    {
                        rooms.Add(room);
                    }
                }
            }
            if (rooms.Count == 0)
            {
                if (LastChanceMonstersDebugGate.IsEnabled(InternalDebugFlags.DebugLastChanceAnimalCollisionFlow))
                {
                    LogDebug($"no rooms from head overlaps player={GetPlayerName(player)} head={headCenter}");
                }

                return true;
            }

            var candidates = new List<LevelPoint>();
            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];
                if (point == null || point == ignorePoint || point.Room == null || !rooms.Contains(point.Room))
                {
                    continue;
                }

                var distance = Vector3.Distance(point.transform.position, headCenter);
                if (distance < _minDistance || distance > _maxDistance)
                {
                    continue;
                }

                candidates.Add(point);
            }

            if (candidates.Count == 0)
            {
                // Fallback: when room mapping is sparse/inconsistent, keep WreakHavoc alive by
                // selecting from global level points using head distance constraints.
                for (var i = 0; i < points.Count; i++)
                {
                    var point = points[i];
                    if (point == null || point == ignorePoint || point.Room == null || point.Room.Truck)
                    {
                        continue;
                    }

                    var distance = Vector3.Distance(point.transform.position, headCenter);
                    if (distance < _minDistance || distance > _maxDistance)
                    {
                        continue;
                    }

                    candidates.Add(point);
                }
            }

            if (candidates.Count == 0)
            {
                if (LastChanceMonstersDebugGate.IsEnabled(InternalDebugFlags.DebugLastChanceAnimalCollisionFlow))
                {
                    LogDebug(
                        $"no candidates player={GetPlayerName(player)} head={headCenter} rooms={rooms.Count} range={_minDistance:F1}-{_maxDistance:F1}");
                }

                return true;
            }

            __result = candidates[Random.Range(0, candidates.Count)];
            if (LastChanceMonstersDebugGate.IsEnabled(InternalDebugFlags.DebugLastChanceAnimalCollisionFlow))
            {
                LogDebug(
                    $"proxy hit player={GetPlayerName(player)} head={headCenter} rooms={rooms.Count} candidates={candidates.Count} chosen={__result.transform.position}");
            }

            return false;
        }

        private static PlayerAvatar? ResolveTargetPlayer(RoomVolumeCheck target)
        {
            if (target == null)
            {
                return null;
            }

            var direct = target.GetComponentInParent<PlayerAvatar>();
            if (direct != null)
            {
                return direct;
            }

            return RoomVolumeCheckPlayerField?.GetValue(target) as PlayerAvatar;
        }

        private static HashSet<RoomVolume> CollectHeadRooms(Vector3 headCenter)
        {
            var rooms = new HashSet<RoomVolume>();
            var overlaps = Physics.OverlapSphere(headCenter, 1.1f, LayerMask.GetMask("RoomVolume"), QueryTriggerInteraction.Collide);
            for (var i = 0; i < overlaps.Length; i++)
            {
                var overlap = overlaps[i];
                if (overlap == null)
                {
                    continue;
                }

                var room = overlap.GetComponent<RoomVolume>() ?? overlap.GetComponentInParent<RoomVolume>();
                if (room != null)
                {
                    rooms.Add(room);
                }
            }

            return rooms;
        }

        private static string GetPlayerName(PlayerAvatar player)
        {
            if (player == null)
            {
                return "n/a";
            }

            var reflected = PlayerNameField?.GetValue(player) as string;
            if (!string.IsNullOrWhiteSpace(reflected))
            {
                return reflected!;
            }

            return player.photonView != null ? $"view:{player.photonView.ViewID}" : "n/a";
        }

        private static void LogDebug(string message)
        {
            if (!LastChanceMonstersDebugGate.IsVerbose(InternalDebugFlags.DebugLastChanceAnimalCollisionVerbose) &&
                !LogLimiter.ShouldLog("AnimalRoomProxy.Trace", 2))
            {
                return;
            }

            Log.LogInfo($"[Animal][RoomProxy] {message}");
        }
    }
}

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx.Logging;
using DHHFLastChanceMode.Modules.Config;
using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;
using Logger = BepInEx.Logging.Logger;

namespace DHHFLastChanceMode.Modules.Utilities
{
    internal static class PlayerTruckDistanceHelper
    {
        private static readonly ManualLogSource Log = Logger.CreateLogSource("DHHFLastChanceMode.PlayerTruckDistance");
        private const float HeightCacheTtlSeconds = 2f;

        [Flags]
        internal enum DistanceQueryFields
        {
            None = 0,
            Height = 1 << 0,
            NavMeshDistance = 1 << 1,
            RoomPath = 1 << 2,
            All = Height | NavMeshDistance | RoomPath
        }

        internal readonly struct PlayerTruckDistance
        {
            internal PlayerTruckDistance(PlayerAvatar playerAvatar, float navMeshDistance, float heightDelta, int shortestRoomPathToTruck, int totalMapRooms, bool hasValidPath)
            {
                PlayerAvatar = playerAvatar;
                NavMeshDistance = navMeshDistance;
                HeightDelta = heightDelta;
                ShortestRoomPathToTruck = shortestRoomPathToTruck;
                TotalMapRooms = totalMapRooms;
                HasValidPath = hasValidPath;
            }

            internal PlayerAvatar PlayerAvatar { get; }
            internal float NavMeshDistance { get; }
            internal float HeightDelta { get; }
            internal int ShortestRoomPathToTruck { get; }
            internal int TotalMapRooms { get; }
            internal bool HasValidPath { get; }
        }

        internal readonly struct PlayerTruckRouteAssessment
        {
            internal PlayerTruckRouteAssessment(
                Vector3 playerWorldPosition,
                Vector3 truckWorldPosition,
                Vector3 navMeshFrom,
                Vector3 navMeshTo,
                float navMeshDistance,
                bool hasValidPath,
                Vector3[] pathCorners)
            {
                PlayerWorldPosition = playerWorldPosition;
                TruckWorldPosition = truckWorldPosition;
                NavMeshFrom = navMeshFrom;
                NavMeshTo = navMeshTo;
                NavMeshDistance = navMeshDistance;
                HasValidPath = hasValidPath;
                PathCorners = pathCorners;
            }

            internal Vector3 PlayerWorldPosition { get; }
            internal Vector3 TruckWorldPosition { get; }
            internal Vector3 NavMeshFrom { get; }
            internal Vector3 NavMeshTo { get; }
            internal float NavMeshDistance { get; }
            internal bool HasValidPath { get; }
            internal Vector3[] PathCorners { get; }
        }

        private sealed class CachedPlayerDistance
        {
            internal float NavMeshDistance = -1f;
            internal float HeightDelta;
            internal int ShortestRoomPathToTruck = -1;
            internal int TotalMapRooms = -1;
            internal bool HasValidPath;
            internal int RoomHash;
            internal float HeightUpdatedAt = float.NegativeInfinity;
            internal int LevelStamp;
            internal Vector3 LastKnownWorldPosition;
        }

        private static LevelGenerator? s_cachedGraphLevelGenerator;
        private static int s_cachedGraphPointCount;
        private static Dictionary<RoomVolume, HashSet<RoomVolume>>? s_cachedRoomGraph;
        private static readonly Dictionary<int, CachedPlayerDistance> s_playerCache = new();
        private static readonly Dictionary<int, RemotePlayerHint> s_remoteHints = new();
        private static LevelGenerator? s_cachedLevelGeneratorForPlayers;
        private static Vector3 s_cachedTruckPosition;
        private static bool s_hasCachedTruckPosition;
        private static int s_cachedLevelPointsCount;
        private static bool s_activationProfilingEnabled;
        private static ActivationProfileStats s_activationProfileStats;

        private readonly struct RemotePlayerHint
        {
            internal RemotePlayerHint(int roomHash, float heightDelta, int levelStamp, float updatedAt)
            {
                RoomHash = roomHash;
                HeightDelta = heightDelta;
                LevelStamp = levelStamp;
                UpdatedAt = updatedAt;
            }

            internal int RoomHash { get; }
            internal float HeightDelta { get; }
            internal int LevelStamp { get; }
            internal float UpdatedAt { get; }
        }

        private struct ActivationProfileStats
        {
            internal int Calls;
            internal int PlayersProcessed;
            internal int NavRefreshCount;
            internal int RoomRefreshCount;
            internal int RemoteHintUsedCount;
            internal float TotalMs;
            internal float SetupMs;
            internal float LoopMs;
            internal float MaxCallMs;
        }

        internal static void PrimeDistancesCache()
        {
            _ = GetDistancesFromTruck(DistanceQueryFields.NavMeshDistance | DistanceQueryFields.RoomPath);
        }

        internal static PlayerTruckDistance[] GetDistancesFromTruck()
        {
            return GetDistancesFromTruck(DistanceQueryFields.All, null, false);
        }

        internal static PlayerTruckDistance[] GetDistancesFromTruck(
            DistanceQueryFields fields,
            ICollection<PlayerAvatar>? players = null,
            bool forceRefresh = false)
        {
            try
            {
            var profileEnabled = FeatureFlags.DebugLogging;
            var profileStart = profileEnabled ? Time.realtimeSinceStartup : 0f;
            var profileAfterSetup = profileStart;
            var profileLoopStart = profileStart;
            var navRefreshCount = 0;
            var roomRefreshCount = 0;
            var remoteHintUsedCount = 0;
            var processedPlayers = 0;

            if (fields == DistanceQueryFields.None)
            {
                return Array.Empty<PlayerTruckDistance>();
            }

            var levelGenerator = LevelGenerator.Instance;
            if (levelGenerator == null)
            {
                return Array.Empty<PlayerTruckDistance>();
            }

            var allLevelPoints = GetAllLevelPoints(levelGenerator);
            if (!TryGetTruckTarget(levelGenerator, allLevelPoints, out var truckPosition, out var truckPoint))
            {
                return Array.Empty<PlayerTruckDistance>();
            }

            var director = GameDirector.instance;
            if (director?.PlayerList == null || director.PlayerList.Count == 0)
            {
                return Array.Empty<PlayerTruckDistance>();
            }

            var levelPointsCount = allLevelPoints?.Count ?? 0;
            if (!ReferenceEquals(s_cachedLevelGeneratorForPlayers, levelGenerator) ||
                !s_hasCachedTruckPosition ||
                Vector3.SqrMagnitude(s_cachedTruckPosition - truckPosition) > 0.0001f ||
                s_cachedLevelPointsCount != levelPointsCount)
            {
                s_playerCache.Clear();
                s_remoteHints.Clear();
                s_cachedLevelGeneratorForPlayers = levelGenerator;
                s_cachedTruckPosition = truckPosition;
                s_cachedLevelPointsCount = levelPointsCount;
                s_hasCachedTruckPosition = true;
            }

            HashSet<int>? allowedKeys = null;
            if (players != null)
            {
                allowedKeys = new HashSet<int>();
                foreach (var player in players)
                {
                    if (player == null)
                    {
                        continue;
                    }

                    allowedKeys.Add(GetPlayerKey(player));
                }

                if (allowedKeys.Count == 0)
                {
                    return Array.Empty<PlayerTruckDistance>();
                }
            }

            var needsRoomPath = (fields & DistanceQueryFields.RoomPath) != 0;
            var needsNavPath = (fields & DistanceQueryFields.NavMeshDistance) != 0;
            var needsHeight = (fields & DistanceQueryFields.Height) != 0;
            var roomGraph = (needsRoomPath || needsNavPath)
                ? GetOrBuildRoomGraph(levelGenerator, allLevelPoints)
                : null;
            var totalMapRooms = roomGraph != null && roomGraph.Count > 0 ? roomGraph.Count : -1;
            var levelStamp = RunManager.instance != null ? RunManager.instance.levelsCompleted : 0;
            if (profileEnabled)
            {
                profileAfterSetup = Time.realtimeSinceStartup;
                profileLoopStart = profileAfterSetup;
            }

            var distances = new List<PlayerTruckDistance>(director.PlayerList.Count);
            foreach (var player in director.PlayerList)
            {
                if (player == null)
                {
                    continue;
                }

                var playerKey = GetPlayerKey(player);
                if (allowedKeys != null && !allowedKeys.Contains(playerKey))
                {
                    continue;
                }

                if (!s_playerCache.TryGetValue(playerKey, out var cached))
                {
                    cached = new CachedPlayerDistance();
                    s_playerCache[playerKey] = cached;
                }

                var worldPosition = GetPlayerWorldPosition(player);
                cached.LastKnownWorldPosition = worldPosition;
                var actorNumber = player.photonView?.Owner?.ActorNumber ?? 0;
                s_remoteHints.TryGetValue(actorNumber, out var remoteHint);
                var shouldUseRemoteHint =
                    SemiFunc.IsMasterClientOrSingleplayer() &&
                    SemiFunc.IsMultiplayer() &&
                    actorNumber > 0 &&
                    PhotonNetwork.LocalPlayer != null &&
                    actorNumber != PhotonNetwork.LocalPlayer.ActorNumber &&
                    remoteHint.LevelStamp == levelStamp;

            List<RoomVolume>? playerRooms = null;
                var roomHash = cached.RoomHash;
                if (shouldUseRemoteHint)
                {
                    roomHash = remoteHint.RoomHash;
                    remoteHintUsedCount++;
                }
                else if (needsRoomPath || needsNavPath)
                {
                    playerRooms = GetPlayerRooms(player);
                    roomHash = ComputeRoomsHash(playerRooms);
                }

                var roomChanged = roomHash != cached.RoomHash;
                var levelChanged = cached.LevelStamp != levelStamp;

                if (needsHeight)
                {
                    var heightAge = Time.unscaledTime - cached.HeightUpdatedAt;
                    if (forceRefresh || levelChanged || heightAge < 0f || heightAge > HeightCacheTtlSeconds)
                    {
                        if (shouldUseRemoteHint && (Time.unscaledTime - remoteHint.UpdatedAt) <= HeightCacheTtlSeconds)
                        {
                            cached.HeightDelta = remoteHint.HeightDelta;
                            cached.HeightUpdatedAt = remoteHint.UpdatedAt;
                        }
                        else
                        {
                            cached.HeightDelta = worldPosition.y - truckPosition.y;
                            cached.HeightUpdatedAt = Time.unscaledTime;
                        }
                    }
                }

                if (needsNavPath && (forceRefresh || levelChanged || roomChanged))
                {
                    var hasAssessment = TryAssessPlayerTruckRoute(
                        player,
                        worldPosition,
                        truckPosition,
                        sampleTruckTarget: false,
                        allowUnsampledPlayerFallback: false,
                        includePathCorners: false,
                        assessment: out var routeAssessment);
                    cached.NavMeshDistance = hasAssessment && routeAssessment.HasValidPath
                        ? routeAssessment.NavMeshDistance
                        : -1f;
                    cached.HasValidPath = hasAssessment && routeAssessment.HasValidPath;
                    navRefreshCount++;
                }

                if (needsRoomPath && (forceRefresh || levelChanged || roomChanged))
                {
                    playerRooms ??= GetPlayerRooms(player);
                    cached.ShortestRoomPathToTruck = ResolveShortestRoomPathToTruck(playerRooms ?? new List<RoomVolume>(), truckPoint, roomGraph);
                    roomRefreshCount++;
                }

                if (needsRoomPath || needsNavPath)
                {
                    cached.TotalMapRooms = totalMapRooms;
                }

                cached.RoomHash = roomHash;
                cached.LevelStamp = levelStamp;

                distances.Add(new PlayerTruckDistance(
                    player,
                    cached.NavMeshDistance,
                    cached.HeightDelta,
                    cached.ShortestRoomPathToTruck,
                    cached.TotalMapRooms,
                    cached.HasValidPath));
                processedPlayers++;
            }

            if (profileEnabled && s_activationProfilingEnabled)
            {
                var profileEnd = Time.realtimeSinceStartup;
                var totalMs = (profileEnd - profileStart) * 1000f;
                var setupMs = (profileAfterSetup - profileStart) * 1000f;
                var loopMs = (profileEnd - profileLoopStart) * 1000f;
                s_activationProfileStats.Calls++;
                s_activationProfileStats.PlayersProcessed += processedPlayers;
                s_activationProfileStats.NavRefreshCount += navRefreshCount;
                s_activationProfileStats.RoomRefreshCount += roomRefreshCount;
                s_activationProfileStats.RemoteHintUsedCount += remoteHintUsedCount;
                s_activationProfileStats.TotalMs += totalMs;
                s_activationProfileStats.SetupMs += setupMs;
                s_activationProfileStats.LoopMs += loopMs;
                s_activationProfileStats.MaxCallMs = Mathf.Max(s_activationProfileStats.MaxCallMs, totalMs);
            }

            return distances.ToArray();
            }
            catch (Exception ex)
            {
                LogRuntimeHotPathException("GetDistancesFromTruck", ex);
                return Array.Empty<PlayerTruckDistance>();
            }
        }

        internal static bool TryGetLocalPlayerTruckRouteAssessment(
            bool includePathCorners,
            out PlayerTruckRouteAssessment assessment)
        {
            assessment = default;
            try
            {
                var player = PlayerAvatar.instance;
                if (player == null)
                {
                    return false;
                }

                var levelGenerator = LevelGenerator.Instance;
                if (levelGenerator == null)
                {
                    return false;
                }

                var allLevelPoints = GetAllLevelPoints(levelGenerator);
                if (!TryGetTruckTarget(levelGenerator, allLevelPoints, out var truckPosition, out _))
                {
                    return false;
                }

                return TryAssessPlayerTruckRoute(
                    player,
                    GetPlayerWorldPosition(player),
                    truckPosition,
                    sampleTruckTarget: true,
                    allowUnsampledPlayerFallback: true,
                    includePathCorners: includePathCorners,
                    assessment: out assessment);
            }
            catch (Exception ex)
            {
                LogRuntimeHotPathException("TryGetLocalPlayerTruckRouteAssessment", ex);
                return false;
            }
        }

        internal static void ApplyRemotePlayerHint(int actorNumber, int roomHash, float heightDelta, int levelStamp)
        {
            if (actorNumber <= 0)
            {
                return;
            }

            s_remoteHints[actorNumber] = new RemotePlayerHint(roomHash, heightDelta, levelStamp, Time.unscaledTime);
        }

        internal static bool TryBuildLocalPlayerTruckHint(out int roomHash, out float heightDelta, out int levelStamp)
        {
            roomHash = 0;
            heightDelta = 0f;
            levelStamp = RunManager.instance != null ? RunManager.instance.levelsCompleted : 0;

            try
            {
                if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null)
                {
                    return false;
                }

                var levelGenerator = LevelGenerator.Instance;
                if (levelGenerator == null)
                {
                    return false;
                }

                var allLevelPoints = GetAllLevelPoints(levelGenerator);
                if (!TryGetTruckTarget(levelGenerator, allLevelPoints, out var truckPosition, out _))
                {
                    return false;
                }

                var director = GameDirector.instance;
                if (director?.PlayerList == null || director.PlayerList.Count == 0)
                {
                    return false;
                }

                var localActor = PhotonNetwork.LocalPlayer.ActorNumber;
                PlayerAvatar? localPlayer = null;
                for (var i = 0; i < director.PlayerList.Count; i++)
                {
                    var candidate = director.PlayerList[i];
                    if (candidate == null)
                    {
                        continue;
                    }

                    if ((candidate.photonView?.Owner?.ActorNumber ?? 0) == localActor)
                    {
                        localPlayer = candidate;
                        break;
                    }
                }

                if (localPlayer == null)
                {
                    return false;
                }

                var rooms = GetPlayerRooms(localPlayer);
                roomHash = ComputeRoomsHash(rooms);
                var position = GetPlayerWorldPosition(localPlayer);
                heightDelta = position.y - truckPosition.y;
                return true;
            }
            catch (Exception ex)
            {
                LogRuntimeHotPathException("TryBuildLocalPlayerTruckHint", ex);
                return false;
            }
        }

        private static void LogRuntimeHotPathException(string context, Exception ex)
        {
            if (!FeatureFlags.DebugLogging)
            {
                return;
            }

            var key = "LastChance.Runtime.PlayerTruckDistance." + context;
            if (!LogLimiter.ShouldLog(key, 600))
            {
                return;
            }

            Log.LogWarning($"[LastChance] Runtime hot-path failed in {context}: {ex.GetType().Name}: {ex.Message}");
        }

        internal static void BeginActivationProfiling()
        {
            s_activationProfileStats = default;
            s_activationProfilingEnabled = true;
        }

        internal static string EndActivationProfilingSummary()
        {
            s_activationProfilingEnabled = false;
            return
                $"calls={s_activationProfileStats.Calls} total={s_activationProfileStats.TotalMs:F1}ms setup={s_activationProfileStats.SetupMs:F1}ms loop={s_activationProfileStats.LoopMs:F1}ms maxCall={s_activationProfileStats.MaxCallMs:F1}ms players={s_activationProfileStats.PlayersProcessed} navRefresh={s_activationProfileStats.NavRefreshCount} roomRefresh={s_activationProfileStats.RoomRefreshCount} remoteHints={s_activationProfileStats.RemoteHintUsedCount}";
        }

        private static int GetPlayerKey(PlayerAvatar player)
        {
            var actor = player.photonView?.Owner?.ActorNumber ?? 0;
            if (actor != 0)
            {
                return actor;
            }

            return player.GetInstanceID();
        }

        private static int ComputeRoomsHash(List<RoomVolume> rooms)
        {
            unchecked
            {
                var hash = 17;
                for (var i = 0; i < rooms.Count; i++)
                {
                    hash = (hash * 31) + RuntimeHelpers.GetHashCode(rooms[i]);
                }

                return hash;
            }
        }

        private static Dictionary<RoomVolume, HashSet<RoomVolume>> GetOrBuildRoomGraph(LevelGenerator levelGenerator, List<LevelPoint>? allLevelPoints)
        {
            var pointCount = allLevelPoints?.Count ?? 0;
            if (s_cachedRoomGraph != null &&
                ReferenceEquals(levelGenerator, s_cachedGraphLevelGenerator) &&
                s_cachedGraphPointCount == pointCount)
            {
                return s_cachedRoomGraph;
            }

            var graph = BuildRoomGraph(allLevelPoints);
            s_cachedGraphLevelGenerator = levelGenerator;
            s_cachedGraphPointCount = pointCount;
            s_cachedRoomGraph = graph;
            return graph;
        }

        private static bool TryGetTruckTarget(LevelGenerator levelGenerator, List<LevelPoint>? allLevelPoints, out Vector3 truckPosition, out LevelPoint? truckPoint)
        {
            truckPosition = Vector3.zero;
            truckPoint = null;
            var candidate = levelGenerator.LevelPathTruck;
            if (TryGetLevelPointPosition(candidate, out truckPosition))
            {
                truckPoint = candidate;
                return true;
            }

            if (allLevelPoints == null)
            {
                return false;
            }

            foreach (var point in allLevelPoints)
            {
                if (point == null)
                {
                    continue;
                }

                if (!TryIsTruckPoint(point))
                {
                    continue;
                }

                if (TryGetLevelPointPosition(point, out truckPosition))
                {
                    truckPoint = point;
                    return true;
                }
            }

            return false;
        }

        private static List<LevelPoint>? GetAllLevelPoints(LevelGenerator levelGenerator)
        {
            var points = levelGenerator.LevelPathPoints;
            if (points == null)
            {
                return null;
            }

            var list = new List<LevelPoint>();
            foreach (var point in points)
            {
                if (point is LevelPoint levelPoint)
                {
                    list.Add(levelPoint);
                }
            }

            return list.Count > 0 ? list : null;
        }

        private static bool TryIsTruckPoint(LevelPoint point)
        {
            if (point.Truck)
            {
                return true;
            }

            return point.Room != null && point.Room.Truck;
        }

        private static int ResolveShortestRoomPathToTruck(List<RoomVolume> playerRooms, LevelPoint? truckPoint, Dictionary<RoomVolume, HashSet<RoomVolume>>? roomGraph)
        {
            if (truckPoint == null || roomGraph == null || roomGraph.Count == 0)
            {
                return -1;
            }

            var truckRoom = GetLevelPointRoom(truckPoint);
            if (truckRoom == null || !roomGraph.ContainsKey(truckRoom))
            {
                return -1;
            }

            if (playerRooms.Count == 0)
            {
                return -1;
            }

            var visited = new HashSet<RoomVolume>();
            var queue = new Queue<(RoomVolume room, int distance)>();
            foreach (var room in playerRooms)
            {
                if (room == null || !roomGraph.ContainsKey(room) || !visited.Add(room))
                {
                    continue;
                }

                queue.Enqueue((room, 0));
            }

            while (queue.Count > 0)
            {
                var (room, depth) = queue.Dequeue();
                if (ReferenceEquals(room, truckRoom))
                {
                    return depth;
                }

                if (!roomGraph.TryGetValue(room, out var neighbors))
                {
                    continue;
                }

                foreach (var neighbor in neighbors)
                {
                    if (neighbor == null || !visited.Add(neighbor))
                    {
                        continue;
                    }

                    queue.Enqueue((neighbor, depth + 1));
                }
            }

            return -1;
        }

        private static List<RoomVolume> GetPlayerRooms(PlayerAvatar player)
        {
            var results = new List<RoomVolume>();
            if (player == null)
            {
                return results;
            }

            RoomVolumeCheck? roomCheck = null;
            var deathHead = player.playerDeathHead;
            if (deathHead != null)
            {
                roomCheck = deathHead.roomVolumeCheck;
            }

            if (roomCheck == null)
            {
                roomCheck = player.RoomVolumeCheck;
            }

            if (roomCheck == null)
            {
                return results;
            }

            var currentRooms = roomCheck.CurrentRooms;
            if (currentRooms == null)
            {
                return results;
            }

            foreach (var room in currentRooms)
            {
                if (room is RoomVolume roomVolume)
                {
                    results.Add(roomVolume);
                }
            }

            return results;
        }

        private static Dictionary<RoomVolume, HashSet<RoomVolume>> BuildRoomGraph(List<LevelPoint>? allLevelPoints)
        {
            var graph = new Dictionary<RoomVolume, HashSet<RoomVolume>>();
            if (allLevelPoints == null)
            {
                return graph;
            }

            foreach (var point in allLevelPoints)
            {
                if (point == null)
                {
                    continue;
                }

                var room = GetLevelPointRoom(point);
                if (room == null)
                {
                    continue;
                }

                if (!graph.ContainsKey(room))
                {
                    graph[room] = new HashSet<RoomVolume>();
                }

                var connected = GetConnectedPoints(point);
                if (connected == null)
                {
                    continue;
                }

                foreach (var neighborPoint in connected)
                {
                    if (neighborPoint == null)
                    {
                        continue;
                    }

                    var neighborRoom = GetLevelPointRoom(neighborPoint);
                    if (neighborRoom == null)
                    {
                        continue;
                    }

                    if (!graph.ContainsKey(neighborRoom))
                    {
                        graph[neighborRoom] = new HashSet<RoomVolume>();
                    }

                    if (!ReferenceEquals(room, neighborRoom))
                    {
                        graph[room].Add(neighborRoom);
                        graph[neighborRoom].Add(room);
                    }
                }
            }

            return graph;
        }

        private static RoomVolume? GetLevelPointRoom(LevelPoint levelPoint)
        {
            return levelPoint.Room;
        }

        private static IEnumerable<LevelPoint>? GetConnectedPoints(LevelPoint levelPoint)
        {
            if (levelPoint == null || levelPoint.ConnectedPoints == null)
            {
                return null;
            }

            var list = new List<LevelPoint>();
            foreach (var point in levelPoint.ConnectedPoints)
            {
                if (point is LevelPoint levelPointNeighbor)
                {
                    list.Add(levelPointNeighbor);
                }
            }

            return list.Count > 0 ? list : null;
        }

        private static bool TryGetLevelPointPosition(LevelPoint? levelPoint, out Vector3 position)
        {
            position = Vector3.zero;
            if (levelPoint == null)
            {
                return false;
            }

            position = levelPoint.transform.position;
            return true;
        }

        private static Vector3 GetPlayerWorldPosition(PlayerAvatar player)
        {
            if (player == null)
            {
                return Vector3.zero;
            }

            var deathHead = player.playerDeathHead;
            if (deathHead != null)
            {
                return deathHead.transform.position;
            }

            if (player.playerTransform != null)
            {
                return player.playerTransform.position;
            }

            return player.transform.position;
        }

        private static bool TryGetPlayerNavMeshPosition(PlayerAvatar player, Vector3 worldPosition, out Vector3 navMeshPosition)
        {
            navMeshPosition = Vector3.zero;
            if (player == null)
            {
                return false;
            }

            if (TrySamplePosition(worldPosition, 8f, out var sampledPosition))
            {
                navMeshPosition = sampledPosition;
                return true;
            }

            // Death head often moves above the navmesh; prefer multiple probes around its physics center
            // before falling back to PlayerAvatar.LastNavmeshPosition (which may remain at death location).
            var deathHead = player.playerDeathHead;
            if (deathHead != null)
            {
                var headCenter = deathHead.transform.position;
                var physGrabObject = deathHead.physGrabObject;
                if (physGrabObject != null)
                {
                    headCenter = physGrabObject.centerPoint;
                }

                if (TrySamplePosition(headCenter, 12f, out sampledPosition) ||
                    TrySamplePosition(headCenter - Vector3.up * 0.5f, 18f, out sampledPosition) ||
                    TrySamplePosition(headCenter, 30f, out sampledPosition))
                {
                    navMeshPosition = sampledPosition;
                    return true;
                }

                // When the death head is active and no navmesh point can be resolved nearby,
                // avoid using stale avatar navmesh position from the corpse location.
                return false;
            }

            if (player.LastNavmeshPosition != Vector3.zero)
            {
                navMeshPosition = player.LastNavmeshPosition;
                return true;
            }

            return false;
        }

        private static bool TryAssessPlayerTruckRoute(
            PlayerAvatar player,
            Vector3 playerWorldPosition,
            Vector3 truckWorldPosition,
            bool sampleTruckTarget,
            bool allowUnsampledPlayerFallback,
            bool includePathCorners,
            out PlayerTruckRouteAssessment assessment)
        {
            assessment = default;
            if (player == null)
            {
                return false;
            }

            var hasPlayerNavMeshPosition = TryGetPlayerNavMeshPosition(player, playerWorldPosition, out var navMeshFrom);
            if (!hasPlayerNavMeshPosition)
            {
                if (!allowUnsampledPlayerFallback)
                {
                    assessment = new PlayerTruckRouteAssessment(
                        playerWorldPosition,
                        truckWorldPosition,
                        playerWorldPosition,
                        truckWorldPosition,
                        -1f,
                        false,
                        Array.Empty<Vector3>());
                    return true;
                }

                navMeshFrom = playerWorldPosition;
            }

            var navMeshTo = truckWorldPosition;
            if (sampleTruckTarget && TrySamplePosition(truckWorldPosition, 8f, out var sampledTruckPosition))
            {
                navMeshTo = sampledTruckPosition;
            }

            var hasPath = TryCalculatePath(
                navMeshFrom,
                navMeshTo,
                includePathCorners,
                out var navMeshDistance,
                out var pathCorners);

            assessment = new PlayerTruckRouteAssessment(
                playerWorldPosition,
                truckWorldPosition,
                navMeshFrom,
                navMeshTo,
                hasPath ? navMeshDistance : -1f,
                hasPath,
                pathCorners);
            return true;
        }

        private static bool TrySamplePosition(Vector3 worldPosition, float maxDistance, out Vector3 navMeshPosition)
        {
            navMeshPosition = Vector3.zero;
            if (!NavMesh.SamplePosition(worldPosition, out var navHit, maxDistance, NavMesh.AllAreas))
            {
                return false;
            }

            navMeshPosition = navHit.position;
            return true;
        }

        private static bool TryCalculatePath(
            Vector3 from,
            Vector3 to,
            bool includePathCorners,
            out float navMeshDistance,
            out Vector3[] pathCorners)
        {
            navMeshDistance = 0f;
            pathCorners = Array.Empty<Vector3>();
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path))
            {
                return false;
            }

            var corners = path.corners;
            if (corners == null || corners.Length == 0)
            {
                navMeshDistance = Vector3.Distance(from, to);
                return true;
            }

            var previous = from;
            var totalDistance = 0f;
            foreach (var corner in corners)
            {
                totalDistance += Vector3.Distance(previous, corner);
                previous = corner;
            }

            navMeshDistance = totalDistance;
            if (includePathCorners)
            {
                pathCorners = corners;
            }
            return true;
        }
    }
}

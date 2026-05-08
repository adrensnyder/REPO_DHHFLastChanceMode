#nullable enable

using System.Collections.Generic;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Runtime;
using UnityEngine;

namespace DHHFLastChanceMode.Modules.Utilities
{
    internal static class PlayerStateExtractionHelper
    {
        internal readonly struct PlayerStateSnapshot
        {
            internal PlayerStateSnapshot(
                int actorNumber,
                int steamIdShort,
                string name,
                Color color,
                bool isAlive,
                bool isDead,
                bool isInTruck,
                bool isSurrendered,
                int sourceOrder)
            {
                ActorNumber = actorNumber;
                SteamIdShort = steamIdShort;
                Name = name;
                Color = color;
                IsAlive = isAlive;
                IsDead = isDead;
                IsInTruck = isInTruck;
                IsSurrendered = isSurrendered;
                SourceOrder = sourceOrder;
            }

            internal int ActorNumber { get; }
            internal int SteamIdShort { get; }
            internal string Name { get; }
            internal Color Color { get; }
            internal bool IsAlive { get; }
            internal bool IsDead { get; }
            internal bool IsInTruck { get; }
            internal bool IsSurrendered { get; }
            internal int SourceOrder { get; }
        }

        internal static List<PlayerStateSnapshot> GetPlayersStateSnapshot()
        {
            var snapshots = new List<PlayerStateSnapshot>();
            var director = GameDirector.instance;
            if (director == null || director.PlayerList == null || director.PlayerList.Count == 0)
            {
                return snapshots;
            }

            for (var i = 0; i < director.PlayerList.Count; i++)
            {
                var player = director.PlayerList[i];
                if (player == null)
                {
                    continue;
                }

                var actorNumber = player.photonView?.Owner?.ActorNumber ?? 0;
                var steamIdShort = GetSteamIdShort(player);
                var name = GetPlayerName(player);
                var color = GetPlayerColor(player);
                var deadSet = IsDeadSet(player);
                var disabled = IsDisabled(player);
                var isDead = deadSet || disabled;
                var isAlive = !isDead;
                var isInTruck = IsPlayerInTruck(player, disabled);
                var isSurrendered = LastChanceTimerController.IsPlayerSurrenderedForData(player);

                snapshots.Add(
                    new PlayerStateSnapshot(
                        actorNumber,
                        steamIdShort,
                        name,
                        color,
                        isAlive,
                        isDead,
                        isInTruck,
                        isSurrendered,
                        i));
            }

            snapshots.Sort(CompareSnapshotOrder);
            return snapshots;
        }

        internal static List<PlayerStateSnapshot> GetPlayersStillInLastChance()
        {
            var allPlayers = GetPlayersStateSnapshot();
            var activePlayers = new List<PlayerStateSnapshot>(allPlayers.Count);
            for (var i = 0; i < allPlayers.Count; i++)
            {
                var snapshot = allPlayers[i];
                if (!snapshot.IsSurrendered)
                {
                    activePlayers.Add(snapshot);
                }
            }

            return activePlayers;
        }

        private static int CompareSnapshotOrder(PlayerStateSnapshot left, PlayerStateSnapshot right)
        {
            if (left.ActorNumber > 0 && right.ActorNumber > 0)
            {
                return left.ActorNumber.CompareTo(right.ActorNumber);
            }

            return left.SourceOrder.CompareTo(right.SourceOrder);
        }

        private static string GetPlayerName(PlayerAvatar player)
        {
            if (!string.IsNullOrWhiteSpace(player.playerName))
            {
                return player.playerName;
            }

            return "unknown";
        }

        private static Color GetPlayerColor(PlayerAvatar player)
        {
            var visuals = player.playerAvatarVisuals;
            if (visuals == null)
            {
                return Color.black;
            }

            return SemiFunc.PlayerGetColorMain(player);
        }

        private static bool IsDeadSet(PlayerAvatar player)
        {
            return player.deadSet;
        }

        private static bool IsDisabled(PlayerAvatar player)
        {
            return player.isDisabled;
        }

        private static bool IsPlayerInTruck(PlayerAvatar player, bool isDisabled)
        {
            if (!isDisabled)
            {
                var roomVolumeCheck = player.RoomVolumeCheck;
                return roomVolumeCheck != null && IsRoomVolumeInTruck(roomVolumeCheck);
            }

            var deathHead = player.playerDeathHead;
            if (deathHead == null)
            {
                return false;
            }

            var roomVolume = deathHead.roomVolumeCheck;
            if (roomVolume != null)
            {
                return IsRoomVolumeInTruck(roomVolume);
            }

            return deathHead.inTruck;
        }

        private static bool IsRoomVolumeInTruck(RoomVolumeCheck roomVolumeCheck)
        {
            return roomVolumeCheck.inTruck;
        }

        private static int GetSteamIdShort(PlayerAvatar player)
        {
            return player.steamIDshort;
        }
    }
}

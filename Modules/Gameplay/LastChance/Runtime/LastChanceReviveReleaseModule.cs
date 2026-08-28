#nullable enable

using System;
using System.Collections.Generic;
using HarmonyLib;
using Photon.Pun;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Runtime
{
    internal static class LastChanceReviveReleaseTracker
    {
        private const int ReleaseObjectViewId = -1;
        private static readonly HashSet<PlayerIdentity> PendingRevives = new();
        private static readonly Dictionary<int, PlayerIdentity> PendingIdentityByInstanceId = new();

        internal static void CapturePendingDeathsAtActivation()
        {
            var players = GameDirector.instance?.PlayerList;
            if (players == null)
            {
                return;
            }

            for (var i = 0; i < players.Count; i++)
            {
                MarkPendingIfActiveAndDead(players[i]);
            }
        }

        internal static void MarkPendingIfActiveAndDead(PlayerAvatar? player)
        {
            if (player == null || !LastChanceRuntimeOrchestrator.IsRuntimeActive || !IsDead(player))
            {
                return;
            }

            var identity = PlayerIdentity.Create(player);
            PendingRevives.Add(identity);
            PendingIdentityByInstanceId[player.GetInstanceID()] = identity;
        }

        internal static PhysGrabber[]? CapturePendingReviveGrabbers(PlayerAvatar? player)
        {
            if (player == null || !TryGetPendingIdentity(player, out _))
            {
                return null;
            }

            var grabbers = player.playerDeathHead?.physGrabObject?.playerGrabbing;
            return grabbers == null || grabbers.Count == 0
                ? Array.Empty<PhysGrabber>()
                : grabbers.ToArray();
        }

        internal static void HandleReviveCompleted(PlayerAvatar? player, PhysGrabber[]? pendingGrabbers)
        {
            if (player == null || pendingGrabbers == null || IsDead(player))
            {
                return;
            }

            if (!TryGetPendingIdentity(player, out var identity))
            {
                return;
            }

            if (!SemiFunc.IsMultiplayer() || PhotonNetwork.IsMasterClient)
            {
                ReleaseDeathHeadGrabbers(pendingGrabbers);
            }

            RemovePending(player.GetInstanceID(), identity);
        }

        internal static void ClearForPlayer(PlayerAvatar? player)
        {
            if (player == null || !TryGetPendingIdentity(player, out var identity))
            {
                return;
            }

            RemovePending(player.GetInstanceID(), identity);
        }

        internal static void ClearForActorNumber(int actorNumber)
        {
            if (actorNumber <= 0 || PendingRevives.Count == 0)
            {
                return;
            }

            PendingRevives.RemoveWhere(identity => identity.ActorNumber == actorNumber);

            var staleInstanceIds = new List<int>();
            foreach (var pair in PendingIdentityByInstanceId)
            {
                if (pair.Value.ActorNumber == actorNumber)
                {
                    staleInstanceIds.Add(pair.Key);
                }
            }

            for (var i = 0; i < staleInstanceIds.Count; i++)
            {
                PendingIdentityByInstanceId.Remove(staleInstanceIds[i]);
            }
        }

        internal static void ResetForRoomExit()
        {
            ClearAllPending();
        }

        internal static void ResetForSceneChange()
        {
            ClearAllPending();
        }

        internal static void ResetForPluginDestroy()
        {
            ClearAllPending();
        }

        private static bool TryGetPendingIdentity(PlayerAvatar player, out PlayerIdentity identity)
        {
            var currentIdentity = PlayerIdentity.Create(player);
            if (PendingRevives.Contains(currentIdentity))
            {
                identity = currentIdentity;
                return true;
            }

            if (PendingIdentityByInstanceId.TryGetValue(player.GetInstanceID(), out identity) &&
                PendingRevives.Contains(identity))
            {
                return true;
            }

            identity = default;
            return false;
        }

        private static void RemovePending(int instanceId, PlayerIdentity identity)
        {
            PendingRevives.Remove(identity);
            PendingIdentityByInstanceId.Remove(instanceId);
        }

        private static void ClearAllPending()
        {
            PendingRevives.Clear();
            PendingIdentityByInstanceId.Clear();
        }

        private static bool IsDead(PlayerAvatar player)
        {
            return player.deadSet || player.isDisabled;
        }

        private static void ReleaseDeathHeadGrabbers(PhysGrabber[] grabbers)
        {
            for (var i = 0; i < grabbers.Length; i++)
            {
                var grabber = grabbers[i];
                if (grabber == null)
                {
                    continue;
                }

                if (!SemiFunc.IsMultiplayer())
                {
                    grabber.ReleaseObjectRPC(true, 2f, ReleaseObjectViewId);
                    continue;
                }

                if (grabber.photonView != null)
                {
                    grabber.photonView.RPC(
                        nameof(PhysGrabber.ReleaseObjectRPC),
                        RpcTarget.All,
                        false,
                        1f,
                        ReleaseObjectViewId);
                }
            }
        }

        private readonly struct PlayerIdentity : IEquatable<PlayerIdentity>
        {
            private const int ActorIdentityKind = 1;
            private const int ViewIdentityKind = 2;
            private const int InstanceIdentityKind = 3;

            private PlayerIdentity(int kind, int value, int actorNumber)
            {
                Kind = kind;
                Value = value;
                ActorNumber = actorNumber;
            }

            private int Kind { get; }
            private int Value { get; }
            internal int ActorNumber { get; }

            internal static PlayerIdentity Create(PlayerAvatar player)
            {
                var photonView = player.photonView;
                var actorNumber = photonView?.Owner?.ActorNumber ?? 0;
                if (actorNumber > 0)
                {
                    return new PlayerIdentity(ActorIdentityKind, actorNumber, actorNumber);
                }

                var viewId = photonView?.ViewID ?? 0;
                if (viewId > 0)
                {
                    return new PlayerIdentity(ViewIdentityKind, viewId, 0);
                }

                return new PlayerIdentity(InstanceIdentityKind, player.GetInstanceID(), 0);
            }

            public bool Equals(PlayerIdentity other)
            {
                return Kind == other.Kind && Value == other.Value;
            }

            public override bool Equals(object? obj)
            {
                return obj is PlayerIdentity other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (Kind * 397) ^ Value;
                }
            }
        }
    }

    [HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.PlayerDeathRPC))]
    internal static class LastChancePlayerDeathOriginPatch
    {
        [HarmonyPostfix]
        private static void Postfix(PlayerAvatar __instance)
        {
            LastChanceReviveReleaseTracker.MarkPendingIfActiveAndDead(__instance);
        }
    }

    [HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.ReviveRPC))]
    internal static class LastChancePlayerReviveReleasePatch
    {
        [HarmonyPrefix]
        private static void Prefix(PlayerAvatar __instance, out PhysGrabber[]? __state)
        {
            __state = LastChanceReviveReleaseTracker.CapturePendingReviveGrabbers(__instance);
        }

        [HarmonyPostfix]
        private static void Postfix(PlayerAvatar __instance, PhysGrabber[]? __state)
        {
            LastChanceReviveReleaseTracker.HandleReviveCompleted(__instance, __state);
        }
    }

    [HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.OnDestroy))]
    internal static class LastChancePlayerDestroyReviveCleanupPatch
    {
        [HarmonyPostfix]
        private static void Postfix(PlayerAvatar __instance)
        {
            LastChanceReviveReleaseTracker.ClearForPlayer(__instance);
        }
    }
}

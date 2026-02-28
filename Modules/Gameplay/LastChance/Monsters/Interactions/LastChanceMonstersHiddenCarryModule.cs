#nullable enable

using System;
using System.Collections.Generic;
using BepInEx.Logging;
using DHHFLastChanceMode.Modules.Config;
using HarmonyLib;
using UnityEngine;
using DHHFLastChanceMode.Modules.Utilities;
using Logger = BepInEx.Logging.Logger;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Interactions
{
    [HarmonyPatch]
    internal static class LastChanceMonstersCarryProxyModule
    {
        private static readonly ManualLogSource Log = Logger.CreateLogSource("DHHFLastChanceMode.LastChance.HiddenCarry");

        private sealed class CarryAnchorState
        {
            internal int PlayerId;
            internal Vector3 PickupOrigin;
            internal bool HasOrigin;
            internal string LastState = string.Empty;
        }

        private static readonly Dictionary<int, CarryAnchorState> AnchorByCarrier = new();

        internal static void ResetRuntimeState()
        {
            AnchorByCarrier.Clear();
        }

        [HarmonyTargetMethods]
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.DeclaredMethod(typeof(EnemyHidden), "PlayerTumbleLogic");
        }

        [HarmonyPrefix]
        private static bool Prefix(EnemyHidden __instance)
        {
            if (__instance == null || !LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
            {
                return true;
            }

            var player = __instance.playerTarget;
            if (player == null || !LastChanceMonstersTargetProxyHelper.IsHeadProxyActive(player))
            {
                ClearPickupOrigin(__instance);
                return true;
            }

            if (!IsCarryState(__instance))
            {
                ClearPickupOrigin(__instance);
                return true;
            }

            var head = player.playerDeathHead;
            var phys = head?.physGrabObject;
            var rb = phys?.rb;
            var centerPoint = phys != null ? phys.centerPoint : head != null ? head.transform.position : Vector3.zero;
            var pickupTransform = __instance.playerPickupTransform;
            if (head == null || phys == null || rb == null || pickupTransform == null)
            {
                return true;
            }

            UpdatePickupOrigin(__instance, player, GetCurrentStateName(__instance), centerPoint);

            if (InternalDebugFlags.DebugLastChanceHiddenCarryFlow && LogLimiter.ShouldLog("HiddenCarry.PrefixState", 120))
            {
                Log.LogInfo(
                    $"[HiddenCarry][Prefix] state={GetCurrentStateName(__instance)} " +
                    $"headCenter={centerPoint} bodyPos={player.transform.position} pickupPos={pickupTransform.position}");
            }

            player.FallDamageResetSet(0.1f);
            phys.OverrideMass(1f, 0.1f);
            phys.OverrideAngularDrag(2f, 0.1f);
            phys.OverrideDrag(1f, 0.1f);

            var strength = 1f;
            if (phys.playerGrabbing.Count > 0)
            {
                strength = 0.5f;
            }
            else if (IsState(__instance, EnemyHidden.State.PlayerRelease) || IsState(__instance, EnemyHidden.State.PlayerPickup))
            {
                strength = 0.75f;
            }

            if (rb.isKinematic)
            {
                rb.position = Vector3.Lerp(centerPoint, pickupTransform.position, 0.35f);
                rb.rotation = Quaternion.Slerp(head.transform.rotation, pickupTransform.rotation, 0.2f * strength);
                return false;
            }

            var followPos = SemiFunc.PhysFollowPosition(centerPoint, pickupTransform.position, rb.velocity, 10f * strength);
            rb.AddForce(followPos * (10f * Time.fixedDeltaTime * strength), ForceMode.Impulse);

            var followRot = SemiFunc.PhysFollowRotation(head.transform, pickupTransform.rotation, rb, 0.2f * strength);
            rb.AddTorque(followRot * (1f * Time.fixedDeltaTime * strength), ForceMode.Impulse);

            // We handled hidden carry logic for head-proxy target; skip vanilla tumble-only path.
            return false;
        }

        internal static bool TryGetPickupOrigin(object carrierInstance, PlayerAvatar player, out Vector3 origin)
        {
            origin = default;
            if (carrierInstance == null || player == null)
            {
                return false;
            }

            var key = GetCarrierKey(carrierInstance);
            if (!AnchorByCarrier.TryGetValue(key, out var state) || state == null || !state.HasOrigin)
            {
                return false;
            }

            var playerId = GetPlayerId(player);
            if (state.PlayerId != playerId)
            {
                return false;
            }

            origin = state.PickupOrigin;
            return true;
        }

        private static bool IsCarryState(EnemyHidden instance)
        {
            return IsState(instance, EnemyHidden.State.PlayerPickup) ||
                   IsState(instance, EnemyHidden.State.PlayerMove) ||
                   IsState(instance, EnemyHidden.State.PlayerRelease);
        }

        private static string GetCurrentStateName(EnemyHidden instance)
        {
            if (instance == null)
            {
                return string.Empty;
            }

            return instance.currentState.ToString();
        }

        private static void UpdatePickupOrigin(object carrierInstance, PlayerAvatar player, string stateName, Vector3 currentHeadCenter)
        {
            var key = GetCarrierKey(carrierInstance);
            if (!AnchorByCarrier.TryGetValue(key, out var state) || state == null)
            {
                state = new CarryAnchorState();
                AnchorByCarrier[key] = state;
            }

            var playerId = GetPlayerId(player);
            var enteringPickup = string.Equals(stateName, "PlayerPickup", StringComparison.Ordinal) &&
                                 !string.Equals(state.LastState, "PlayerPickup", StringComparison.Ordinal);
            var playerChanged = state.PlayerId != 0 && state.PlayerId != playerId;

            if (!state.HasOrigin || enteringPickup || playerChanged)
            {
                state.PickupOrigin = currentHeadCenter;
                state.HasOrigin = true;
                state.PlayerId = playerId;
                if (InternalDebugFlags.DebugLastChanceHiddenCarryFlow && LogLimiter.ShouldLog("HiddenCarry.PickupOriginSet", 120))
                {
                    Log.LogInfo($"[HiddenCarry][OriginSet] state={stateName} origin={state.PickupOrigin} playerId={state.PlayerId}");
                }
            }

            state.LastState = stateName;
        }

        private static void ClearPickupOrigin(object carrierInstance)
        {
            if (carrierInstance == null)
            {
                return;
            }

            AnchorByCarrier.Remove(GetCarrierKey(carrierInstance));
        }

        private static int GetCarrierKey(object carrierInstance)
        {
            if (carrierInstance is UnityEngine.Object unityObject)
            {
                return unityObject.GetInstanceID();
            }

            return carrierInstance.GetHashCode();
        }

        private static int GetPlayerId(PlayerAvatar player)
        {
            var photonView = player.photonView;
            if (photonView != null)
            {
                return photonView.ViewID;
            }

            return player.GetInstanceID();
        }

        private static bool IsState(EnemyHidden instance, EnemyHidden.State state)
        {
            if (instance == null)
            {
                return false;
            }

            return instance.currentState == state;
        }
    }
}


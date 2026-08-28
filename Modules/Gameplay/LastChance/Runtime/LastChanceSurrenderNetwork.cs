#nullable enable

using System;
using DHHFLastChanceMode.Modules.Config;
using ExitGames.Client.Photon;
using DHHFLastChanceMode.Modules.Utilities;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Runtime
{
    internal sealed class LastChanceSurrenderNetwork : MonoBehaviourPunCallbacks, IOnEventCallback
    {
        private static LastChanceSurrenderNetwork? s_instance;
        private static int s_messageSeq;
        private static float s_lastTruckHintSentAt;
        private static int s_lastTruckHintRoomHash;
        private static int s_lastTruckHintLevelStamp = -1;
        private const float TruckHintBroadcastIntervalSeconds = 0.5f;
        private const string LastChanceSurrenderMessageType = "LastChanceSurrender";
        private const string LastChanceTimerStateMessageType = "LastChanceTimerState";
        private const string LastChanceDirectionPenaltyRequestMessageType = "LastChanceDirectionPenaltyRequest";
        private const string LastChanceUiStateMessageType = "LastChanceUiState";
        private const string LastChancePlayerTruckHintMessageType = "LastChancePlayerTruckHint";
        private const string LastChanceSurrenderSnapshotMessageType = "LastChanceSurrenderSnapshot";
        private const string LastChanceExtractionRewardMessageType = "LastChanceExtractionReward";
        private static readonly System.Collections.Generic.HashSet<int> s_appliedExtractionRewardIds = new();

        internal static void EnsureCreated()
        {
            if (s_instance != null)
            {
                return;
            }

            var go = new GameObject("DHHFix.LastChanceSurrender");
            UnityEngine.Object.DontDestroyOnLoad(go);
            s_instance = go.AddComponent<LastChanceSurrenderNetwork>();
        }

        internal static void NotifyLocalSurrender(int actorNumber)
        {
            if (!PhotonNetwork.InRoom || actorNumber <= 0)
            {
                return;
            }

            EnsureCreated();

            var options = new RaiseEventOptions
            {
                Receivers = ReceiverGroup.All
            };

            var envelope = CreateEnvelope(LastChanceSurrenderMessageType, actorNumber);
            PhotonNetwork.RaiseEvent(PhotonEventCodes.LastChanceSurrender, envelope.ToEventPayload(), options, SendOptions.SendReliable);
        }

        internal static void NotifyTimerState(bool active, float secondsRemaining, double hostSentAt)
        {
            if (!PhotonNetwork.InRoom)
            {
                return;
            }

            EnsureCreated();
            var options = new RaiseEventOptions
            {
                Receivers = ReceiverGroup.All
            };

            var envelope = CreateEnvelope(LastChanceTimerStateMessageType, new object[] { active, secondsRemaining, hostSentAt });
            PhotonNetwork.RaiseEvent(PhotonEventCodes.LastChanceTimerState, envelope.ToEventPayload(), options, SendOptions.SendReliable);
        }

        internal static void NotifySurrenderSnapshot(object[] surrenderedActorsPayload)
        {
            if (!PhotonNetwork.InRoom)
            {
                return;
            }

            EnsureCreated();
            var options = new RaiseEventOptions
            {
                Receivers = ReceiverGroup.All
            };

            var envelope = CreateEnvelope(LastChanceSurrenderSnapshotMessageType, surrenderedActorsPayload ?? System.Array.Empty<object>());
            PhotonNetwork.RaiseEvent(PhotonEventCodes.LastChanceSurrender, envelope.ToEventPayload(), options, SendOptions.SendReliable);
        }

        internal static void BroadcastExtractionReward()
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer() ||
                !FeatureFlags.LastChancePreserveExtractedCosmeticTokens ||
                RoundDirector.instance == null ||
                RoundDirector.instance.cosmeticWorldObjectsExtracted == null ||
                RoundDirector.instance.cosmeticWorldObjectsExtracted.Count == 0)
            {
                return;
            }

            EnsureCreated();

            var rarities = new object[RoundDirector.instance.cosmeticWorldObjectsExtracted.Count];
            for (var i = 0; i < rarities.Length; i++)
            {
                rarities[i] = (int)RoundDirector.instance.cosmeticWorldObjectsExtracted[i];
            }

            var rewardId = unchecked(++s_messageSeq);
            if (rewardId <= 0)
            {
                rewardId = s_messageSeq = 1;
            }

            ApplyExtractionReward(rewardId, rarities);

            if (!PhotonNetwork.InRoom || !SemiFunc.IsMultiplayer())
            {
                return;
            }

            var options = new RaiseEventOptions
            {
                // The master applies the reward locally above; send the event only to the other clients.
                Receivers = ReceiverGroup.Others
            };
            var envelope = CreateEnvelope(LastChanceExtractionRewardMessageType, new object[] { rewardId, rarities });
            PhotonNetwork.RaiseEvent(PhotonEventCodes.LastChanceExtractionReward, envelope.ToEventPayload(), options, SendOptions.SendReliable);
        }

        internal static void NotifyDirectionPenaltyRequest()
        {
            if (!PhotonNetwork.InRoom)
            {
                return;
            }

            EnsureCreated();
            var options = new RaiseEventOptions
            {
                Receivers = ReceiverGroup.MasterClient
            };

            var envelope = CreateEnvelope(LastChanceDirectionPenaltyRequestMessageType, null);
            PhotonNetwork.RaiseEvent(PhotonEventCodes.LastChanceDirectionPenaltyRequest, envelope.ToEventPayload(), options, SendOptions.SendReliable);
        }

        internal static void NotifyUiState(int requiredOnTruck, object[] playerStatesPayload)
        {
            if (!PhotonNetwork.InRoom)
            {
                return;
            }

            EnsureCreated();
            var options = new RaiseEventOptions
            {
                Receivers = ReceiverGroup.All
            };

            var envelope = CreateEnvelope(LastChanceUiStateMessageType, new object[] { requiredOnTruck, playerStatesPayload });
            PhotonNetwork.RaiseEvent(PhotonEventCodes.LastChanceUiState, envelope.ToEventPayload(), options, SendOptions.SendReliable);
        }

        internal static void TryBroadcastLocalPlayerTruckHint()
        {
            if (!PhotonNetwork.InRoom || SemiFunc.IsMasterClient() || PhotonNetwork.LocalPlayer == null)
            {
                return;
            }

            EnsureCreated();
            if (!PlayerTruckDistanceHelper.TryBuildLocalPlayerTruckHint(out var roomHash, out var heightDelta, out var levelStamp))
            {
                return;
            }

            var roomChanged = roomHash != s_lastTruckHintRoomHash || levelStamp != s_lastTruckHintLevelStamp;
            var dueByTime = Time.unscaledTime - s_lastTruckHintSentAt >= TruckHintBroadcastIntervalSeconds;
            if (!roomChanged && !dueByTime)
            {
                return;
            }

            var options = new RaiseEventOptions
            {
                Receivers = ReceiverGroup.MasterClient
            };

            var envelope = CreateEnvelope(
                LastChancePlayerTruckHintMessageType,
                new object[] { PhotonNetwork.LocalPlayer.ActorNumber, roomHash, heightDelta, levelStamp });
            PhotonNetwork.RaiseEvent(PhotonEventCodes.LastChancePlayerTruckHint, envelope.ToEventPayload(), options, SendOptions.SendUnreliable);

            s_lastTruckHintSentAt = Time.unscaledTime;
            s_lastTruckHintRoomHash = roomHash;
            s_lastTruckHintLevelStamp = levelStamp;
        }

        public override void OnEnable()
        {
            base.OnEnable();
            PhotonNetwork.AddCallbackTarget(this);
        }

        public override void OnDisable()
        {
            base.OnDisable();
            PhotonNetwork.RemoveCallbackTarget(this);
        }

        public override void OnMasterClientSwitched(Player newMasterClient)
        {
            base.OnMasterClientSwitched(newMasterClient);

            LastChanceTimerController.SuppressForCurrentRoom(
                "[LastChance] Master client switched; disabling LastChance and related runtime features for room safety.");
        }

        public override void OnJoinedRoom()
        {
            base.OnJoinedRoom();
            s_appliedExtractionRewardIds.Clear();
            LastChanceTimerController.ClearRoomSuppression();
            if (PhotonNetwork.IsMasterClient)
            {
                LastChanceTimerController.ForceBroadcastRuntimeSnapshotForSync();
            }
        }

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            base.OnPlayerEnteredRoom(newPlayer);
            if (!PhotonNetwork.IsMasterClient || newPlayer == null)
            {
                return;
            }

            LastChanceTimerController.ForceBroadcastRuntimeSnapshotForSync();
        }

        public override void OnLeftRoom()
        {
            base.OnLeftRoom();
            s_appliedExtractionRewardIds.Clear();
            LastChanceTimerController.ReleaseBatteryOverrideForExternalTeardown();
            DHHFLastChanceMode.Modules.Gameplay.LastChance.Spectate.LastChanceSpectateHelper.ResetOwnedState();
            LastChanceTimerController.ClearRoomSuppression();
        }

        public void OnEvent(EventData photonEvent)
        {
            if (photonEvent.Code == PhotonEventCodes.LastChanceExtractionReward)
            {
                var masterActor = PhotonNetwork.MasterClient?.ActorNumber ?? -1;
                if (masterActor <= 0 ||
                    photonEvent.Sender != masterActor ||
                    !NetworkEnvelope.TryParse(photonEvent.CustomData, out var rewardEnvelope) ||
                    !rewardEnvelope.IsExpectedSource() ||
                    !string.Equals(rewardEnvelope.MessageType, LastChanceExtractionRewardMessageType, System.StringComparison.Ordinal) ||
                    rewardEnvelope.Payload is not object[] rewardPayload ||
                    rewardPayload.Length < 2 ||
                    rewardPayload[0] is not int rewardId ||
                    rewardPayload[1] is not object[] rarities)
                {
                    return;
                }

                ApplyExtractionReward(rewardId, rarities);
                return;
            }

            if (photonEvent.Code == PhotonEventCodes.LastChanceTimerState)
            {
                var masterActor = PhotonNetwork.MasterClient?.ActorNumber ?? -1;
                if (masterActor > 0 &&
                    photonEvent.Sender == masterActor &&
                    NetworkEnvelope.TryParse(photonEvent.CustomData, out var envelope) &&
                    envelope.IsExpectedSource() &&
                    string.Equals(envelope.MessageType, LastChanceTimerStateMessageType, System.StringComparison.Ordinal) &&
                    envelope.Payload is object[] timerPayload &&
                    timerPayload.Length >= 3 &&
                    timerPayload[0] is bool active &&
                    timerPayload[1] is float remaining &&
                    timerPayload[2] is double hostSentAt)
                {
                    LastChanceTimerController.ApplyNetworkTimerState(active, remaining, hostSentAt);
                }
                return;
            }

            if (photonEvent.Code == PhotonEventCodes.LastChanceDirectionPenaltyRequest)
            {
                var masterActor = PhotonNetwork.MasterClient?.ActorNumber ?? -1;
                if (masterActor <= 0 ||
                    !PhotonNetwork.IsMasterClient ||
                    photonEvent.Sender <= 0 ||
                    photonEvent.Sender == masterActor ||
                    !NetworkEnvelope.TryParse(photonEvent.CustomData, out var envelope) ||
                    !envelope.IsExpectedSource() ||
                    !string.Equals(envelope.MessageType, LastChanceDirectionPenaltyRequestMessageType, System.StringComparison.Ordinal))
                {
                    return;
                }

                LastChanceTimerController.HandleDirectionPenaltyRequest(photonEvent.Sender);
                return;
            }

            if (photonEvent.Code == PhotonEventCodes.LastChanceUiState)
            {
                var masterActor = PhotonNetwork.MasterClient?.ActorNumber ?? -1;
                if (masterActor > 0 &&
                    photonEvent.Sender == masterActor &&
                    NetworkEnvelope.TryParse(photonEvent.CustomData, out var envelope) &&
                    envelope.IsExpectedSource() &&
                    string.Equals(envelope.MessageType, LastChanceUiStateMessageType, System.StringComparison.Ordinal) &&
                    envelope.Payload is object[] uiPayload &&
                    uiPayload.Length >= 2 &&
                    uiPayload[0] is int required &&
                    uiPayload[1] is object[] states)
                {
                    LastChanceTimerController.ApplyNetworkUiState(required, states, photonEvent.Sender);
                }
                return;
            }

            if (photonEvent.Code == PhotonEventCodes.LastChancePlayerTruckHint)
            {
                var masterActor = PhotonNetwork.MasterClient?.ActorNumber ?? -1;
                if (masterActor > 0 &&
                    PhotonNetwork.IsMasterClient &&
                    photonEvent.Sender > 0 &&
                    NetworkEnvelope.TryParse(photonEvent.CustomData, out var envelope) &&
                    envelope.IsExpectedSource() &&
                    string.Equals(envelope.MessageType, LastChancePlayerTruckHintMessageType, System.StringComparison.Ordinal) &&
                    envelope.Payload is object[] hintPayload &&
                    hintPayload.Length >= 4 &&
                    hintPayload[0] is int hintActorNumber &&
                    hintActorNumber == photonEvent.Sender &&
                    hintPayload[1] is int roomHash &&
                    hintPayload[2] is float heightDelta &&
                    hintPayload[3] is int levelStamp)
                {
                    PlayerTruckDistanceHelper.ApplyRemotePlayerHint(hintActorNumber, roomHash, heightDelta, levelStamp);
                }
                return;
            }

            if (photonEvent.Code != PhotonEventCodes.LastChanceSurrender)
            {
                return;
            }

            var surrenderMasterActor = PhotonNetwork.MasterClient?.ActorNumber ?? -1;
            if (surrenderMasterActor <= 0 ||
                !NetworkEnvelope.TryParse(photonEvent.CustomData, out var surrenderEnvelope) ||
                !surrenderEnvelope.IsExpectedSource() ||
                !string.Equals(surrenderEnvelope.MessageType, LastChanceSurrenderMessageType, System.StringComparison.Ordinal) &&
                !string.Equals(surrenderEnvelope.MessageType, LastChanceSurrenderSnapshotMessageType, System.StringComparison.Ordinal))
            {
                return;
            }

            if (string.Equals(surrenderEnvelope.MessageType, LastChanceSurrenderSnapshotMessageType, System.StringComparison.Ordinal))
            {
                if (photonEvent.Sender != surrenderMasterActor)
                {
                    return;
                }

                LastChanceTimerController.ApplyRemoteSurrenderSnapshot(surrenderEnvelope.Payload as object[] ?? System.Array.Empty<object>());
                return;
            }

            var payloadActor = 0;
            if (surrenderEnvelope.Payload is int actorNumber)
            {
                payloadActor = actorNumber;
            }
            else if (surrenderEnvelope.Payload is object[] payload &&
                     payload.Length > 0 &&
                     payload[0] is int payloadActorInArray)
            {
                payloadActor = payloadActorInArray;
            }

            if (payloadActor <= 0)
            {
                return;
            }

            if (PhotonNetwork.IsMasterClient)
            {
                if (photonEvent.Sender <= 0 || photonEvent.Sender == surrenderMasterActor || payloadActor != photonEvent.Sender)
                {
                    return;
                }

                LastChanceTimerController.RegisterRemoteSurrender(payloadActor);
                return;
            }

            if (photonEvent.Sender != surrenderMasterActor)
            {
                return;
            }

            LastChanceTimerController.RegisterRemoteSurrender(payloadActor);
        }

        private static void ApplyExtractionReward(int rewardId, object[] rarities)
        {
            if (rewardId <= 0 ||
                rarities == null ||
                s_appliedExtractionRewardIds.Contains(rewardId) ||
                MetaManager.instance == null)
            {
                return;
            }

            foreach (var rarityPayload in rarities)
            {
                if (rarityPayload is int rarityValue &&
                    Enum.IsDefined(typeof(SemiFunc.Rarity), rarityValue))
                {
                    MetaManager.instance.CosmeticTokenAdd((SemiFunc.Rarity)rarityValue);
                }
            }

            s_appliedExtractionRewardIds.Add(rewardId);
        }

        private static NetworkEnvelope CreateEnvelope(string messageType, object? payload)
        {
            var nextSeq = unchecked(++s_messageSeq);
            return new NetworkEnvelope(
                NetworkProtocol.ModId,
                NetworkProtocol.ProtocolVersion,
                messageType,
                nextSeq,
                payload);
        }
    }
}


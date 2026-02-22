#nullable enable

using System;
using System.Collections.Generic;
using BepInEx;
using DHHFLastChanceMode.Modules.Utilities;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace DHHFLastChanceMode.Modules.Config
{
    internal enum ModFeatureGate
    {
        LastChanceCluster = 1
    }

    internal sealed class CompatibilityGate : MonoBehaviourPunCallbacks, IOnEventCallback
    {
        private const string TracePrefix = "[LastChance][CompatGate][Trace]";
        private const string LastChanceModeKey = nameof(FeatureFlags.LastChangeMode);
        private const string UnknownVersion = "unknown";
        private const string ClientFixPresenceMessageType = "ClientFixPresence";
        private const string HostFixPresenceRequestMessageType = "HostFixPresenceRequest";
        private const string HostGateStateMessageType = "HostGateState";
        private static CompatibilityGate? s_instance;
        private static bool s_hostApprovedLastChanceCluster = true;
        private static bool s_receivedHostDecision;
        private static bool s_lastAppliedRuntimeDisable;
        private static string s_lastHostDecisionReason = string.Empty;
        private static string s_lastLoggedIncompatibilityReason = string.Empty;
        private readonly Dictionary<int, string> _playersWithFixVersion = new();
        private readonly Dictionary<int, float> _pendingPresenceSince = new();
        private readonly Dictionary<int, float> _pendingPresenceNextRetryAt = new();
        private const float PresenceRetrySeconds = 0.15f;
        private const float PresenceTimeoutSeconds = 2f;

        internal static event Action? HostApprovalChanged;

        internal static void EnsureCreated()
        {
            if (s_instance != null)
            {
                Debug.Log($"{TracePrefix} EnsureCreated skipped (already created).");
                return;
            }

            var go = new GameObject("DHHFLastChanceMode.CompatibilityGate");
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<CompatibilityGate>();
            Debug.Log($"{TracePrefix} EnsureCreated created GameObject and component.");
        }

        internal static void ForceResolvePendingPresenceForStart()
        {
            s_instance?.ResolvePendingPresenceForStart();
        }

        internal static bool IsFeatureUsable(ModFeatureGate feature)
        {
            if (feature != ModFeatureGate.LastChanceCluster)
            {
                return true;
            }

            if (!PhotonNetwork.InRoom || !SemiFunc.IsMultiplayer())
            {
                return true;
            }

            if (PhotonNetwork.IsMasterClient)
            {
                return s_hostApprovedLastChanceCluster;
            }

            return s_receivedHostDecision && s_hostApprovedLastChanceCluster;
        }

        public override void OnEnable()
        {
            base.OnEnable();
            Debug.Log($"{TracePrefix} OnEnable begin.");
            PhotonNetwork.AddCallbackTarget(this);
            Debug.Log($"{TracePrefix} OnEnable added Photon callback target.");
        }

        public override void OnDisable()
        {
            base.OnDisable();
            Debug.Log($"{TracePrefix} OnDisable begin. inRoom={PhotonNetwork.InRoom} isMaster={PhotonNetwork.IsMasterClient}");
            PhotonNetwork.RemoveCallbackTarget(this);
            Debug.Log($"{TracePrefix} OnDisable removed Photon callback target.");
        }

        private void OnDestroy()
        {
            Debug.Log($"{TracePrefix} OnDestroy fired.");
            if (ReferenceEquals(s_instance, this))
            {
                s_instance = null;
                Debug.Log($"{TracePrefix} OnDestroy cleared static instance reference.");
            }
        }

        public override void OnJoinedRoom()
        {
            Debug.Log($"{TracePrefix} OnJoinedRoom begin.");
            LogJoinLeaveDebug("OnJoinedRoom");
            _playersWithFixVersion.Clear();
            _pendingPresenceSince.Clear();
            _pendingPresenceNextRetryAt.Clear();
            RegisterLocalPlayerFixVersion();
            s_receivedHostDecision = PhotonNetwork.IsMasterClient;
            s_hostApprovedLastChanceCluster = PhotonNetwork.IsMasterClient;
            s_lastHostDecisionReason = string.Empty;
            s_lastLoggedIncompatibilityReason = string.Empty;

            AnnounceLocalFixPresence();
            if (PhotonNetwork.IsMasterClient)
            {
                Debug.Log($"{TracePrefix} OnJoinedRoom host detected; deferred compatibility check.");
                LogJoinLeaveDebug("OnJoinedRoom host creation phase: compatibility check deferred");
                return;
            }
            Debug.Log($"{TracePrefix} OnJoinedRoom client: presence announced, waiting host decision.");
        }

        public override void OnLeftRoom()
        {
            LogJoinLeaveDebug("OnLeftRoom");
            _playersWithFixVersion.Clear();
            _pendingPresenceSince.Clear();
            _pendingPresenceNextRetryAt.Clear();
            s_receivedHostDecision = false;
            s_hostApprovedLastChanceCluster = true;
            s_lastHostDecisionReason = string.Empty;
            s_lastLoggedIncompatibilityReason = string.Empty;
            ApplyRuntimeHostOverrides();
            HostApprovalChanged?.Invoke();
        }

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            Debug.Log($"{TracePrefix} OnPlayerEnteredRoom player={(newPlayer == null ? "null" : FormatPlayerTag(newPlayer))}.");
            if (!PhotonNetwork.IsMasterClient)
            {
                return;
            }

            if (newPlayer != null)
            {
                _playersWithFixVersion.Remove(newPlayer.ActorNumber);
                _pendingPresenceSince[newPlayer.ActorNumber] = Time.realtimeSinceStartup;
                _pendingPresenceNextRetryAt[newPlayer.ActorNumber] = 0f;
                RequestPresenceFromActor(newPlayer.ActorNumber, "OnPlayerEnteredRoom");
            }

            EvaluateHostApprovalAndBroadcast(forceBroadcast: true);
        }

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            LogJoinLeaveDebug(otherPlayer == null
                ? "OnPlayerLeftRoom player=null"
                : $"OnPlayerLeftRoom player={FormatPlayerTag(otherPlayer)}");
            if (otherPlayer != null)
            {
                _playersWithFixVersion.Remove(otherPlayer.ActorNumber);
                _pendingPresenceSince.Remove(otherPlayer.ActorNumber);
                _pendingPresenceNextRetryAt.Remove(otherPlayer.ActorNumber);
            }

            if (!PhotonNetwork.IsMasterClient)
            {
                return;
            }

            EvaluateHostApprovalAndBroadcast(forceBroadcast: true);
        }

        public override void OnMasterClientSwitched(Player newMasterClient)
        {
            _playersWithFixVersion.Clear();
            _pendingPresenceSince.Clear();
            _pendingPresenceNextRetryAt.Clear();
            RegisterLocalPlayerFixVersion();
            s_receivedHostDecision = PhotonNetwork.IsMasterClient;
            s_hostApprovedLastChanceCluster = PhotonNetwork.IsMasterClient;
            s_lastHostDecisionReason = string.Empty;
            s_lastLoggedIncompatibilityReason = string.Empty;

            AnnounceLocalFixPresence();
            if (!PhotonNetwork.IsMasterClient)
            {
                return;
            }

            EvaluateHostApprovalAndBroadcast(forceBroadcast: true);
        }

        public void OnEvent(EventData photonEvent)
        {
            if (photonEvent == null)
            {
                return;
            }

            if (photonEvent.Code == PhotonEventCodes.ClientFixPresence)
            {
                if (!PhotonNetwork.IsMasterClient)
                {
                    return;
                }

                if (!NetworkEnvelope.TryParse(photonEvent.CustomData, out var envelope) ||
                    !envelope.IsExpectedSource() ||
                    !string.Equals(envelope.MessageType, ClientFixPresenceMessageType, StringComparison.Ordinal))
                {
                    return;
                }

                var hasPayload = TryParseClientPresencePayload(envelope.Payload, out var actorNumber, out var reportedVersion);
                if (!hasPayload || actorNumber <= 0)
                {
                    return;
                }

                if (photonEvent.Sender <= 0 || photonEvent.Sender != actorNumber)
                {
                    return;
                }

                if (_playersWithFixVersion.TryGetValue(actorNumber, out var knownVersion) &&
                    string.Equals(knownVersion, reportedVersion, StringComparison.Ordinal))
                {
                    _pendingPresenceSince.Remove(actorNumber);
                    _pendingPresenceNextRetryAt.Remove(actorNumber);
                    return;
                }

                _playersWithFixVersion[actorNumber] = reportedVersion;
                _pendingPresenceSince.Remove(actorNumber);
                _pendingPresenceNextRetryAt.Remove(actorNumber);
                Debug.Log($"{TracePrefix} ClientFixPresence received actor={actorNumber} version={reportedVersion}.");
                EvaluateHostApprovalAndBroadcast(forceBroadcast: false);
                return;
            }

            if (photonEvent.Code == PhotonEventCodes.HostFixPresenceRequest)
            {
                if (PhotonNetwork.IsMasterClient)
                {
                    return;
                }

                var localActor = PhotonNetwork.LocalPlayer?.ActorNumber ?? 0;
                if (localActor <= 0)
                {
                    return;
                }

                var masterActor = PhotonNetwork.MasterClient?.ActorNumber ?? -1;
                if (masterActor <= 0 || photonEvent.Sender != masterActor)
                {
                    return;
                }

                if (!NetworkEnvelope.TryParse(photonEvent.CustomData, out var envelope) ||
                    !envelope.IsExpectedSource() ||
                    !string.Equals(envelope.MessageType, HostFixPresenceRequestMessageType, StringComparison.Ordinal) ||
                    envelope.Payload is not int targetActor ||
                    targetActor != localActor)
                {
                    Debug.Log($"{TracePrefix} HostFixPresenceRequest ignored localActor={localActor} targetActor={(photonEvent.CustomData is int t ? t : -1)}.");
                    return;
                }

                Debug.Log($"{TracePrefix} HostFixPresenceRequest accepted localActor={localActor}, sending presence.");
                AnnounceLocalFixPresence();
                return;
            }

            if (photonEvent.Code != PhotonEventCodes.HostGateState)
            {
                return;
            }

            var hostActor = PhotonNetwork.MasterClient?.ActorNumber ?? -1;
            if (hostActor <= 0 || photonEvent.Sender != hostActor)
            {
                return;
            }

            if (!NetworkEnvelope.TryParse(photonEvent.CustomData, out var envelopeHostGate) ||
                !envelopeHostGate.IsExpectedSource() ||
                !string.Equals(envelopeHostGate.MessageType, HostGateStateMessageType, StringComparison.Ordinal) ||
                envelopeHostGate.Payload is not object[] payload ||
                payload.Length < 1 ||
                payload[0] is not bool allowed)
            {
                return;
            }

            var reason = payload.Length >= 2 && payload[1] is string rawReason ? rawReason : string.Empty;
            var changed = !s_receivedHostDecision || s_hostApprovedLastChanceCluster != allowed;
            s_receivedHostDecision = true;
            s_hostApprovedLastChanceCluster = allowed;
            s_lastHostDecisionReason = reason;
            if (!allowed)
            {
                EmitIncompatibilityWarning(reason);
            }
            if (changed)
            {
                HostApprovalChanged?.Invoke();
            }
        }

        private static void AnnounceLocalFixPresence()
        {
            if (!PhotonNetwork.InRoom)
            {
                Debug.Log($"{TracePrefix} AnnounceLocalFixPresence skipped (not in room).");
                return;
            }

            var actor = PhotonNetwork.LocalPlayer?.ActorNumber ?? 0;
            if (actor <= 0)
            {
                Debug.Log($"{TracePrefix} AnnounceLocalFixPresence skipped (invalid actor).");
                return;
            }

            var options = new RaiseEventOptions
            {
                Receivers = ReceiverGroup.MasterClient
            };

            var envelope = new NetworkEnvelope(
                NetworkProtocol.ModId,
                NetworkProtocol.ProtocolVersion,
                ClientFixPresenceMessageType,
                0,
                new object[] { actor, GetLocalFixVersion() });
            PhotonNetwork.RaiseEvent(PhotonEventCodes.ClientFixPresence, envelope.ToEventPayload(), options, SendOptions.SendReliable);
            Debug.Log($"{TracePrefix} AnnounceLocalFixPresence sent actor={actor} version={GetLocalFixVersion()}.");
        }

        private void RequestPresenceFromActor(int actorNumber, string source)
        {
            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || actorNumber <= 0)
            {
                return;
            }

            var options = new RaiseEventOptions
            {
                TargetActors = new[] { actorNumber }
            };

            var envelope = new NetworkEnvelope(
                NetworkProtocol.ModId,
                NetworkProtocol.ProtocolVersion,
                HostFixPresenceRequestMessageType,
                0,
                actorNumber);
            PhotonNetwork.RaiseEvent(PhotonEventCodes.HostFixPresenceRequest, envelope.ToEventPayload(), options, SendOptions.SendReliable);
            Debug.Log($"{TracePrefix} RequestPresenceFromActor sent actor={actorNumber} source={source}.");
        }

        private void Update()
        {
            if (!PhotonNetwork.IsMasterClient || !PhotonNetwork.InRoom || _pendingPresenceSince.Count == 0)
            {
                return;
            }

            var now = Time.realtimeSinceStartup;
            var needsRecheck = false;
            var actors = new List<int>(_pendingPresenceSince.Keys);
            for (var i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                if (_playersWithFixVersion.ContainsKey(actor))
                {
                    _pendingPresenceSince.Remove(actor);
                    _pendingPresenceNextRetryAt.Remove(actor);
                    continue;
                }

                if (!_pendingPresenceNextRetryAt.TryGetValue(actor, out var nextRetryAt) || now >= nextRetryAt)
                {
                    RequestPresenceFromActor(actor, "UpdateRetry");
                    _pendingPresenceNextRetryAt[actor] = now + PresenceRetrySeconds;
                }

                if (!_pendingPresenceSince.TryGetValue(actor, out var pendingSince))
                {
                    continue;
                }

                var elapsed = now - pendingSince;
                if (elapsed >= PresenceTimeoutSeconds)
                {
                    _pendingPresenceSince.Remove(actor);
                    _pendingPresenceNextRetryAt.Remove(actor);
                    needsRecheck = true;
                    Debug.Log($"{TracePrefix} Pending presence timeout reached actor={actor} elapsed={elapsed:0.000}s. Marking as missing and stopping retries.");
                }
            }

            if (needsRecheck)
            {
                Debug.Log($"{TracePrefix} Update forcing compatibility recheck due to pending timeout.");
                EvaluateHostApprovalAndBroadcast(forceBroadcast: true);
            }
        }

        private void EvaluateHostApprovalAndBroadcast(bool forceBroadcast)
        {
            if (!PhotonNetwork.InRoom || !SemiFunc.IsMultiplayer())
            {
                s_hostApprovedLastChanceCluster = true;
                s_receivedHostDecision = PhotonNetwork.IsMasterClient;
                ApplyRuntimeHostOverrides();
                HostApprovalChanged?.Invoke();
                return;
            }

            if (!PhotonNetwork.IsMasterClient)
            {
                ApplyRuntimeHostOverrides();
                HostApprovalChanged?.Invoke();
                return;
            }

            RegisterLocalPlayerFixVersion();

            var localVersion = GetLocalFixVersion();
            var missingPlayers = new List<string>();
            var pendingPlayers = new List<string>();
            var mismatchPlayers = new List<string>();
            var allPlayersCompatible = true;
            var players = PhotonNetwork.PlayerList;
            var now = Time.realtimeSinceStartup;
            for (var i = 0; i < players.Length; i++)
            {
                var player = players[i];
                if (player == null)
                {
                    continue;
                }

                if (!_playersWithFixVersion.TryGetValue(player.ActorNumber, out var remoteVersion))
                {
                    var playerTag = FormatPlayerTag(player);
                    if (_pendingPresenceSince.TryGetValue(player.ActorNumber, out var since))
                    {
                        var elapsed = now - since;
                        if (elapsed < PresenceTimeoutSeconds)
                        {
                            pendingPlayers.Add($"{playerTag} ({elapsed:0.0}s/{PresenceTimeoutSeconds:0.0}s)");
                            allPlayersCompatible = false;
                            continue;
                        }
                    }

                    allPlayersCompatible = false;
                    missingPlayers.Add(playerTag);
                    continue;
                }

                if (!string.Equals(remoteVersion, localVersion, StringComparison.Ordinal))
                {
                    allPlayersCompatible = false;
                    mismatchPlayers.Add($"{FormatPlayerTag(player)}={remoteVersion}");
                }
            }

            var reason = BuildIncompatibilityReason(localVersion, missingPlayers, mismatchPlayers, pendingPlayers);
            var changed = s_hostApprovedLastChanceCluster != allPlayersCompatible ||
                          !s_receivedHostDecision ||
                          !string.Equals(s_lastHostDecisionReason, reason, StringComparison.Ordinal);
            s_hostApprovedLastChanceCluster = allPlayersCompatible;
            s_receivedHostDecision = true;
            s_lastHostDecisionReason = reason;
            ApplyRuntimeHostOverrides();

            if (changed || forceBroadcast)
            {
                BroadcastHostApproval();
                HostApprovalChanged?.Invoke();
            }
        }

        private void ResolvePendingPresenceForStart()
        {
            if (!PhotonNetwork.IsMasterClient || !PhotonNetwork.InRoom || _pendingPresenceSince.Count == 0)
            {
                return;
            }

            var now = Time.realtimeSinceStartup;
            var actors = new List<int>(_pendingPresenceSince.Keys);
            for (var i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                if (_playersWithFixVersion.ContainsKey(actor))
                {
                    _pendingPresenceSince.Remove(actor);
                    _pendingPresenceNextRetryAt.Remove(actor);
                    continue;
                }

                var elapsed = _pendingPresenceSince.TryGetValue(actor, out var since) ? now - since : 0f;
                _pendingPresenceSince.Remove(actor);
                _pendingPresenceNextRetryAt.Remove(actor);
                Debug.Log($"{TracePrefix} Start pressed with unresolved presence actor={actor} elapsed={elapsed:0.000}s. Marking as missing immediately.");
            }

            Debug.Log($"{TracePrefix} Start forced compatibility recheck after pending presence flush.");
            EvaluateHostApprovalAndBroadcast(forceBroadcast: true);
        }

        private static void BroadcastHostApproval()
        {
            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
            {
                return;
            }

            var options = new RaiseEventOptions
            {
                Receivers = ReceiverGroup.All
            };

            var envelope = new NetworkEnvelope(
                NetworkProtocol.ModId,
                NetworkProtocol.ProtocolVersion,
                HostGateStateMessageType,
                0,
                new object[] { s_hostApprovedLastChanceCluster, s_lastHostDecisionReason });
            PhotonNetwork.RaiseEvent(PhotonEventCodes.HostGateState, envelope.ToEventPayload(), options, SendOptions.SendReliable);
        }

        private static void ApplyRuntimeHostOverrides()
        {
            if (!PhotonNetwork.IsMasterClient)
            {
                return;
            }

            var shouldDisable = !s_hostApprovedLastChanceCluster;
            if (s_lastAppliedRuntimeDisable == shouldDisable)
            {
                return;
            }

            s_lastAppliedRuntimeDisable = shouldDisable;
            if (shouldDisable)
            {
                ConfigManager.SetHostRuntimeOverride(LastChanceModeKey, bool.FalseString);
                EmitIncompatibilityWarning(s_lastHostDecisionReason);
            }
            else
            {
                ConfigManager.ClearHostRuntimeOverride(LastChanceModeKey);
                s_lastLoggedIncompatibilityReason = string.Empty;
            }
        }

        private static void EmitIncompatibilityWarning(string reason)
        {
            var normalizedReason = string.IsNullOrWhiteSpace(reason) ? "Unknown incompatibility reason." : reason.Trim();
            if (string.Equals(s_lastLoggedIncompatibilityReason, normalizedReason, StringComparison.Ordinal))
            {
                return;
            }

            s_lastLoggedIncompatibilityReason = normalizedReason;
            Debug.LogWarning($"[LastChance] LastChange disabled due to incompatibility: {normalizedReason}");
        }

        private static bool TryParseClientPresencePayload(object? customData, out int actorNumber, out string version)
        {
            actorNumber = 0;
            version = UnknownVersion;

            if (customData is int legacyActor)
            {
                actorNumber = legacyActor;
                return true;
            }

            if (customData is object[] payload &&
                payload.Length >= 2 &&
                payload[0] is int actor &&
                payload[1] is string payloadVersion)
            {
                actorNumber = actor;
                version = NormalizeVersion(payloadVersion);
                return true;
            }

            return false;
        }

        private void RegisterLocalPlayerFixVersion()
        {
            var actor = PhotonNetwork.LocalPlayer?.ActorNumber ?? 0;
            if (actor <= 0)
            {
                return;
            }

            _playersWithFixVersion[actor] = GetLocalFixVersion();
        }

        private static string GetLocalFixVersion()
        {
            return NormalizeVersion(GetPluginVersionRaw());
        }

        private static string NormalizeVersion(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return UnknownVersion;
            }

            return value!.Trim();
        }

        private static string? GetPluginVersionRaw()
        {
            var attr = (BepInPlugin?)Attribute.GetCustomAttribute(typeof(Plugin), typeof(BepInPlugin));
            return attr?.Version?.ToString();
        }

        private static string BuildIncompatibilityReason(
            string localVersion,
            List<string> missingPlayers,
            List<string> mismatchPlayers,
            List<string> pendingPlayers)
        {
            var reasonParts = new List<string>();
            if (pendingPlayers.Count > 0)
            {
                reasonParts.Add("awaiting fix presence from: " + string.Join(", ", pendingPlayers));
            }

            if (missingPlayers.Count > 0)
            {
                reasonParts.Add("missing fix presence from: " + string.Join(", ", missingPlayers));
            }

            if (mismatchPlayers.Count > 0)
            {
                reasonParts.Add($"version mismatch (host={localVersion}): " + string.Join(", ", mismatchPlayers));
            }

            return reasonParts.Count == 0 ? string.Empty : string.Join(" | ", reasonParts);
        }

        private static string FormatPlayerTag(Player player)
        {
            var name = string.IsNullOrWhiteSpace(player.NickName) ? "unknown" : player.NickName.Trim();
            return $"{name}#{player.ActorNumber}";
        }

        private static void LogJoinLeaveDebug(string message)
        {
            if (!FeatureFlags.DebugLogging)
            {
                return;
            }

            var inRoom = PhotonNetwork.InRoom;
            var isMaster = PhotonNetwork.IsMasterClient;
            var actor = PhotonNetwork.LocalPlayer?.ActorNumber ?? 0;
            Debug.Log($"[LastChance][CompatGate] {message} | inRoom={inRoom}, isMaster={isMaster}, localActor={actor}");
        }

    }
}

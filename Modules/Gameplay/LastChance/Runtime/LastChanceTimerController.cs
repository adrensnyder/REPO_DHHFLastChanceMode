#nullable enable

using System;
using System.Collections.Generic;
using BepInEx.Logging;
using DHHFLastChanceMode.Modules.Config;
using DeathHeadHopperFix.API.Battery;
using DHHFLastChanceMode.Modules.Utilities;
using DHHFLastChanceMode.Modules.Gameplay.Core.Abilities;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.UI;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Spectate;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Runtime
{
    // RunManager.Update postfix drives LastChance runtime/timer state every frame.
    // Vanilla all-dead suppression is handled separately by AllPlayersDeadGuard:
    // Update postfix guards allPlayersDead assignment and ChangeLevel prefix blocks fail transition.
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.Update))]
    internal static class RunManagerUpdateLastChanceTimerPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            LastChanceTimerController.Tick();
        }
    }

    internal static class LastChanceTimerController
    {
        private static readonly ManualLogSource Log = Logger.CreateLogSource("DHHFLastChanceMode.LastChance.Runtime");
        private const string LogKey = "LastChance.Timer";
        private const string TimerSecondAudioFileName = "TimerSecond.mp3";
        private const string TimerWarningAudioPrimaryFileName = "TimerWarning.mp3";
        private static float s_timerRemaining;
        private static bool s_active;
        private static int s_baseCurrency;
        private static bool s_currencyCaptured;
        private static bool s_successHandled;
        private static bool s_consolationMoneyPending;
        private static readonly Color TimerColor = new(1f, 0.85f, 0.1f, 1f);
        private static readonly Color FlashColor = new(1f, 0.2f, 0.2f, 1f);
        private static readonly InputKey SurrenderInputKey = InputKey.Crouch;
        private const string SurrenderHintPromptFormat = "Back to truck, hop hop! (Surrender [{0}])";
        private const string SurrenderCountdownFormat = "Surrender in {0}s";
        private const string SurrenderedHintText = "Surrendered <3";
        private const string LocalSurrenderedHintText = "You surrendered <3";
        private const string IndicatorLogKey = "LastChance.Indicator";
        private const string IndicatorCooldownLogKey = "LastChance.Indicator.Cooldown";
        private static readonly Vector2 DirectionLineScrollSpeed = new(4f, 0f);
        private const float DirectionLineHeightOffset = 0.2f;
        private const float DirectionPathRefreshSeconds = 0.4f;
        private const float DirectionPathMovementThresholdSqr = 0.64f; // 0.8m
        private const float DirectionIndicatorHoldSeconds = 1f;
        private const float DirectionIndicatorMinimumTimerSeconds = 30f;

        private enum LastChanceIndicatorMode
        {
            None = 0,
            Direction = 1
        }

        private enum IndicatorKind
        {
            Direction = 1
        }

        private enum TimerChangeReason
        {
            CountdownTick = 0,
            MonsterKillBonus = 1,
            DirectionPenalty = 2,
            NetworkSync = 3
        }

        private static float SurrenderHoldDuration => Mathf.Clamp(FeatureFlags.LastChanceSurrenderSeconds, 2f, 10f);
        private static readonly HashSet<int> LastChanceSurrenderedPlayers = new();
        private static float s_surrenderHoldTimer;
        private static bool s_localSurrendered;
        private static bool s_surrenderDistanceLogged;
        private static float s_directionCooldownUntil;
        private static float s_directionActiveUntil;
        private static bool s_directionActive;
        private static float s_directionHoldTimer;
        private static bool s_indicatorNoneLoggedThisCycle;
        private static GameObject? s_indicatorDirectionObject;
        private static LineRenderer? s_indicatorDirectionLine;
        private static Material? s_indicatorDirectionMaterial;
        private static float s_indicatorNextPathRefreshAt;
        private static Vector3 s_lastDirectionPathFrom;
        private static Vector3 s_lastDirectionPathTo;
        private static bool s_hasLastDirectionPathSample;
        private static AudioSource? s_timerSecondAudioSource;
        private static AudioClip? s_timerSecondAudioClip;
        private static bool s_timerSecondAudioLoadAttempted;
        private static float s_nextTimerSecondAudioRetryAt;
        private static int s_lastTimerSecondAudioPlayed = -1;
        private static AudioSource? s_timerWarningAudioSource;
        private static AudioClip? s_timerWarningAudioClip;
        private static bool s_timerWarningAudioLoadAttempted;
        private static float s_nextTimerWarningAudioRetryAt;
        private static int s_lastTimerWarningAudioPlayed = -1;
        private static float s_previousTimerWarningCheckSeconds = float.NaN;
        private static int s_lastNetworkTimerBroadcastSecond = -1;
        private static bool s_timerSyncedFromHost;
        private static bool s_hasNetworkUiState;
        private static int s_networkUiRequiredOnTruck;
        private static readonly Dictionary<int, NetworkUiPlayerState> s_networkUiStatesByActor = new();
        private static readonly Dictionary<int, float> s_nextDirectionPenaltyAllowedAtByActor = new();
        private static float s_lastUiStateBroadcastAt;
        private static int s_lastUiStateHash;
        private static bool s_suppressedForRoom;
        private static bool s_suppressedLogEmitted;
        private const string LastChanceBatteryLeaseOwnerId = "AdrenSnyder.DHHFLastChanceMode.Battery";
        private static bool s_lastChanceBatteryOverrideApplied;
        private static DynamicTimerInputs s_cachedDynamicTimerInputs;
        private static bool s_hasCachedDynamicTimerInputs;
        private static string? s_lastTruckStateDebugMessage;
        private const float UiStateBroadcastIntervalSeconds = 0.2f;
        private const float DirectionPenaltyRequestCooldownSeconds = 0.2f;
        private const float TimerDriftHardSnapSeconds = 1.25f;
        private const float TimerDriftLerpFactor = 0.35f;
        private static bool s_activationProfilePending;
        private static float s_activationProfileStartedAt;
        private static DynamicTimerProfileSnapshot s_lastDynamicTimerProfile;
        private static bool s_hasDynamicTimerProfile;
        private static ActivationStartPhaseProfileSnapshot s_lastActivationStartPhaseProfile;
        private static bool s_hasActivationStartPhaseProfile;
        private static bool s_assetsPrewarmedForSession;
        private static float s_cachedDirectionPenaltySeconds;
        private static bool s_hasCachedDirectionPenaltySeconds;
        private const float AssetAudioRetryIntervalSeconds = 2f;

        private readonly struct NetworkUiPlayerState
        {
            internal NetworkUiPlayerState(bool isInTruck, bool isSurrendered)
            {
                IsInTruck = isInTruck;
                IsSurrendered = isSurrendered;
            }

            internal bool IsInTruck { get; }
            internal bool IsSurrendered { get; }
        }

        private enum ReturnPathSource
        {
            None,
            NavMesh,
            RoomPathFallback,
            UnresolvedFallback
        }

        private readonly struct ReturnCostCandidate
        {
            internal ReturnCostCandidate(
                PlayerAvatar? playerAvatar,
                int actorNumber,
                float navMeshDistance,
                float effectiveDistanceMeters,
                int effectiveRoomSteps,
                float heightDelta,
                float distanceCostSeconds,
                float roomCostSeconds,
                float verticalCostSeconds,
                float returnCostSeconds,
                ReturnPathSource pathSource)
            {
                PlayerAvatar = playerAvatar;
                ActorNumber = actorNumber;
                NavMeshDistance = navMeshDistance;
                EffectiveDistanceMeters = effectiveDistanceMeters;
                EffectiveRoomSteps = effectiveRoomSteps;
                HeightDelta = heightDelta;
                DistanceCostSeconds = distanceCostSeconds;
                RoomCostSeconds = roomCostSeconds;
                VerticalCostSeconds = verticalCostSeconds;
                ReturnCostSeconds = returnCostSeconds;
                PathSource = pathSource;
            }

            internal PlayerAvatar? PlayerAvatar { get; }
            internal int ActorNumber { get; }
            internal float NavMeshDistance { get; }
            internal float EffectiveDistanceMeters { get; }
            internal int EffectiveRoomSteps { get; }
            internal float HeightDelta { get; }
            internal float DistanceCostSeconds { get; }
            internal float RoomCostSeconds { get; }
            internal float VerticalCostSeconds { get; }
            internal float ReturnCostSeconds { get; }
            internal ReturnPathSource PathSource { get; }
        }

        private readonly struct RepoDifficultySnapshot
        {
            internal RepoDifficultySnapshot(float difficulty1, float difficulty2, float difficulty3, bool usedFallback)
            {
                Difficulty1 = difficulty1;
                Difficulty2 = difficulty2;
                Difficulty3 = difficulty3;
                Progress = Mathf.Clamp01((difficulty1 + difficulty2 + difficulty3) / 3f);
                UsedFallback = usedFallback;
            }

            internal float Difficulty1 { get; }
            internal float Difficulty2 { get; }
            internal float Difficulty3 { get; }
            internal float Progress { get; }
            internal bool UsedFallback { get; }
        }

        private readonly struct DynamicTimerInputs
        {
            internal DynamicTimerInputs(
                int requiredPlayers,
                int levelNumber,
                int aliveSearchMonsters,
                int candidateCount,
                int criticalActorNumber,
                int criticalPlayerInstanceId,
                float criticalNavMeshDistance,
                float criticalEffectiveDistanceMeters,
                int criticalRoomSteps,
                float criticalHeightDelta,
                float criticalDistanceCostSeconds,
                float criticalRoomCostSeconds,
                float criticalVerticalCostSeconds,
                float criticalReturnCostSeconds,
                ReturnPathSource criticalPathSource)
            {
                RequiredPlayers = requiredPlayers;
                LevelNumber = levelNumber;
                AliveSearchMonsters = aliveSearchMonsters;
                CandidateCount = candidateCount;
                CriticalActorNumber = criticalActorNumber;
                CriticalPlayerInstanceId = criticalPlayerInstanceId;
                CriticalNavMeshDistance = criticalNavMeshDistance;
                CriticalEffectiveDistanceMeters = criticalEffectiveDistanceMeters;
                CriticalRoomSteps = criticalRoomSteps;
                CriticalHeightDelta = criticalHeightDelta;
                CriticalDistanceCostSeconds = criticalDistanceCostSeconds;
                CriticalRoomCostSeconds = criticalRoomCostSeconds;
                CriticalVerticalCostSeconds = criticalVerticalCostSeconds;
                CriticalReturnCostSeconds = criticalReturnCostSeconds;
                CriticalPathSource = criticalPathSource;
            }

            internal int RequiredPlayers { get; }
            internal int LevelNumber { get; }
            internal int AliveSearchMonsters { get; }
            internal int CandidateCount { get; }
            internal int CriticalActorNumber { get; }
            internal int CriticalPlayerInstanceId { get; }
            internal float CriticalNavMeshDistance { get; }
            internal float CriticalEffectiveDistanceMeters { get; }
            internal int CriticalRoomSteps { get; }
            internal float CriticalHeightDelta { get; }
            internal float CriticalDistanceCostSeconds { get; }
            internal float CriticalRoomCostSeconds { get; }
            internal float CriticalVerticalCostSeconds { get; }
            internal float CriticalReturnCostSeconds { get; }
            internal ReturnPathSource CriticalPathSource { get; }
        }

        private readonly struct DynamicTimerProfileSnapshot
        {
            internal DynamicTimerProfileSnapshot(
                float totalMs,
                float monstersMs,
                float recordsMs,
                float candidateBuildMs,
                float criticalSelectMs,
                float aggregateMs,
                int recordsCount,
                int candidateCount,
                int requiredPlayers,
                int levelNumber,
                int aliveMonsters)
            {
                TotalMs = totalMs;
                MonstersMs = monstersMs;
                RecordsMs = recordsMs;
                CandidateBuildMs = candidateBuildMs;
                CriticalSelectMs = criticalSelectMs;
                AggregateMs = aggregateMs;
                RecordsCount = recordsCount;
                CandidateCount = candidateCount;
                RequiredPlayers = requiredPlayers;
                LevelNumber = levelNumber;
                AliveMonsters = aliveMonsters;
            }

            internal float TotalMs { get; }
            internal float MonstersMs { get; }
            internal float RecordsMs { get; }
            internal float CandidateBuildMs { get; }
            internal float CriticalSelectMs { get; }
            internal float AggregateMs { get; }
            internal int RecordsCount { get; }
            internal int CandidateCount { get; }
            internal int RequiredPlayers { get; }
            internal int LevelNumber { get; }
            internal int AliveMonsters { get; }
        }

        private readonly struct ActivationStartPhaseProfileSnapshot
        {
            internal ActivationStartPhaseProfileSnapshot(
                float totalMs,
                float setActiveMs,
                float initialTimerMs,
                float captureCurrencyMs,
                float ensureNetworkMs,
                float showUiMs,
                float clearStateMs,
                float broadcastMs,
                float debugExtrasMs)
            {
                TotalMs = totalMs;
                SetActiveMs = setActiveMs;
                InitialTimerMs = initialTimerMs;
                CaptureCurrencyMs = captureCurrencyMs;
                EnsureNetworkMs = ensureNetworkMs;
                ShowUiMs = showUiMs;
                ClearStateMs = clearStateMs;
                BroadcastMs = broadcastMs;
                DebugExtrasMs = debugExtrasMs;
            }

            internal float TotalMs { get; }
            internal float SetActiveMs { get; }
            internal float InitialTimerMs { get; }
            internal float CaptureCurrencyMs { get; }
            internal float EnsureNetworkMs { get; }
            internal float ShowUiMs { get; }
            internal float ClearStateMs { get; }
            internal float BroadcastMs { get; }
            internal float DebugExtrasMs { get; }
        }

        internal static bool IsActive => s_active;
        internal static bool IsSuppressedForRoom => s_suppressedForRoom;
        internal static bool IsDirectionIndicatorUiVisible =>
            s_active &&
            AllPlayersDeadGuard.AllPlayersDisabled() &&
            GetIndicatorMode() == LastChanceIndicatorMode.Direction;
        internal static void OnHostControlledConfigChanged()
        {
            // Keep runtime caches/states coherent when host-controlled flags are changed live.
            ClearCachedDynamicTimerInputs();
            ClearDirectionPenaltyCache();
            LastChanceMonstersNoiseAggroModule.ResetRuntimeState();
            LastChanceMonstersSearchModule.ResetRuntimeState();
            LastChanceMonstersVoiceEnemyOnlyModule.ResetRuntimeState();
            LastChanceMonstersCameraForceLockModule.ResetRuntimeState();
            LastChanceMonstersPlayerVisionCheckModule.ResetRuntimeState();
            LastChanceMonstersAnimalHeadVisionFallbackModule.ResetRuntimeState();
            LastChanceMonstersCarryProxyModule.ResetRuntimeState();
            LastChanceMonstersOnScreenCameraModule.ResetRuntimeState();
            LastChanceMonstersThinManStandModule.ResetRuntimeState();
            LastChanceHeadPupilVisualModule.ResetRuntimeState();
            LastChanceHeadEyesOverrideBypassModule.ResetRuntimeState();
        }

        internal static float GetDirectionIndicatorPenaltySecondsPreview()
        {
            if (!IsDirectionIndicatorUiVisible)
            {
                return 0f;
            }

            var maxPlayers = GetRunPlayerCount();
            if (maxPlayers <= 0)
            {
                return 0f;
            }

            return GetOrComputeDirectionPenaltySeconds();
        }

        internal static bool IsDirectionIndicatorEnergySufficientPreview()
        {
            if (!IsDirectionIndicatorUiVisible)
            {
                return false;
            }

            var penaltyPreview = GetDirectionIndicatorPenaltySecondsPreview();
            if (penaltyPreview <= 0f)
            {
                return false;
            }

            return HasEnoughTimerForDirectionPenalty(penaltyPreview);
        }

        internal static void GetDirectionIndicatorEnergyDebugSnapshot(
            out bool visible,
            out float timerRemaining,
            out float penaltyPreview,
            out bool hasEnoughEnergy)
        {
            visible = IsDirectionIndicatorUiVisible;
            timerRemaining = s_timerRemaining;
            penaltyPreview = visible ? GetDirectionIndicatorPenaltySecondsPreview() : 0f;
            hasEnoughEnergy = visible && penaltyPreview > 0f && timerRemaining >= penaltyPreview;
        }

        internal static void OnLevelLoaded(bool shouldPrewarmAssets)
        {
            LastChanceSpectateHelper.ResetOwnedState();
            LastChanceRuntimeOrchestrator.OnLevelTransition();
            s_suppressedForRoom = false;
            s_suppressedLogEmitted = false;
            ClearSurrenderState();
            ClearCachedDynamicTimerInputs();
            ClearLastChanceHostRuntimeOverrides();
            ClearActivationProfileState();
            s_assetsPrewarmedForSession = false;
            LastChanceTimerUI.DestroyUi();

            if (shouldPrewarmAssets)
            {
                // Preload UI/audio assets on gameplay scene load to avoid activation hitch when LastChance starts.
                PrewarmGlobalAssetsAtBoot();
            }

            if (!s_active)
            {
                LastChanceTimerUI.Hide();
                ResetLastChanceRuntimeModules(allowVanillaAllPlayersDead: false, allowAutoDelete: false);
                return;
            }

            SetLastChanceActive(false);
            s_currencyCaptured = false;
            s_successHandled = false;
            s_consolationMoneyPending = false;
            s_timerRemaining = 0f;
            s_timerSyncedFromHost = false;
            LastChanceTimerUI.Hide();
            ResetLastChanceRuntimeModules(allowVanillaAllPlayersDead: false, allowAutoDelete: false);
        }

        internal static void Tick()
        {
            if (!FeatureFlags.LastChangeMode)
            {
                ResetState();
                return;
            }

            if (!CompatibilityGate.IsFeatureUsable(ModFeatureGate.LastChanceCluster))
            {
                ResetState();
                return;
            }

            if (s_suppressedForRoom)
            {
                ForceStopRuntimeState();
                return;
            }

            if (!IsValidRunContext())
            {
                ResetState();
                return;
            }

            if (SemiFunc.IsMultiplayer())
            {
                LastChanceSurrenderNetwork.TryBroadcastLocalPlayerTruckHint();
            }

            var allDead = AllPlayersDeadGuard.AllPlayersDisabled();
            if (!allDead)
            {
                PrewarmLastChanceAssets();
                // Pre-warm heavy distance/path data before LastChance starts to reduce activation hitch.
                if (!SemiFunc.IsMultiplayer() || SemiFunc.IsMasterClient())
                {
                    PlayerTruckDistanceHelper.PrimeDistancesCache();
                }
                ResetState();
                return;
            }

            var maxPlayers = GetRunPlayerCount();
            if (maxPlayers <= 0)
            {
                ResetState();
                return;
            }

            if (!s_active)
            {
                BeginActivationProfile();
                StartTimer(maxPlayers);
                EmitActivationProfileSummary();
            }

            UpdateTimer();
            UpdateSurrenderInput(allDead);
            UpdateIndicators(maxPlayers, allDead);
            UpdatePlayersStatusUi(maxPlayers);

            if (SemiFunc.IsMultiplayer() && !SemiFunc.IsMasterClient())
            {
                return;
            }

            if (CheckSurrenderFailure(maxPlayers))
            {
                return;
            }

            DebugTruckState(allDead);

            if (AllHeadsInTruck())
            {
                HandleSuccess();
                return;
            }

            if (s_timerRemaining <= 0f)
            {
                if (SemiFunc.IsMultiplayer() && !SemiFunc.IsMasterClient() && !s_timerSyncedFromHost)
                {
                    return;
                }
                HandleTimeout();
            }
        }

        private static void StartTimer(int maxPlayers)
        {
            var profileEnabled = FeatureFlags.DebugLogging && s_activationProfilePending;
            var profileStart = profileEnabled ? Time.realtimeSinceStartup : 0f;
            var afterSetActive = profileStart;
            var afterInitialTimer = profileStart;
            var afterCaptureCurrency = profileStart;
            var afterEnsureNetwork = profileStart;
            var afterShowUi = profileStart;
            var afterClearState = profileStart;
            var afterBroadcast = profileStart;

            SetLastChanceActive(true);
            if (profileEnabled)
            {
                afterSetActive = Time.realtimeSinceStartup;
            }

            if (SemiFunc.IsMultiplayer() && !SemiFunc.IsMasterClient())
            {
                s_timerRemaining = Mathf.Max(30f, GetConfiguredSeconds());
                s_timerSyncedFromHost = false;
            }
            else
            {
                s_timerRemaining = GetInitialTimerSeconds(maxPlayers);
                s_timerSyncedFromHost = true;
            }
            if (profileEnabled)
            {
                afterInitialTimer = Time.realtimeSinceStartup;
            }

            s_lastTimerSecondAudioPlayed = -1;
            s_lastTimerWarningAudioPlayed = -1;
            s_previousTimerWarningCheckSeconds = s_timerRemaining;
            s_lastNetworkTimerBroadcastSecond = -1;
            s_currencyCaptured = false;
            s_successHandled = false;
            s_consolationMoneyPending = false;
            s_indicatorNoneLoggedThisCycle = false;
            CaptureBaseCurrency();
            if (profileEnabled)
            {
                afterCaptureCurrency = Time.realtimeSinceStartup;
            }

            LastChanceSurrenderNetwork.EnsureCreated();
            if (profileEnabled)
            {
                afterEnsureNetwork = Time.realtimeSinceStartup;
            }

            LastChanceTimerUI.Show(GetSurrenderHintPrompt());
            if (profileEnabled)
            {
                afterShowUi = Time.realtimeSinceStartup;
            }

            s_surrenderDistanceLogged = false;
            s_hasNetworkUiState = false;
            s_networkUiRequiredOnTruck = 0;
            s_networkUiStatesByActor.Clear();
            s_nextDirectionPenaltyAllowedAtByActor.Clear();
            s_lastUiStateBroadcastAt = 0f;
            s_lastUiStateHash = 0;
            CacheDirectionPenaltySeconds();
            if (profileEnabled)
            {
                afterClearState = Time.realtimeSinceStartup;
            }

            BroadcastTimerStateIfHost(force: true);
            if (profileEnabled)
            {
                afterBroadcast = Time.realtimeSinceStartup;
            }

            if (FeatureFlags.DebugLogging)
            {
                LastChanceTruckDistanceLogger.LogDistances();
            }

            Log.LogInfo($"[LastChance] Runtime activated. Timer started: {s_timerRemaining:F1}s.");

            if (profileEnabled)
            {
                var profileEnd = Time.realtimeSinceStartup;
                s_lastActivationStartPhaseProfile = new ActivationStartPhaseProfileSnapshot(
                    (profileEnd - profileStart) * 1000f,
                    (afterSetActive - profileStart) * 1000f,
                    (afterInitialTimer - afterSetActive) * 1000f,
                    (afterCaptureCurrency - afterInitialTimer) * 1000f,
                    (afterEnsureNetwork - afterCaptureCurrency) * 1000f,
                    (afterShowUi - afterEnsureNetwork) * 1000f,
                    (afterClearState - afterShowUi) * 1000f,
                    (afterBroadcast - afterClearState) * 1000f,
                    (profileEnd - afterBroadcast) * 1000f);
                s_hasActivationStartPhaseProfile = true;
            }
        }

        private static void UpdateTimer()
        {
            ApplyTimerDelta(-Time.deltaTime, TimerChangeReason.CountdownTick, broadcastIfHost: true, forceBroadcastIfHost: false);
            TryPlayLastChanceTimerSecondTick();
        }

        private static void HandleTimeout()
        {
            FailLastChance("[LastChance] Timer expired; resuming vanilla all-dead flow.");
        }

        private static void HandleSuccess()
        {
            if (s_successHandled)
            {
                return;
            }

            s_successHandled = true;
            if (FeatureFlags.DebugLogging && LogLimiter.ShouldLog(LogKey, 30))
            {
                Log.LogDebug("[LastChance] All heads in truck; sending to shop.");
            }

            LastChanceTimerUI.Hide();
            SetLastChanceActive(false);
            ResetLastChanceRuntimeModules(allowVanillaAllPlayersDead: false, allowAutoDelete: false);
            s_timerSyncedFromHost = false;
            StopTimerSecondAudio();
            BroadcastTimerStateIfHost(force: true);

            if (!SemiFunc.IsMasterClientOrSingleplayer())
            {
                return;
            }

            var runMgr = RunManager.instance;
            if (runMgr == null || IsRestarting(runMgr))
            {
                return;
            }

            CaptureBaseCurrency();
            var preservedCurrency = FeatureFlags.LastChancePreserveExtractedMoney ? s_baseCurrency : 0;
            var targetCurrency = 0;
            var primaryTargetAvailable = TryCalculatePrimaryConsolationTarget(out targetCurrency);
            s_consolationMoneyPending = !primaryTargetAvailable;
            var newCurrency = Mathf.Max(preservedCurrency, targetCurrency);

            if (FeatureFlags.DebugLogging)
            {
                var extractionPointsCompleted = RoundDirector.instance?.extractionPointsCompleted ?? -1;
                var extractionPointsTotal = RoundDirector.instance?.extractionPoints ?? -1;
                var gateStatus = extractionPointsCompleted > 0
                    ? "blocked"
                    : extractionPointsCompleted == 0
                        ? "passed"
                        : "unavailable";
                var gateReason = extractionPointsCompleted > 0
                    ? "extractionPointsCompleted > 0"
                    : extractionPointsCompleted == 0
                    ? (primaryTargetAvailable ? "primary reference available" : "primary reference unavailable; crystal fallback pending")
                    : "RoundDirector unavailable; crystal fallback pending";
                Log.LogInfo(
                    $"[LastChance] ConsolationMoney evaluation: extractions={extractionPointsCompleted}/{extractionPointsTotal} " +
                    $"extractionPointsCompleted={extractionPointsCompleted} " +
                    $"gate={gateStatus} reason={gateReason} preserved={preservedCurrency}k " +
                    $"target={targetCurrency}k final={newCurrency}k topUp={Mathf.Max(0, newCurrency - preservedCurrency)}k " +
                    $"primaryApplied={primaryTargetAvailable && newCurrency > preservedCurrency}.");
            }

            LastChanceSurrenderNetwork.BroadcastExtractionReward();
            SemiFunc.StatSetRunCurrency(newCurrency);
            NormalizeDirectorsBeforeShopReturn();

            runMgr.previousRunLevel = runMgr.levelCurrent;

            TryLogShopReturnSnapshot(runMgr, newCurrency, "before-change-level");
            runMgr.ChangeLevel(false, false, RunManager.ChangeLevelType.Shop);
        }

        internal static void TryApplyPendingConsolationMoney(ShopManager shopManager)
        {
            if (!s_consolationMoneyPending ||
                shopManager == null ||
                !SemiFunc.IsMasterClientOrSingleplayer())
            {
                return;
            }

            try
            {
                var statsManager = StatsManager.instance;
                if (statsManager == null || statsManager.itemDictionary == null)
                {
                    CancelPendingConsolationMoney("StatsManager or item dictionary unavailable in destination shop.");
                    return;
                }

                Item? crystal = null;
                foreach (var item in statsManager.itemDictionary.Values)
                {
                    if (item != null && item.itemType == SemiFunc.itemType.power_crystal)
                    {
                        crystal = item;
                        break;
                    }
                }

                if (crystal == null || crystal.value == null)
                {
                    CancelPendingConsolationMoney("Power Crystal asset unavailable in destination shop.");
                    return;
                }

                // Mirror ItemAttributes.GetValue(): clamp raw value, round to k,
                // then apply the crystal-specific level multiplier.
                var rawMaxValue = crystal.value.valueMax * shopManager.itemValueMultiplier;
                rawMaxValue = Mathf.Max(rawMaxValue, 1000f);
                var baseCrystalValue = Mathf.Ceil(rawMaxValue / 1000f);
                var referenceCost = Mathf.CeilToInt(shopManager.CrystalValueGet(baseCrystalValue));
                var targetCurrency = CalculateConsolationTarget(referenceCost);
                var currentCurrency = SemiFunc.StatGetRunCurrency();
                var newCurrency = Mathf.Max(currentCurrency, targetCurrency);

                SemiFunc.StatSetRunCurrency(newCurrency);
                s_consolationMoneyPending = false;

                if (FeatureFlags.DebugLogging)
                {
                    Log.LogInfo(
                        $"[LastChance] Applied crystal fallback consolation money: crystalReference={referenceCost}k " +
                        $"percentage={Mathf.Clamp(FeatureFlags.ConsolationMoneyPercent, 0, 500)}% " +
                        $"target={targetCurrency}k topUp={Mathf.Max(0, newCurrency - currentCurrency)}k total={newCurrency}k.");
                }
            }
            catch (Exception ex)
            {
                s_consolationMoneyPending = false;
                Log.LogWarning($"[LastChance] Failed to apply crystal fallback consolation money; threshold application cancelled. {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static bool TryCalculatePrimaryConsolationTarget(out int target)
        {
            target = 0;
            var roundDirector = RoundDirector.instance;

            // Once an extraction has been completed, LastChance does not grant
            // consolation money for this level.
            if (roundDirector != null && roundDirector.extractionPointsCompleted > 0)
            {
                return true;
            }

            if (roundDirector == null ||
                roundDirector.extractionPoints <= 0 ||
                roundDirector.haulGoal <= 0)
            {
                return false;
            }

            // This is the same integer division used by ExtractionPoint when
            // assigning the minimum goal to each extraction point.
            var firstExtractionGoal = roundDirector.haulGoal / roundDirector.extractionPoints;
            if (firstExtractionGoal <= 0)
            {
                return false;
            }

            var firstExtractionGoalK = Mathf.CeilToInt(firstExtractionGoal / 1000f);
            target = CalculateConsolationTarget(firstExtractionGoalK);

            if (FeatureFlags.DebugLogging)
            {
                Log.LogDebug(
                    $"[LastChance] Calculated extraction consolation money: " +
                    $"extractionGoal={firstExtractionGoal} firstExtractionReference={firstExtractionGoalK}k " +
                    $"percentage={Mathf.Clamp(FeatureFlags.ConsolationMoneyPercent, 0, 500)}% target={target}k.");
            }

            return true;
        }

        private static int CalculateConsolationTarget(int referenceCostK)
        {
            var percentage = Mathf.Clamp(FeatureFlags.ConsolationMoneyPercent, 0, 500);
            return Mathf.CeilToInt(Mathf.Max(0, referenceCostK) * percentage / 100f);
        }

        private static void CancelPendingConsolationMoney(string reason)
        {
            s_consolationMoneyPending = false;
            Log.LogWarning($"[LastChance] Consolation money cancelled: {reason}");
        }

        private static void NormalizeDirectorsBeforeShopReturn()
        {
            try
            {
                if (RoundDirector.instance != null)
                {
                    RoundDirector.instance.allExtractionPointsCompleted = false;
                    RoundDirector.instance.extractionPointActive = false;
                    RoundDirector.instance.extractionPointCurrent = null;
                }

                if (EnemyDirector.instance != null)
                {
                    EnemyDirector.instance.extractionsDoneState = EnemyDirector.ExtractionsDoneState.StartRoom;
                }
            }
            catch (Exception ex)
            {
                LogRuntimeHotPathException("NormalizeDirectorsBeforeShopReturn", ex);
            }
        }

        private static void TryLogShopReturnSnapshot(RunManager runMgr, int targetCurrency, string phase)
        {
            if (!FeatureFlags.DebugLogging || !LogLimiter.ShouldLog("LastChance.ShopReturn", 15))
            {
                return;
            }

            try
            {
                var levelCurrent = runMgr.levelCurrent != null ? runMgr.levelCurrent.name : "<null>";
                var previousRunLevelName = runMgr.previousRunLevel != null ? runMgr.previousRunLevel.name : "<null>";

                var extractionDone = RoundDirector.instance != null &&
                                     RoundDirector.instance.allExtractionPointsCompleted;

                Log.LogDebug(
                    $"[LastChance] ShopReturn snapshot phase={phase} " +
                    $"levelCurrent={levelCurrent} previousRunLevel={previousRunLevelName} " +
                    $"runCurrency={SemiFunc.StatGetRunCurrency()} targetCurrency={targetCurrency} " +
                    $"allExtractionPointsCompleted={extractionDone}.");
            }
            catch (Exception ex)
            {
                LogRuntimeHotPathException("TryLogShopReturnSnapshot", ex);
            }
        }

        private static void CaptureBaseCurrency()
        {
            if (s_currencyCaptured)
            {
                return;
            }

            s_baseCurrency = SemiFunc.StatGetRunCurrency();
            s_currencyCaptured = true;
        }

        private static void ResetState()
        {
            if (!HasRuntimeStateToReset())
            {
                return;
            }

            ClearSurrenderState();
            ClearCachedDynamicTimerInputs();
            ClearActivationProfileState();

            if (!s_active)
            {
                ClearLastChanceHostRuntimeOverrides();
                return;
            }

            SetLastChanceActive(false);
            s_currencyCaptured = false;
            s_successHandled = false;
            s_timerRemaining = 0f;
            s_timerSyncedFromHost = false;
            s_hasNetworkUiState = false;
            s_networkUiRequiredOnTruck = 0;
            s_networkUiStatesByActor.Clear();
            s_nextDirectionPenaltyAllowedAtByActor.Clear();
            s_lastUiStateBroadcastAt = 0f;
            s_lastUiStateHash = 0;
            StopTimerSecondAudio();
            LastChanceTimerUI.Hide();
            ResetLastChanceRuntimeModules(allowVanillaAllPlayersDead: false, allowAutoDelete: false);
            BroadcastTimerStateIfHost(force: true);
        }

        internal static void SuppressForCurrentRoom(string reason)
        {
            s_suppressedForRoom = true;
            ForceStopRuntimeState();

            if (FeatureFlags.DebugLogging && !s_suppressedLogEmitted && LogLimiter.ShouldLog("LastChance.Suppress", 10))
            {
                s_suppressedLogEmitted = true;
                Log.LogDebug(reason);
            }
        }

        internal static void ClearRoomSuppression()
        {
            s_suppressedForRoom = false;
            s_suppressedLogEmitted = false;
            s_consolationMoneyPending = false;
        }

        private static void ForceStopRuntimeState()
        {
            if (!HasRuntimeStateToReset())
            {
                return;
            }

            ClearSurrenderState();
            ClearCachedDynamicTimerInputs();
            SetLastChanceActive(false);
            s_currencyCaptured = false;
            s_consolationMoneyPending = false;
            s_timerRemaining = 0f;
            s_timerSyncedFromHost = false;
            s_hasNetworkUiState = false;
            s_networkUiRequiredOnTruck = 0;
            s_networkUiStatesByActor.Clear();
            s_nextDirectionPenaltyAllowedAtByActor.Clear();
            s_lastUiStateBroadcastAt = 0f;
            s_lastUiStateHash = 0;
            StopTimerSecondAudio();
            LastChanceTimerUI.Hide();
            ResetLastChanceRuntimeModules(allowVanillaAllPlayersDead: true, allowAutoDelete: true);
            BroadcastTimerStateIfHost(force: true);
        }

        private static bool HasRuntimeStateToReset()
        {
            if (s_active)
            {
                return true;
            }

            if (s_currencyCaptured || s_timerRemaining > 0f)
            {
                return true;
            }

            if (LastChanceSurrenderedPlayers.Count > 0 || s_surrenderHoldTimer > 0f || s_localSurrendered)
            {
                return true;
            }

            if (s_directionActive || s_directionActiveUntil > 0f || s_directionCooldownUntil > 0f)
            {
                return true;
            }

            return s_lastChanceBatteryOverrideApplied;
        }

        private static bool IsValidRunContext()
        {
            if (!RunManager.instance)
            {
                return false;
            }

            if (SemiFunc.RunIsArena() || SemiFunc.RunIsLobby() || SemiFunc.RunIsShop() || SemiFunc.RunIsLobbyMenu() || SemiFunc.RunIsTutorial())
            {
                return false;
            }

            if (GameDirector.instance == null)
            {
                return false;
            }

            var runMgr = RunManager.instance;
            if (runMgr == null)
            {
                return false;
            }

            return IsRunStarted(runMgr) && GameDirector.instance.currentState == GameDirector.gameState.Main;
        }

        private static bool AllHeadsInTruck()
        {
            var director = GameDirector.instance;
            if (director == null || director.PlayerList == null || director.PlayerList.Count == 0)
            {
                return false;
            }

            int totalPlayers = 0;
            int headsInTruck = 0;

            foreach (var player in director.PlayerList)
            {
                if (player == null)
                {
                    continue;
                }

                totalPlayers++;

                if (IsPlayerSurrendered(player))
                {
                    continue;
                }

                if (!IsPlayerDisabled(player))
                {
                    var roomVolumeCheck = GetRoomVolumeCheck(player);
                    if (roomVolumeCheck == null || !IsRoomVolumeInTruck(roomVolumeCheck))
                    {
                        return false;
                    }
                    continue;
                }

                var deathHead = player.playerDeathHead;
                if (deathHead == null)
                {
                    return false;
                }

                var inTruck = GetDeathHeadInTruckStatus(deathHead);
                if (inTruck.HasValue && inTruck.Value)
                {
                    headsInTruck++;
                }
            }

            var maxPlayers = totalPlayers;
            var required = GetLastChanceNeededPlayers(maxPlayers);
            if (required <= 0)
            {
                return false;
            }

            return headsInTruck >= required;
        }

        private static int GetLastChanceNeededPlayers(int maxPlayers)
        {
            if (maxPlayers <= 0)
            {
                return 0;
            }

            var allowedMissing = Math.Max(0, Math.Min(FeatureFlags.LastChanceMissingPlayers, Math.Max(0, maxPlayers - 1)));
            var required = maxPlayers - allowedMissing;
            return Math.Max(1, required);
        }

        private static int GetLastChanceCanSurrender(int maxPlayers)
        {
            if (maxPlayers <= 0)
            {
                return 0;
            }

            var needed = GetLastChanceNeededPlayers(maxPlayers);
            return Math.Max(0, maxPlayers - needed);
        }

        private static int GetRunPlayerCount()
        {
            var director = GameDirector.instance;
            if (director == null || director.PlayerList == null)
            {
                return 0;
            }

            var count = 0;
            foreach (var player in director.PlayerList)
            {
                if (player != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool CheckSurrenderFailure(int maxPlayers)
        {
            if (maxPlayers <= 0)
            {
                return false;
            }

            var surrendered = LastChanceSurrenderedPlayers.Count;
            var allowedToSurrender = GetLastChanceCanSurrender(maxPlayers);
            if (surrendered <= allowedToSurrender)
            {
                return false;
            }

            FailLastChance($"[LastChance] Too many surrendered ({surrendered}) > allowed ({allowedToSurrender}); resuming vanilla all-dead flow.");
            return true;
        }

        private static void FailLastChance(string reason)
        {
            if (FeatureFlags.DebugLogging && LogLimiter.ShouldLog(LogKey, 30))
            {
                Log.LogDebug(reason);
            }

            LastChanceTimerUI.Hide();
            s_timerRemaining = 0f;
            s_consolationMoneyPending = false;
            SetLastChanceActive(false);
            s_timerSyncedFromHost = false;
            StopTimerSecondAudio();
            BroadcastTimerStateIfHost(force: true);

            ResetLastChanceRuntimeModules(allowVanillaAllPlayersDead: true, allowAutoDelete: true);
            ClearIndicatorsState();

            if (!SemiFunc.IsMasterClientOrSingleplayer())
            {
                return;
            }

            var runMgr = RunManager.instance;
            if (runMgr == null || IsRestarting(runMgr))
            {
                return;
            }

            runMgr.ChangeLevel(false, true, RunManager.ChangeLevelType.Normal);
        }

        internal static void ApplyNetworkTimerState(bool active, float secondsRemaining, double hostSentAt)
        {
            if (!SemiFunc.IsMultiplayer() || SemiFunc.IsMasterClient())
            {
                return;
            }

            SetLastChanceActive(active);
            s_timerSyncedFromHost = active;
            var previousTimerRemaining = s_timerRemaining;
            var authoritativeRemaining = ComputeAuthoritativeRemaining(secondsRemaining, hostSentAt);
            if (!active || !s_active)
            {
                s_timerRemaining = Mathf.Max(0f, authoritativeRemaining);
                if (!active)
                {
                    s_previousTimerWarningCheckSeconds = float.NaN;
                }
            }
            else
            {
                var drift = Mathf.Abs(s_timerRemaining - authoritativeRemaining);
                if (drift >= TimerDriftHardSnapSeconds)
                {
                    SetTimerRemainingAndRefreshUi(
                        authoritativeRemaining,
                        TimerChangeReason.NetworkSync,
                        broadcastIfHost: false,
                        forceBroadcastIfHost: false);
                }
                else
                {
                    SetTimerRemainingAndRefreshUi(
                        Mathf.Lerp(s_timerRemaining, authoritativeRemaining, TimerDriftLerpFactor),
                        TimerChangeReason.NetworkSync,
                        broadcastIfHost: false,
                        forceBroadcastIfHost: false);
                }
            }
            s_lastNetworkTimerBroadcastSecond = Mathf.CeilToInt(s_timerRemaining);

            if (active)
            {
                LastChanceTimerUI.Show(GetSurrenderHintPrompt());
                LastChanceTimerUI.UpdateText(FormatTimerText(s_timerRemaining));
                var syncedDelta = s_timerRemaining - previousTimerRemaining;
                LastChanceTimerUI.NotifyTimerDelta(syncedDelta, isNetworkSync: true);
                return;
            }

            LastChanceTimerUI.Hide();
            StopTimerSecondAudio();
        }

        private static void BroadcastTimerStateIfHost(bool force)
        {
            if (!SemiFunc.IsMultiplayer() || !SemiFunc.IsMasterClient())
            {
                return;
            }

            var wholeSeconds = Mathf.CeilToInt(s_timerRemaining);
            if (!force && wholeSeconds == s_lastNetworkTimerBroadcastSecond)
            {
                return;
            }

            s_lastNetworkTimerBroadcastSecond = wholeSeconds;
            LastChanceSurrenderNetwork.NotifyTimerState(s_active, s_timerRemaining, PhotonNetwork.Time);
        }

        private static void UpdateSurrenderInput(bool allDead)
        {
            if (!s_active || !allDead)
            {
                ResetLocalSurrenderAttempt();
                return;
            }

            if (s_localSurrendered)
            {
                LastChanceTimerUI.SetSurrenderHintText(SurrenderedHintText);
                return;
            }

            if (!SemiFunc.InputHold(SurrenderInputKey))
            {
                ResetLocalSurrenderAttempt();
                return;
            }

            if (s_surrenderHoldTimer <= 0f)
            {
                TryLogTruckDistancesForSurrender();
            }

            s_surrenderHoldTimer += Time.deltaTime;
            var remaining = SurrenderHoldDuration - s_surrenderHoldTimer;
            if (remaining > 0f)
            {
                var secs = Mathf.CeilToInt(remaining);
                LastChanceTimerUI.SetSurrenderHintText(string.Format(SurrenderCountdownFormat, secs));
                return;
            }

            HandleLocalSurrender();
        }

        private static void HandleLocalSurrender()
        {
            if (s_localSurrendered)
            {
                return;
            }

            var actorNumber = GetLocalActorNumber();
            if (!RegisterSurrenderedActor(actorNumber, true))
            {
                return;
            }

            s_localSurrendered = true;
            s_surrenderHoldTimer = SurrenderHoldDuration;
            LastChanceTimerUI.SetSurrenderHintText(LocalSurrenderedHintText);
        }

        private static void ResetLocalSurrenderAttempt()
        {
            if (s_surrenderHoldTimer > 0f && !s_localSurrendered)
            {
                s_surrenderHoldTimer = 0f;
                LastChanceTimerUI.ResetSurrenderHint();
            }
            s_surrenderDistanceLogged = false;
        }

        private static void TryLogTruckDistancesForSurrender()
        {
            if (!FeatureFlags.LastChangeMode || !FeatureFlags.DebugLogging || s_surrenderDistanceLogged)
            {
                return;
            }

            LastChanceTruckDistanceLogger.LogDistances();
            s_surrenderDistanceLogged = true;
        }

        private static string GetSurrenderHintPrompt()
        {
            return string.Format(SurrenderHintPromptFormat, SurrenderInputKey);
        }

        private static int GetLocalActorNumber()
        {
            var localAvatar = PlayerAvatar.instance;
            if (localAvatar?.photonView != null)
            {
                var owner = localAvatar.photonView.Owner;
                if (owner != null)
                {
                    return owner.ActorNumber;
                }
            }

            return PhotonNetwork.LocalPlayer?.ActorNumber ?? 0;
        }

        private static int GetPlayerActorNumber(PlayerAvatar player)
        {
            if (player?.photonView != null)
            {
                var owner = player.photonView.Owner;
                if (owner != null)
                {
                    return owner.ActorNumber;
                }
            }

            return 0;
        }

        private static bool IsPlayerSurrendered(PlayerAvatar player)
        {
            var actorNumber = GetPlayerActorNumber(player);
            if (actorNumber <= 0)
            {
                return false;
            }

            return LastChanceSurrenderedPlayers.Contains(actorNumber);
        }

        private static bool RegisterSurrenderedActor(int actorNumber, bool broadcast)
        {
            if (actorNumber <= 0)
            {
                return false;
            }

            var added = LastChanceSurrenderedPlayers.Add(actorNumber);
            if (added && broadcast)
            {
                LastChanceSurrenderNetwork.NotifyLocalSurrender(actorNumber);
            }

            return true;
        }

        internal static void RegisterRemoteSurrender(int actorNumber)
        {
            RegisterSurrenderedActor(actorNumber, false);
        }

        internal static void ApplyRemoteSurrenderSnapshot(object[] payload)
        {
            if (!SemiFunc.IsMultiplayer() || SemiFunc.IsMasterClient())
            {
                return;
            }

            LastChanceSurrenderedPlayers.Clear();
            if (payload == null || payload.Length == 0)
            {
                return;
            }

            for (var i = 0; i < payload.Length; i++)
            {
                if (payload[i] is int actor && actor > 0)
                {
                    LastChanceSurrenderedPlayers.Add(actor);
                }
            }
        }

        private static void UpdatePlayersStatusUi(int maxPlayers)
        {
            if (!s_active)
            {
                return;
            }

            if (SemiFunc.IsMultiplayer() && !SemiFunc.IsMasterClient())
            {
                if (!s_hasNetworkUiState)
                {
                    return;
                }

                var localSnapshots = PlayerStateExtractionHelper.GetPlayersStateSnapshot();
                var authoritativeSnapshots = BuildUiSnapshotsFromNetwork(localSnapshots);
                LastChanceTimerUI.UpdatePlayerStates(authoritativeSnapshots, Mathf.Max(1, s_networkUiRequiredOnTruck));
                return;
            }

            var snapshots = PlayerStateExtractionHelper.GetPlayersStateSnapshot();
            var required = GetLastChanceNeededPlayers(maxPlayers);
            LastChanceTimerUI.UpdatePlayerStates(snapshots, required);
            TryBroadcastUiStateIfHost(snapshots, required, force: false);
        }

        internal static bool IsPlayerSurrenderedForData(PlayerAvatar? player)
        {
            return player != null && IsPlayerSurrendered(player);
        }

        private static void ClearSurrenderState()
        {
            LastChanceSurrenderedPlayers.Clear();
            s_surrenderHoldTimer = 0f;
            s_localSurrendered = false;
            s_hasNetworkUiState = false;
            s_networkUiRequiredOnTruck = 0;
            s_networkUiStatesByActor.Clear();
            s_lastUiStateBroadcastAt = 0f;
            s_lastUiStateHash = 0;
            StopTimerSecondAudio();
            LastChanceTimerUI.ResetSurrenderHint();
            ClearIndicatorsState();
        }

        internal static void ApplyNetworkUiState(int requiredOnTruck, object[] statesPayload, int senderActorNumber)
        {
            if (!SemiFunc.IsMultiplayer())
            {
                return;
            }

            var masterActor = PhotonNetwork.MasterClient?.ActorNumber ?? -1;
            if (masterActor <= 0 || senderActorNumber != masterActor)
            {
                return;
            }

            s_networkUiStatesByActor.Clear();
            for (var i = 0; i < statesPayload.Length; i++)
            {
                if (statesPayload[i] is not object[] row || row.Length < 3)
                {
                    continue;
                }

                if (row[0] is not int actorNumber || actorNumber <= 0)
                {
                    continue;
                }

                if (row[1] is not bool isInTruck || row[2] is not bool isSurrendered)
                {
                    continue;
                }

                s_networkUiStatesByActor[actorNumber] = new NetworkUiPlayerState(isInTruck, isSurrendered);
            }

            s_networkUiRequiredOnTruck = Mathf.Max(1, requiredOnTruck);
            s_hasNetworkUiState = true;
        }

        private static List<PlayerStateExtractionHelper.PlayerStateSnapshot> BuildUiSnapshotsFromNetwork(
            List<PlayerStateExtractionHelper.PlayerStateSnapshot> localSnapshots)
        {
            if (localSnapshots == null || localSnapshots.Count == 0)
            {
                return localSnapshots ?? new List<PlayerStateExtractionHelper.PlayerStateSnapshot>(0);
            }

            var merged = new List<PlayerStateExtractionHelper.PlayerStateSnapshot>(localSnapshots.Count);
            for (var i = 0; i < localSnapshots.Count; i++)
            {
                var snapshot = localSnapshots[i];
                if (snapshot.ActorNumber > 0 && s_networkUiStatesByActor.TryGetValue(snapshot.ActorNumber, out var networkState))
                {
                    merged.Add(new PlayerStateExtractionHelper.PlayerStateSnapshot(
                        snapshot.ActorNumber,
                        snapshot.SteamIdShort,
                        snapshot.Name,
                        snapshot.Color,
                        snapshot.IsAlive,
                        snapshot.IsDead,
                        networkState.IsInTruck,
                        networkState.IsSurrendered,
                        snapshot.SourceOrder));
                }
                else
                {
                    merged.Add(snapshot);
                }
            }

            return merged;
        }

        private static void TryBroadcastUiStateIfHost(
            List<PlayerStateExtractionHelper.PlayerStateSnapshot> snapshots,
            int requiredOnTruck,
            bool force)
        {
            if (!SemiFunc.IsMultiplayer() || !SemiFunc.IsMasterClient())
            {
                return;
            }

            if (!force && Time.time < s_lastUiStateBroadcastAt + UiStateBroadcastIntervalSeconds)
            {
                return;
            }

            var hash = ComputeUiStateHash(snapshots, requiredOnTruck);
            if (!force && hash == s_lastUiStateHash && s_lastUiStateBroadcastAt > 0f)
            {
                return;
            }

            var payload = BuildUiStatePayload(snapshots);
            LastChanceSurrenderNetwork.NotifyUiState(Mathf.Max(1, requiredOnTruck), payload);
            s_lastUiStateHash = hash;
            s_lastUiStateBroadcastAt = Time.time;
        }

        internal static void ForceBroadcastRuntimeSnapshotForSync()
        {
            if (!SemiFunc.IsMultiplayer() || !SemiFunc.IsMasterClient())
            {
                return;
            }

            BroadcastTimerStateIfHost(force: true);

            var maxPlayers = GetRunPlayerCount();
            if (s_active && maxPlayers > 0)
            {
                var snapshots = PlayerStateExtractionHelper.GetPlayersStateSnapshot();
                var required = GetLastChanceNeededPlayers(maxPlayers);
                TryBroadcastUiStateIfHost(snapshots, required, force: true);
            }
            else
            {
                LastChanceSurrenderNetwork.NotifyUiState(1, System.Array.Empty<object>());
                s_lastUiStateBroadcastAt = Time.time;
                s_lastUiStateHash = 0;
            }

            LastChanceSurrenderNetwork.NotifySurrenderSnapshot(BuildSurrenderedActorsPayload());
        }

        private static object[] BuildUiStatePayload(List<PlayerStateExtractionHelper.PlayerStateSnapshot> snapshots)
        {
            if (snapshots == null || snapshots.Count == 0)
            {
                return Array.Empty<object>();
            }

            var rows = new List<object>(snapshots.Count);
            for (var i = 0; i < snapshots.Count; i++)
            {
                var s = snapshots[i];
                if (s.ActorNumber <= 0)
                {
                    continue;
                }

                rows.Add(new object[] { s.ActorNumber, s.IsInTruck, s.IsSurrendered });
            }

            return rows.ToArray();
        }

        private static int ComputeUiStateHash(List<PlayerStateExtractionHelper.PlayerStateSnapshot> snapshots, int requiredOnTruck)
        {
            unchecked
            {
                var hash = requiredOnTruck * 397;
                if (snapshots == null)
                {
                    return hash;
                }

                for (var i = 0; i < snapshots.Count; i++)
                {
                    var s = snapshots[i];
                    hash = (hash * 31) + s.ActorNumber;
                    hash = (hash * 31) + (s.IsInTruck ? 1 : 0);
                    hash = (hash * 31) + (s.IsSurrendered ? 1 : 0);
                }

                return hash;
            }
        }

        private static void TryPlayLastChanceTimerSecondTick()
        {
            if (!s_active)
                return;

            var wholeSeconds = Mathf.CeilToInt(s_timerRemaining);
            if (wholeSeconds > 10 || wholeSeconds <= 0)
                return;
            if (wholeSeconds == s_lastTimerSecondAudioPlayed)
                return;

            if (!TryEnsureTimerSecondAudioReady())
                return;
            if (s_timerSecondAudioSource == null || s_timerSecondAudioClip == null)
                return;

            s_timerSecondAudioSource.PlayOneShot(s_timerSecondAudioClip);
            s_lastTimerSecondAudioPlayed = wholeSeconds;
        }

        private static void TryPlayLastChanceTimerWarnings(float previousSeconds, float currentSeconds)
        {
            if (!s_active)
            {
                return;
            }

            var crossedBelow60 = previousSeconds > 60f && currentSeconds <= 60f;
            var crossedBelow30 = previousSeconds > 30f && currentSeconds <= 30f;
            if (!crossedBelow60 && !crossedBelow30)
            {
                return;
            }

            if (!TryEnsureTimerWarningAudioReady())
            {
                return;
            }

            if (s_timerWarningAudioSource == null || s_timerWarningAudioClip == null)
            {
                return;
            }

            // 1:00 at normal speed, 0:30 at +50% speed.
            s_timerWarningAudioSource.pitch = crossedBelow30 ? 1.5f : 1f;
            s_timerWarningAudioSource.PlayOneShot(s_timerWarningAudioClip);
            s_lastTimerWarningAudioPlayed = crossedBelow30 ? 30 : 60;
        }

        private static bool TryEnsureTimerSecondAudioReady()
        {
            if (s_timerSecondAudioClip == null)
            {
                var now = Time.unscaledTime;
                if (!s_timerSecondAudioLoadAttempted || now >= s_nextTimerSecondAudioRetryAt)
                {
                    s_timerSecondAudioLoadAttempted = true;
                    s_nextTimerSecondAudioRetryAt = now + AssetAudioRetryIntervalSeconds;
                if (!AudioAssetLoader.TryLoadAudioClip(
                        TimerSecondAudioFileName,
                        AudioAssetLoader.GetDefaultAssetsDirectory(),
                        out var clip,
                        out var resolvedPath) || clip == null)
                {
                    if (FeatureFlags.DebugLogging && LogLimiter.ShouldLog("LastChance.TimerSecond.LoadFail", 30))
                    {
                        var baseDir = AudioAssetLoader.GetDefaultAssetsDirectory();
                        Log.LogWarning($"[LastChance] Failed to load timer tick audio. file={TimerSecondAudioFileName} baseDir={baseDir}");
                    }

                    return false;
                }

                s_timerSecondAudioClip = clip;
                if (FeatureFlags.DebugLogging && LogLimiter.ShouldLog("LastChance.TimerSecond.Loaded", 30))
                {
                    Log.LogDebug($"[LastChance] Loaded timer tick audio from: {resolvedPath}");
                }
                }
                else
                {
                    return false;
                }
            }

            if (s_timerSecondAudioClip == null)
                return false;

            if (s_timerSecondAudioSource == null)
            {
                var go = new GameObject("DHHFix.LastChanceTimerSecondAudio");
                UnityEngine.Object.DontDestroyOnLoad(go);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = false;
                src.spatialBlend = 0f;
                src.volume = 1f;
                s_timerSecondAudioSource = src;
            }

            return s_timerSecondAudioSource != null;
        }

        private static bool TryEnsureTimerWarningAudioReady()
        {
            if (s_timerWarningAudioClip == null)
            {
                var now = Time.unscaledTime;
                if (!s_timerWarningAudioLoadAttempted || now >= s_nextTimerWarningAudioRetryAt)
                {
                    s_timerWarningAudioLoadAttempted = true;
                    s_nextTimerWarningAudioRetryAt = now + AssetAudioRetryIntervalSeconds;
                if (!TryLoadTimerWarningClip(out var clip, out var resolvedPath) || clip == null)
                {
                    if (FeatureFlags.DebugLogging && LogLimiter.ShouldLog("LastChance.TimerWarning.LoadFail", 30))
                    {
                        var baseDir = AudioAssetLoader.GetDefaultAssetsDirectory();
                        Log.LogWarning(
                            $"[LastChance] Failed to load timer warning audio. files={TimerWarningAudioPrimaryFileName} baseDir={baseDir}");
                    }

                    return false;
                }

                s_timerWarningAudioClip = clip;
                if (FeatureFlags.DebugLogging && LogLimiter.ShouldLog("LastChance.TimerWarning.Loaded", 30))
                {
                    Log.LogDebug($"[LastChance] Loaded timer warning audio from: {resolvedPath}");
                }
                }
                else
                {
                    return false;
                }
            }

            if (s_timerWarningAudioClip == null)
            {
                return false;
            }

            if (s_timerWarningAudioSource == null)
            {
                var go = new GameObject("DHHFix.LastChanceTimerWarningAudio");
                UnityEngine.Object.DontDestroyOnLoad(go);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = false;
                src.spatialBlend = 0f;
                src.volume = 1f;
                src.pitch = 1f;
                s_timerWarningAudioSource = src;
            }

            return s_timerWarningAudioSource != null;
        }

        private static bool TryLoadTimerWarningClip(out AudioClip? clip, out string resolvedPath)
        {
            clip = null;
            resolvedPath = string.Empty;

            return AudioAssetLoader.TryLoadAudioClip(
                TimerWarningAudioPrimaryFileName,
                AudioAssetLoader.GetDefaultAssetsDirectory(),
                out clip,
                out resolvedPath);
        }

        private static void StopTimerSecondAudio()
        {
            s_lastTimerSecondAudioPlayed = -1;
            if (s_timerSecondAudioSource != null)
            {
                s_timerSecondAudioSource.Stop();
            }

            s_lastTimerWarningAudioPlayed = -1;
            s_previousTimerWarningCheckSeconds = float.NaN;
            if (s_timerWarningAudioSource != null)
            {
                s_timerWarningAudioSource.Stop();
                s_timerWarningAudioSource.pitch = 1f;
            }
        }

        private static void PrewarmLastChanceAssets()
        {
            if (s_assetsPrewarmedForSession)
            {
                return;
            }

            LastChanceTimerUI.PrewarmAssets();
            _ = TryEnsureTimerSecondAudioReady();
            _ = TryEnsureTimerWarningAudioReady();
            s_assetsPrewarmedForSession = true;

            if (FeatureFlags.DebugLogging && LogLimiter.ShouldLog("LastChance.Prewarm", 30))
            {
                Log.LogDebug("[LastChance] Prewarmed UI sprites and timer audio assets.");
            }
        }

        internal static void PrewarmGlobalAssetsAtBoot()
        {
            LastChanceTimerUI.PrewarmAssets();
            _ = TryEnsureTimerSecondAudioReady();
            _ = TryEnsureTimerWarningAudioReady();
            s_assetsPrewarmedForSession = true;
        }

        private static void UpdateIndicators(int maxPlayers, bool allDead)
        {
            var mode = GetIndicatorMode();
            if (!s_active || !allDead)
            {
                if (FeatureFlags.DebugLogging && LogLimiter.ShouldLog("LastChance.Indicator.Blocked", 5))
                {
                    var rawMode = FeatureFlags.LastChanceIndicators ?? string.Empty;
                    Log.LogDebug($"[LastChance] Indicator blocked: active={s_active} allDead={allDead} modeRaw='{rawMode}' modeParsed={mode}");
                }
                ClearActiveIndicatorVisuals();
                AbilityModule.RefreshDirectionSlotVisuals();
                return;
            }

            if (mode == LastChanceIndicatorMode.None)
            {
                if (!s_indicatorNoneLoggedThisCycle && FeatureFlags.DebugLogging)
                {
                    var rawMode = FeatureFlags.LastChanceIndicators ?? string.Empty;
                    Log.LogDebug($"[LastChance] Indicator disabled for this cycle: modeRaw='{rawMode}' modeParsed={mode}");
                    s_indicatorNoneLoggedThisCycle = true;
                }
                ClearActiveIndicatorVisuals();
                AbilityModule.RefreshDirectionSlotVisuals();
                return;
            }

            var directionEnabled = mode == LastChanceIndicatorMode.Direction;
            UpdateSingleIndicator(IndicatorKind.Direction, directionEnabled);
            AbilityModule.RefreshDirectionSlotVisuals();
        }

        private static LastChanceIndicatorMode GetIndicatorMode()
        {
            var raw = (FeatureFlags.LastChanceIndicators ?? string.Empty).Trim();
            if (raw.Equals("Direction", StringComparison.OrdinalIgnoreCase))
            {
                return LastChanceIndicatorMode.Direction;
            }

            if (raw.Equals("Indicator", StringComparison.OrdinalIgnoreCase))
            {
                return LastChanceIndicatorMode.Direction;
            }

            return LastChanceIndicatorMode.None;
        }

        private static void UpdateSingleIndicator(IndicatorKind kind, bool enabled)
        {
            if (!enabled)
            {
                ResetIndicatorHold();
                DeactivateIndicator(kind);
                return;
            }

            if (IsIndicatorActive(kind))
            {
                if (Time.time >= GetIndicatorActiveUntil(kind))
                {
                    DeactivateIndicator(kind);
                }
                else
                {
                    TickActiveIndicator(kind);
                }
            }

            if (IsIndicatorActive(kind) || Time.time < GetIndicatorCooldownUntil(kind))
            {
                ResetIndicatorHold();
                return;
            }

            if (kind == IndicatorKind.Direction && !IsDirectionIndicatorEnergySufficientPreview())
            {
                ResetIndicatorHold();
                return;
            }

            // Input handling is driven by equipped ability callbacks (OnAbilityDown/Hold/Up/Cancel).
        }

        private static void ResetIndicatorHold()
        {
            s_directionHoldTimer = 0f;
            AbilityModule.SetDirectionSlotActivationProgress(0f);
        }

        internal static void OnDirectionAbilityInputDown()
        {
            if (!IsDirectionIndicatorUiVisible)
            {
                ResetIndicatorHold();
            }
        }

        internal static void OnDirectionAbilityInputHold()
        {
            if (!IsDirectionIndicatorUiVisible)
            {
                ResetIndicatorHold();
                return;
            }

            if (IsIndicatorActive(IndicatorKind.Direction) || Time.time < GetIndicatorCooldownUntil(IndicatorKind.Direction))
            {
                ResetIndicatorHold();
                return;
            }

            if (!IsDirectionIndicatorEnergySufficientPreview())
            {
                ResetIndicatorHold();
                return;
            }

            var holdSeconds = DirectionIndicatorHoldSeconds;
            s_directionHoldTimer = Mathf.Min(holdSeconds, s_directionHoldTimer + Time.deltaTime);
            AbilityModule.SetDirectionSlotActivationProgress(Mathf.Clamp01(s_directionHoldTimer / holdSeconds));
            if (s_directionHoldTimer < holdSeconds)
            {
                return;
            }

            ResetIndicatorHold();
            var maxPlayers = GetRunPlayerCount();
            if (maxPlayers <= 0)
            {
                return;
            }

            TriggerIndicator(IndicatorKind.Direction, maxPlayers);
        }

        internal static void OnDirectionAbilityInputUp()
        {
            ResetIndicatorHold();
        }

        internal static void OnDirectionAbilityInputCancel()
        {
            ResetIndicatorHold();
        }

        private static void TriggerIndicator(IndicatorKind kind, int maxPlayers)
        {
            var duration = Mathf.Clamp(FeatureFlags.LastChanceIndicatorDirectionDurationSeconds, 0.5f, 20f);
            var cooldown = Mathf.Clamp(FeatureFlags.LastChanceIndicatorDirectionCooldownSeconds, 1f, 60f);
            var activeUntil = Time.time + duration;
            SetIndicatorActive(kind, true);
            SetIndicatorActiveUntil(kind, activeUntil);
            SetIndicatorCooldownUntil(kind, activeUntil + cooldown);

            if (FeatureFlags.DebugLogging && LogLimiter.ShouldLog($"{IndicatorCooldownLogKey}.Start.{kind}", 2))
            {
                Log.LogDebug($"[LastChance] Indicator cooldown started: kind={kind} duration={duration:F1}s cooldown={cooldown:F1}s");
            }

            ApplyIndicatorPenalty(kind, maxPlayers);
            TickActiveIndicator(kind);
            if (kind == IndicatorKind.Direction)
            {
                var uiLockSeconds = Mathf.Max(0f, GetIndicatorCooldownUntil(kind) - Time.time);
                AbilityModule.TriggerDirectionSlotCooldown(uiLockSeconds);
            }

            if (FeatureFlags.DebugLogging && LogLimiter.ShouldLog(IndicatorLogKey, 3))
            {
                var remainingCooldown = Mathf.Max(0f, GetIndicatorCooldownUntil(kind) - Time.time);
                Log.LogDebug($"[LastChance] Indicator triggered: mode={kind} active={duration:F1}s cooldown={remainingCooldown:F1}s timer={s_timerRemaining:F1}s");
            }
        }

        private static void ApplyIndicatorPenalty(IndicatorKind kind, int maxPlayers)
        {
            if (SemiFunc.IsMultiplayer() && !SemiFunc.IsMasterClient())
            {
                LastChanceSurrenderNetwork.NotifyDirectionPenaltyRequest();
                return;
            }

            ApplyIndicatorPenaltyHost(maxPlayers);
        }

        internal static void HandleDirectionPenaltyRequest(int senderActorNumber)
        {
            if (!SemiFunc.IsMultiplayer() || !SemiFunc.IsMasterClient())
            {
                return;
            }

            if (!s_active || !AllPlayersDeadGuard.AllPlayersDisabled())
            {
                return;
            }

            if (senderActorNumber <= 0)
            {
                return;
            }

            var now = Time.unscaledTime;
            if (s_nextDirectionPenaltyAllowedAtByActor.TryGetValue(senderActorNumber, out var nextAllowedAt) &&
                now < nextAllowedAt)
            {
                return;
            }

            s_nextDirectionPenaltyAllowedAtByActor[senderActorNumber] = now + DirectionPenaltyRequestCooldownSeconds;
            var maxPlayers = GetRunPlayerCount();
            if (maxPlayers <= 0)
            {
                return;
            }

            ApplyIndicatorPenaltyHost(maxPlayers);
        }

        internal static void TryApplyMonsterDeathTimerBonusHost()
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer())
            {
                return;
            }

            // NOOP guards: feature disabled or runtime not in active LastChance must not alter timer.
            if (!FeatureFlags.LastChangeMode || !s_active || !AllPlayersDeadGuard.AllPlayersDisabled())
            {
                return;
            }

            var bonusSeconds = Mathf.Clamp(FeatureFlags.LastChanceTimerBonusPerMonsterDeathSeconds, 0, 10);
            if (bonusSeconds <= 0)
            {
                return;
            }

            ApplyTimerDelta(bonusSeconds, TimerChangeReason.MonsterKillBonus, broadcastIfHost: true, forceBroadcastIfHost: true);
        }

        private static void ApplyIndicatorPenaltyHost(int maxPlayers)
        {
            var penalty = GetOrComputeDirectionPenaltySeconds();
            if (penalty <= 0f)
            {
                return;
            }

            if (!HasEnoughTimerForDirectionPenalty(penalty))
            {
                return;
            }

            ApplyTimerDelta(-penalty, TimerChangeReason.DirectionPenalty, broadcastIfHost: true, forceBroadcastIfHost: true);
        }

        private static bool HasEnoughTimerForDirectionPenalty(float penaltySeconds)
        {
            var safePenalty = Mathf.Max(0f, penaltySeconds);
            if (safePenalty <= 0f)
            {
                return false;
            }

            if (s_timerRemaining <= DirectionIndicatorMinimumTimerSeconds)
            {
                return false;
            }

            return s_timerRemaining >= safePenalty;
        }

        private static void ApplyTimerDelta(
            float deltaSeconds,
            TimerChangeReason reason,
            bool broadcastIfHost,
            bool forceBroadcastIfHost)
        {
            if (Mathf.Abs(deltaSeconds) <= Mathf.Epsilon)
            {
                return;
            }

            SetTimerRemainingAndRefreshUi(
                s_timerRemaining + deltaSeconds,
                reason,
                broadcastIfHost,
                forceBroadcastIfHost);
            if (reason == TimerChangeReason.MonsterKillBonus || reason == TimerChangeReason.DirectionPenalty)
            {
                LastChanceTimerUI.NotifyTimerDelta(deltaSeconds);
            }
        }

        private static void SetTimerRemainingAndRefreshUi(
            float nextSeconds,
            TimerChangeReason reason,
            bool broadcastIfHost,
            bool forceBroadcastIfHost)
        {
            // Keep a single authoritative write path for timer value and UI refresh.
            var previousSeconds = s_timerRemaining;
            s_timerRemaining = Mathf.Max(0f, nextSeconds);
            if (broadcastIfHost)
            {
                BroadcastTimerStateIfHost(forceBroadcastIfHost);
            }

            LastChanceTimerUI.UpdateText(FormatTimerText(s_timerRemaining));
            var warningPrevious = float.IsNaN(s_previousTimerWarningCheckSeconds)
                ? previousSeconds
                : s_previousTimerWarningCheckSeconds;
            TryPlayLastChanceTimerWarnings(warningPrevious, s_timerRemaining);
            s_previousTimerWarningCheckSeconds = s_timerRemaining;
            _ = reason;
        }

        private static float CalculateIndicatorPenaltySeconds()
        {
            var maxPenalty = Mathf.Max(0f, FeatureFlags.LastChanceIndicatorDirectionPenaltyMaxSeconds);
            var minPenalty = Mathf.Max(0f, FeatureFlags.LastChanceIndicatorDirectionPenaltyMinSeconds);
            if (minPenalty > maxPenalty)
            {
                (minPenalty, maxPenalty) = (maxPenalty, minPenalty);
            }

            // Static timer mode: always use the low-difficulty / maximum configured penalty.
            if (!FeatureFlags.LastChanceDynamicTimerEnabled)
            {
                return Mathf.Round(maxPenalty);
            }

            var difficulty = GetRepoDifficultySnapshot();
            return Mathf.Round(Mathf.Lerp(maxPenalty, minPenalty, difficulty.Progress));
        }

        private static float GetOrComputeDirectionPenaltySeconds()
        {
            if (s_hasCachedDirectionPenaltySeconds)
            {
                return s_cachedDirectionPenaltySeconds;
            }

            CacheDirectionPenaltySeconds();
            return s_cachedDirectionPenaltySeconds;
        }

        private static void CacheDirectionPenaltySeconds()
        {
            s_cachedDirectionPenaltySeconds = CalculateIndicatorPenaltySeconds();
            s_hasCachedDirectionPenaltySeconds = true;
        }

        private static void ClearDirectionPenaltyCache()
        {
            s_cachedDirectionPenaltySeconds = 0f;
            s_hasCachedDirectionPenaltySeconds = false;
        }

        private static void TickActiveIndicator(IndicatorKind kind)
        {
            EnsureDirectionLine();
            AnimateDirectionLineMaterial();
            UpdateDirectionPath(force: Time.time >= s_indicatorNextPathRefreshAt);
        }

        private static void DeactivateIndicator(IndicatorKind kind)
        {
            SetIndicatorActive(kind, false);
            if (s_indicatorDirectionLine != null)
            {
                s_indicatorDirectionLine.positionCount = 0;
                s_indicatorDirectionLine.enabled = false;
            }
        }

        private static bool IsIndicatorActive(IndicatorKind kind)
        {
            return s_directionActive;
        }

        private static void SetIndicatorActive(IndicatorKind kind, bool value)
        {
            s_directionActive = value;
        }

        private static float GetIndicatorActiveUntil(IndicatorKind kind)
        {
            return s_directionActiveUntil;
        }

        private static void SetIndicatorActiveUntil(IndicatorKind kind, float value)
        {
            s_directionActiveUntil = value;
        }

        private static float GetIndicatorCooldownUntil(IndicatorKind kind)
        {
            return s_directionCooldownUntil;
        }

        private static void SetIndicatorCooldownUntil(IndicatorKind kind, float value)
        {
            s_directionCooldownUntil = value;
        }

        private static void EnsureDirectionLine()
        {
            if (s_indicatorDirectionLine != null)
            {
                s_indicatorDirectionLine.enabled = true;
                return;
            }

            s_indicatorDirectionObject = new GameObject("DHHFix.LastChanceDirectionIndicator");
            UnityEngine.Object.DontDestroyOnLoad(s_indicatorDirectionObject);
            s_indicatorDirectionLine = s_indicatorDirectionObject.AddComponent<LineRenderer>();
            s_indicatorDirectionLine.useWorldSpace = true;
            s_indicatorDirectionLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            s_indicatorDirectionLine.receiveShadows = false;
            s_indicatorDirectionLine.textureMode = LineTextureMode.Tile;
            s_indicatorDirectionLine.alignment = LineAlignment.View;
            s_indicatorDirectionLine.widthCurve = AnimationCurve.EaseInOut(0f, 0.09f, 1f, 0.05f);
            s_indicatorDirectionLine.positionCount = 0;
            s_indicatorDirectionLine.enabled = true;

            if (!TryApplyPhysGrabBeamMaterial(s_indicatorDirectionLine))
            {
                s_indicatorDirectionLine.material = new Material(Shader.Find("Sprites/Default"));
                s_indicatorDirectionMaterial = s_indicatorDirectionLine.material;
            }
            ConfigureDirectionLineFromPhysGrabBeam();
            s_indicatorNextPathRefreshAt = 0f;
        }

        private static bool TryApplyPhysGrabBeamMaterial(LineRenderer lineRenderer)
        {
            if (!TryGetPhysGrabBeamSource(out var source))
            {
                return false;
            }
            if (source == null)
            {
                return false;
            }

            lineRenderer.material = source.material;
            lineRenderer.textureMode = source.textureMode;
            s_indicatorDirectionMaterial = lineRenderer.material;
            return true;
        }

        private static void ConfigureDirectionLineFromPhysGrabBeam()
        {
            if (s_indicatorDirectionLine == null || !TryGetPhysGrabBeamSource(out var source))
            {
                return;
            }
            if (source == null)
            {
                return;
            }

            s_indicatorDirectionLine.alignment = source.alignment;
            s_indicatorDirectionLine.textureMode = source.textureMode;
            s_indicatorDirectionLine.widthMultiplier = source.widthMultiplier;
            s_indicatorDirectionLine.widthCurve = source.widthCurve;
            s_indicatorDirectionLine.colorGradient = source.colorGradient;
            s_indicatorDirectionLine.startColor = source.startColor;
            s_indicatorDirectionLine.endColor = source.endColor;
            s_indicatorDirectionLine.numCornerVertices = source.numCornerVertices;
            s_indicatorDirectionLine.numCapVertices = source.numCapVertices;
            s_indicatorDirectionLine.generateLightingData = source.generateLightingData;
            s_indicatorDirectionLine.material = source.material;
            s_indicatorDirectionMaterial = s_indicatorDirectionLine.material;
        }

        private static void AnimateDirectionLineMaterial()
        {
            if (s_indicatorDirectionMaterial == null)
            {
                return;
            }

            s_indicatorDirectionMaterial.mainTextureScale = Vector2.one;
            s_indicatorDirectionMaterial.mainTextureOffset = Time.time * DirectionLineScrollSpeed;
        }

        private static bool TryGetPhysGrabBeamSource(out LineRenderer? source)
        {
            source = null;
            var avatar = PlayerAvatar.instance;
            if (avatar == null)
            {
                return false;
            }

            var physGrabber = avatar.GetComponent<PhysGrabber>();
            if (physGrabber == null || physGrabber.physGrabBeam == null)
            {
                return false;
            }

            source = physGrabber.physGrabBeam.GetComponent<LineRenderer>();
            if (source == null || source.material == null)
            {
                return false;
            }
            return true;
        }

        private static void UpdateDirectionPath(bool force)
        {
            if (!force)
            {
                return;
            }

            s_indicatorNextPathRefreshAt = Time.time + DirectionPathRefreshSeconds;
            if (s_indicatorDirectionLine == null)
            {
                return;
            }

            if (!TryBuildPathToTruck(out var pathPoints, out var navFrom, out var navTo))
            {
                s_indicatorDirectionLine.positionCount = 0;
                return;
            }

            if (s_hasLastDirectionPathSample &&
                (navFrom - s_lastDirectionPathFrom).sqrMagnitude <= DirectionPathMovementThresholdSqr &&
                (navTo - s_lastDirectionPathTo).sqrMagnitude <= DirectionPathMovementThresholdSqr)
            {
                return;
            }

            s_hasLastDirectionPathSample = true;
            s_lastDirectionPathFrom = navFrom;
            s_lastDirectionPathTo = navTo;

            s_indicatorDirectionLine.positionCount = pathPoints.Count;
            for (var i = 0; i < pathPoints.Count; i++)
            {
                s_indicatorDirectionLine.SetPosition(i, pathPoints[pathPoints.Count - 1 - i]);
            }
        }

        private static bool TryBuildPathToTruck(out List<Vector3> points, out Vector3 navFrom, out Vector3 navTo)
        {
            points = new List<Vector3>(2);
            navFrom = Vector3.zero;
            navTo = Vector3.zero;
            if (!PlayerTruckDistanceHelper.TryGetLocalPlayerTruckRouteAssessment(
                    includePathCorners: true,
                    assessment: out var routeAssessment))
            {
                return false;
            }

            var localPosBase = routeAssessment.PlayerWorldPosition;
            var truckPosBase = routeAssessment.TruckWorldPosition;
            var localPos = localPosBase + Vector3.up * DirectionLineHeightOffset;
            var truckPos = truckPosBase + Vector3.up * DirectionLineHeightOffset;

            navFrom = routeAssessment.NavMeshFrom;
            navTo = routeAssessment.NavMeshTo;

            var corners = routeAssessment.PathCorners;
            if (!routeAssessment.HasValidPath || corners.Length == 0)
            {
                points.Add(localPos);
                points.Add(truckPos);
                return true;
            }

            points = new List<Vector3>(corners.Length + 1) { localPos };
            for (var i = 0; i < corners.Length; i++)
            {
                points.Add(corners[i] + Vector3.up * DirectionLineHeightOffset);
            }

            return points.Count >= 2;
        }

        private static void LogRuntimeHotPathException(string context, Exception ex)
        {
            if (!FeatureFlags.DebugLogging)
            {
                return;
            }

            var key = "LastChance.Runtime.TimerController." + context;
            if (!LogLimiter.ShouldLog(key, 600))
            {
                return;
            }

            Log.LogWarning($"[LastChance] Runtime hot-path failed in {context}: {ex.GetType().Name}: {ex.Message}");
        }

        private static void ClearActiveIndicatorVisuals()
        {
            DeactivateIndicator(IndicatorKind.Direction);
            AbilityModule.RefreshDirectionSlotVisuals();
        }

        private static void ClearIndicatorsState()
        {
            s_indicatorNoneLoggedThisCycle = false;
            s_directionHoldTimer = 0f;
            s_directionCooldownUntil = 0f;
            s_directionActiveUntil = 0f;
            s_directionActive = false;
            s_indicatorNextPathRefreshAt = 0f;
            s_hasLastDirectionPathSample = false;
            AbilityModule.SetDirectionSlotActivationProgress(0f);
            ClearActiveIndicatorVisuals();
        }


        private static void DebugTruckState(bool allDead)
        {
            if (!FeatureFlags.DebugLogging || !FeatureFlags.LastChangeMode)
            {
                return;
            }

            if (!allDead)
            {
                return;
            }

            var director = GameDirector.instance;
            if (director == null || director.PlayerList == null || director.PlayerList.Count == 0)
            {
                return;
            }

            var message = "[LastChance] TruckState:";
            var extractionDone = RoundDirector.instance != null &&
                RoundDirector.instance.allExtractionPointsCompleted;
            message += $" extractionDone={extractionDone}";
            foreach (var player in director.PlayerList)
            {
                if (player == null)
                {
                    message += " [null player]";
                    continue;
                }

                var name = GetPlayerName(player);
                var disabled = IsPlayerDisabled(player);
                if (!disabled)
                {
                    var rvc = GetRoomVolumeCheck(player);
                    var inTruck = rvc != null && IsRoomVolumeInTruck(rvc);
                    message += $" {name}(alive,inTruck={inTruck})";
                    continue;
                }

                var deathHead = player.playerDeathHead;
                var dhRoom = deathHead != null ? GetDeathHeadRoomVolumeCheck(deathHead) : null;
                var dhRoomInTruck = dhRoom != null && IsRoomVolumeInTruck(dhRoom);
                var dhInTruck = deathHead != null && deathHead.inTruck;
                message += $" {name}(deadHead,roomInTruck={dhRoomInTruck},inTruck={dhInTruck})";
            }

            if (string.Equals(s_lastTruckStateDebugMessage, message, StringComparison.Ordinal))
            {
                return;
            }

            s_lastTruckStateDebugMessage = message;
            Log.LogDebug(message);
        }

        private static string GetPlayerName(PlayerAvatar player)
        {
            if (!string.IsNullOrWhiteSpace(player.playerName))
            {
                return player.playerName;
            }

            return player.GetType().Name;
        }

        private static bool IsRunStarted(RunManager runMgr)
        {
            return runMgr.runStarted;
        }

        private static bool IsRestarting(RunManager runMgr)
        {
            return runMgr.restarting;
        }

        private static bool IsPlayerDisabled(PlayerAvatar player)
        {
            return player.isDisabled;
        }

        private static RoomVolumeCheck? GetRoomVolumeCheck(PlayerAvatar player)
        {
            return player.RoomVolumeCheck;
        }

        private static bool IsRoomVolumeInTruck(RoomVolumeCheck roomVolumeCheck)
        {
            return roomVolumeCheck.inTruck;
        }

        private static bool? GetDeathHeadInTruckStatus(PlayerDeathHead deathHead)
        {
            if (deathHead == null)
            {
                return null;
            }

            var roomVolume = GetDeathHeadRoomVolumeCheck(deathHead);
            if (roomVolume != null)
            {
                return IsRoomVolumeInTruck(roomVolume);
            }

            return deathHead.inTruck;
        }

        private static RoomVolumeCheck? GetDeathHeadRoomVolumeCheck(PlayerDeathHead deathHead)
        {
            return deathHead.roomVolumeCheck;
        }

        private static float GetConfiguredSeconds()
        {
            var seconds = Mathf.Clamp(FeatureFlags.LastChanceTimerSeconds, 30, 600);
            var step = Mathf.RoundToInt(seconds / 30f) * 30;
            return Mathf.Clamp(step, 30, 600);
        }

        private static float GetInitialTimerSeconds(int maxPlayers)
        {
            var baseSeconds = GetConfiguredSeconds();
            if (!FeatureFlags.LastChanceDynamicTimerEnabled)
            {
                ClearCachedDynamicTimerInputs();
                return baseSeconds;
            }

            var inputs = CollectDynamicTimerInputs(maxPlayers);
            CacheDynamicTimerInputs(inputs);
            var difficulty = GetRepoDifficultySnapshot();
            var rawAddedSeconds = CalculateRawAddedSeconds(inputs, difficulty, out var contributionFallback);
            var dynamicSeconds = baseSeconds + rawAddedSeconds;
            var difficultyFloorSeconds = GetDifficultyFloorSeconds(baseSeconds, difficulty, out var floorFallback);
            var candidateFinalSeconds = Mathf.Max(dynamicSeconds, difficultyFloorSeconds);
            var finalFallback = contributionFallback || floorFallback || !IsFinite(candidateFinalSeconds);
            var finalSeconds = finalFallback
                ? Mathf.Max(30f, difficultyFloorSeconds)
                : Mathf.Max(30f, candidateFinalSeconds);

            if (FeatureFlags.DebugLogging && LogLimiter.ShouldLog("LastChance.DynamicTimer", 30))
            {
                var criticalId = inputs.CriticalActorNumber > 0
                    ? $"actor:{inputs.CriticalActorNumber}"
                    : $"instance:{inputs.CriticalPlayerInstanceId}";
                Log.LogDebug(
                    $"[LastChance] DynamicTimer: base={baseSeconds:F1}s level={inputs.LevelNumber} D1={difficulty.Difficulty1:F3} D2={difficulty.Difficulty2:F3} D3={difficulty.Difficulty3:F3} repoProgress={difficulty.Progress:F3} " +
                    $"floorBonuses={FeatureFlags.LastChanceDifficulty1FloorBonusSeconds}/{FeatureFlags.LastChanceDifficulty2FloorBonusSeconds}/{FeatureFlags.LastChanceDifficulty3FloorBonusSeconds}s " +
                    $"required={inputs.RequiredPlayers} candidates={inputs.CandidateCount} critical={criticalId} pathSource={inputs.CriticalPathSource} " +
                    $"navDistance={inputs.CriticalNavMeshDistance:F1}m effectiveDistance={inputs.CriticalEffectiveDistanceMeters:F1}m roomSteps={inputs.CriticalRoomSteps} heightDelta={inputs.CriticalHeightDelta:F2}m " +
                    $"distanceCost={inputs.CriticalDistanceCostSeconds:F1}s roomCost={inputs.CriticalRoomCostSeconds:F1}s verticalCost={inputs.CriticalVerticalCostSeconds:F1}s returnCost={inputs.CriticalReturnCostSeconds:F1}s " +
                    $"aliveMonsters={inputs.AliveSearchMonsters} rawAdd={rawAddedSeconds:F1}s situationalDynamic={dynamicSeconds:F1}s difficultyFloor={difficultyFloorSeconds:F1}s final={finalSeconds:F1}s " +
                    $"difficultyFallback={difficulty.UsedFallback} numericFallback={finalFallback}");
            }

            return finalSeconds;
        }

        private static DynamicTimerInputs GetDynamicTimerInputsForRuntime(int maxPlayers)
        {
            if (s_hasCachedDynamicTimerInputs)
            {
                return s_cachedDynamicTimerInputs;
            }

            var inputs = CollectDynamicTimerInputs(maxPlayers);
            CacheDynamicTimerInputs(inputs);
            return inputs;
        }

        private static void CacheDynamicTimerInputs(DynamicTimerInputs inputs)
        {
            s_cachedDynamicTimerInputs = inputs;
            s_hasCachedDynamicTimerInputs = true;
        }

        private static void ClearCachedDynamicTimerInputs()
        {
            s_cachedDynamicTimerInputs = default;
            s_hasCachedDynamicTimerInputs = false;
        }

        private static void CacheDynamicTimerProfile(DynamicTimerProfileSnapshot profile)
        {
            s_lastDynamicTimerProfile = profile;
            s_hasDynamicTimerProfile = true;
        }

        private static void BeginActivationProfile()
        {
            if (!FeatureFlags.DebugLogging || s_activationProfilePending)
            {
                return;
            }

            s_activationProfilePending = true;
            s_activationProfileStartedAt = Time.realtimeSinceStartup;
            s_hasDynamicTimerProfile = false;
            s_hasActivationStartPhaseProfile = false;
            PlayerTruckDistanceHelper.BeginActivationProfiling();
        }

        private static void EmitActivationProfileSummary()
        {
            if (!s_activationProfilePending || !FeatureFlags.DebugLogging)
            {
                return;
            }

            s_activationProfilePending = false;
            var activationMs = (Time.realtimeSinceStartup - s_activationProfileStartedAt) * 1000f;
            var helperSummary = PlayerTruckDistanceHelper.EndActivationProfilingSummary();
            var dynamicSummary = s_hasDynamicTimerProfile
                ? $"dynamic=total={s_lastDynamicTimerProfile.TotalMs:F1}ms monsters={s_lastDynamicTimerProfile.MonstersMs:F1}ms records={s_lastDynamicTimerProfile.RecordsMs:F1}ms candidateBuild={s_lastDynamicTimerProfile.CandidateBuildMs:F1}ms criticalSelect={s_lastDynamicTimerProfile.CriticalSelectMs:F1}ms aggregate={s_lastDynamicTimerProfile.AggregateMs:F1}ms records={s_lastDynamicTimerProfile.RecordsCount} candidates={s_lastDynamicTimerProfile.CandidateCount} required={s_lastDynamicTimerProfile.RequiredPlayers} level={s_lastDynamicTimerProfile.LevelNumber} aliveMonsters={s_lastDynamicTimerProfile.AliveMonsters}"
                : "dynamic=not-collected";
            var startPhaseSummary = s_hasActivationStartPhaseProfile
                ? $"start=total={s_lastActivationStartPhaseProfile.TotalMs:F1}ms setActive={s_lastActivationStartPhaseProfile.SetActiveMs:F1}ms initialTimer={s_lastActivationStartPhaseProfile.InitialTimerMs:F1}ms captureCurrency={s_lastActivationStartPhaseProfile.CaptureCurrencyMs:F1}ms ensureNetwork={s_lastActivationStartPhaseProfile.EnsureNetworkMs:F1}ms showUi={s_lastActivationStartPhaseProfile.ShowUiMs:F1}ms clearState={s_lastActivationStartPhaseProfile.ClearStateMs:F1}ms broadcast={s_lastActivationStartPhaseProfile.BroadcastMs:F1}ms debugExtras={s_lastActivationStartPhaseProfile.DebugExtrasMs:F1}ms"
                : "start=not-collected";

            Log.LogDebug($"[LastChance] ActivationProfile: window={activationMs:F1}ms {startPhaseSummary} {dynamicSummary} helper={helperSummary}");
        }

        private static void ClearActivationProfileState()
        {
            s_activationProfilePending = false;
            s_activationProfileStartedAt = 0f;
            s_hasDynamicTimerProfile = false;
            s_lastDynamicTimerProfile = default;
            s_hasActivationStartPhaseProfile = false;
            s_lastActivationStartPhaseProfile = default;
        }

        private static DynamicTimerInputs CollectDynamicTimerInputs(int maxPlayers)
        {
            var profileEnabled = FeatureFlags.DebugLogging;
            var profileStart = profileEnabled ? Time.realtimeSinceStartup : 0f;
            var profileAfterMonsters = profileStart;
            var profileAfterRecords = profileStart;
            var profileAfterCandidates = profileStart;
            var profileAfterSelect = profileStart;
            var requiredPlayers = Mathf.Max(1, GetLastChanceNeededPlayers(maxPlayers));
            var levelNumber = GetCurrentLevelNumber();
            var aliveSearchMonsters = LastChanceMonstersSearchModule.GetAliveSearchMonsterCount();
            if (profileEnabled)
            {
                profileAfterMonsters = Time.realtimeSinceStartup;
            }

            // LastChance initial allocation must use one fresh complete assessment for every candidate.
            // Prewarm remains useful, but critical-return selection must not depend on stale route/height data.
            var records = PlayerTruckDistanceHelper.GetDistancesFromTruck(
                PlayerTruckDistanceHelper.DistanceQueryFields.All,
                players: null,
                forceRefresh: true);
            if (profileEnabled)
            {
                profileAfterRecords = Time.realtimeSinceStartup;
            }

            if (records.Length == 0)
            {
                if (profileEnabled)
                {
                    var totalMs = (Time.realtimeSinceStartup - profileStart) * 1000f;
                    var monstersMs = (profileAfterMonsters - profileStart) * 1000f;
                    var recordsMs = (profileAfterRecords - profileAfterMonsters) * 1000f;
                    CacheDynamicTimerProfile(new DynamicTimerProfileSnapshot(
                        totalMs,
                        monstersMs,
                        recordsMs,
                        0f,
                        0f,
                        0f,
                        0,
                        0,
                        requiredPlayers,
                        levelNumber,
                        aliveSearchMonsters));
                }

                return new DynamicTimerInputs(
                    requiredPlayers,
                    levelNumber,
                    aliveSearchMonsters,
                    0,
                    0,
                    0,
                    -1f,
                    0f,
                    0,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f,
                    ReturnPathSource.None);
            }

            var belowThreshold = Mathf.Min(0f, FeatureFlags.LastChanceBelowTruckThresholdMeters);
            var candidates = new List<ReturnCostCandidate>(records.Length);
            for (var i = 0; i < records.Length; i++)
            {
                candidates.Add(BuildReturnCostCandidate(records[i], belowThreshold));
            }
            if (profileEnabled)
            {
                profileAfterCandidates = Time.realtimeSinceStartup;
            }

            candidates.Sort((left, right) => left.ReturnCostSeconds.CompareTo(right.ReturnCostSeconds));
            var criticalOrdinal = Mathf.Clamp(requiredPlayers, 1, candidates.Count) - 1;
            var critical = candidates[criticalOrdinal];
            if (profileEnabled)
            {
                profileAfterSelect = Time.realtimeSinceStartup;
            }

            var criticalPlayerInstanceId = critical.PlayerAvatar != null
                ? critical.PlayerAvatar.GetInstanceID()
                : 0;

            if (profileEnabled)
            {
                var profileEnd = Time.realtimeSinceStartup;
                var totalMs = (profileEnd - profileStart) * 1000f;
                var monstersMs = (profileAfterMonsters - profileStart) * 1000f;
                var recordsMs = (profileAfterRecords - profileAfterMonsters) * 1000f;
                var candidateBuildMs = (profileAfterCandidates - profileAfterRecords) * 1000f;
                var criticalSelectMs = (profileAfterSelect - profileAfterCandidates) * 1000f;
                var aggregateMs = (profileEnd - profileAfterSelect) * 1000f;
                CacheDynamicTimerProfile(new DynamicTimerProfileSnapshot(
                    totalMs,
                    monstersMs,
                    recordsMs,
                    candidateBuildMs,
                    criticalSelectMs,
                    aggregateMs,
                    records.Length,
                    candidates.Count,
                    requiredPlayers,
                    levelNumber,
                    aliveSearchMonsters));
            }

            return new DynamicTimerInputs(
                requiredPlayers,
                levelNumber,
                aliveSearchMonsters,
                candidates.Count,
                critical.ActorNumber,
                criticalPlayerInstanceId,
                critical.NavMeshDistance,
                critical.EffectiveDistanceMeters,
                critical.EffectiveRoomSteps,
                critical.HeightDelta,
                critical.DistanceCostSeconds,
                critical.RoomCostSeconds,
                critical.VerticalCostSeconds,
                critical.ReturnCostSeconds,
                critical.PathSource);
        }

        private static ReturnCostCandidate BuildReturnCostCandidate(
            PlayerTruckDistanceHelper.PlayerTruckDistance record,
            float belowThreshold)
        {
            var pathSource = ReturnPathSource.NavMesh;
            var navMeshDistance = record.HasValidPath && record.NavMeshDistance >= 0f
                ? record.NavMeshDistance
                : -1f;
            var effectiveDistanceMeters = navMeshDistance;
            var effectiveRoomSteps = record.ShortestRoomPathToTruck;

            if (navMeshDistance < 0f)
            {
                if (record.ShortestRoomPathToTruck >= 0)
                {
                    pathSource = ReturnPathSource.RoomPathFallback;
                    // Reuse the existing timer difficulty heuristic of roughly 15 path-meters per room step.
                    // Keep at least one step so an invalid NavMesh path never becomes a zero-cost route.
                    effectiveDistanceMeters = Mathf.Max(1, record.ShortestRoomPathToTruck) * 15f;
                }
                else
                {
                    pathSource = ReturnPathSource.UnresolvedFallback;
                    // Current code already treats a fully unresolved route as the hardest selection case.
                    // Convert that conservative intent into bounded route arithmetic using known map size;
                    // when map-room count is unavailable, reuse the existing 14-step context normalization.
                    effectiveRoomSteps = record.TotalMapRooms > 0 ? record.TotalMapRooms : 14;
                    effectiveDistanceMeters = Mathf.Max(1, effectiveRoomSteps) * 15f;
                }
            }

            if (effectiveRoomSteps < 0)
            {
                effectiveRoomSteps = 0;
            }

            var distanceCostSeconds = Mathf.Max(0f, effectiveDistanceMeters) *
                                      Mathf.Max(0f, FeatureFlags.LastChanceTimerPerFarthestMeterSeconds);
            var roomCostSeconds = Mathf.Max(0, effectiveRoomSteps) *
                                  Mathf.Max(0f, FeatureFlags.LastChanceTimerPerRoomStepSeconds);

            var verticalCostSeconds = 0f;
            if (record.HeightDelta <= belowThreshold)
            {
                var belowMeters = Mathf.Max(0f, belowThreshold - record.HeightDelta);
                verticalCostSeconds += Mathf.Max(0f, FeatureFlags.LastChanceTimerPerBelowTruckPlayerSeconds);
                verticalCostSeconds += belowMeters * Mathf.Max(0f, FeatureFlags.LastChanceTimerPerBelowTruckMeterSeconds);
            }

            var returnCostSeconds = Mathf.Max(0f, distanceCostSeconds + roomCostSeconds + verticalCostSeconds);
            var actorNumber = record.PlayerAvatar?.photonView?.Owner?.ActorNumber ?? 0;

            return new ReturnCostCandidate(
                record.PlayerAvatar,
                actorNumber,
                navMeshDistance,
                Mathf.Max(0f, effectiveDistanceMeters),
                effectiveRoomSteps,
                record.HeightDelta,
                distanceCostSeconds,
                roomCostSeconds,
                verticalCostSeconds,
                returnCostSeconds,
                pathSource);
        }

        private static int GetCurrentLevelNumber()
        {
            var runMgr = RunManager.instance;
            if (runMgr == null)
            {
                return 1;
            }

            try
            {
                // Diagnostic/UI level only. Difficulty scaling itself comes from the vanilla SemiFunc APIs below.
                return Mathf.Max(1, runMgr.levelsCompleted + 1);
            }
            catch
            {
                return 1;
            }
        }

        private static RepoDifficultySnapshot GetRepoDifficultySnapshot()
        {
            if (RunManager.instance == null)
            {
                return new RepoDifficultySnapshot(0f, 0f, 0f, usedFallback: true);
            }

            try
            {
                var usedFallback = false;
                var difficulty1 = SanitizeDifficultyMultiplier(SemiFunc.RunGetDifficultyMultiplier1(), ref usedFallback);
                var difficulty2 = SanitizeDifficultyMultiplier(SemiFunc.RunGetDifficultyMultiplier2(), ref usedFallback);
                var difficulty3 = SanitizeDifficultyMultiplier(SemiFunc.RunGetDifficultyMultiplier3(), ref usedFallback);
                return new RepoDifficultySnapshot(difficulty1, difficulty2, difficulty3, usedFallback);
            }
            catch
            {
                return new RepoDifficultySnapshot(0f, 0f, 0f, usedFallback: true);
            }
        }

        private static float SanitizeDifficultyMultiplier(float value, ref bool usedFallback)
        {
            if (!IsFinite(value))
            {
                usedFallback = true;
                return 0f;
            }

            return Mathf.Clamp01(value);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float CalculateRawAddedSeconds(
            DynamicTimerInputs inputs,
            RepoDifficultySnapshot difficulty,
            out bool usedFiniteFallback)
        {
            var added = 0f;
            added += inputs.RequiredPlayers * Mathf.Max(0f, FeatureFlags.LastChanceTimerPerRequiredPlayerSeconds);
            added += inputs.CriticalReturnCostSeconds;
            added += inputs.AliveSearchMonsters * Mathf.Max(0f, FeatureFlags.LastChanceTimerPerMonsterSeconds);
            added *= CalculateDifficultyContributionMultiplier(inputs, difficulty);

            if (!IsFinite(added))
            {
                usedFiniteFallback = true;
                return 0f;
            }

            usedFiniteFallback = false;
            return Mathf.Max(0f, added);
        }

        private static float CalculateDifficultyContributionMultiplier(
            DynamicTimerInputs inputs,
            RepoDifficultySnapshot difficulty)
        {
            var levelScale = Mathf.Lerp(0.55f, 1f, difficulty.Progress);
            var roomFactor = Mathf.Clamp01(inputs.CriticalRoomSteps / 14f);
            var monsterFactor = Mathf.Clamp01(inputs.AliveSearchMonsters / 10f);
            var roomWeight = Mathf.Max(0f, FeatureFlags.LastChanceLevelContextRoomWeight);
            var monsterWeight = Mathf.Max(0f, FeatureFlags.LastChanceLevelContextMonsterWeight);
            var contextMultiplier = 1f + (roomFactor * roomWeight) + (monsterFactor * monsterWeight);
            var contextScale = Mathf.Lerp(1f, contextMultiplier, difficulty.Progress);
            var multiplier = levelScale * contextScale;

            return IsFinite(multiplier) ? Mathf.Max(0.1f, multiplier) : 1f;
        }

        private static float GetDifficultyFloorSeconds(
            float baseSeconds,
            RepoDifficultySnapshot difficulty,
            out bool usedFiniteFallback)
        {
            var floorSeconds = baseSeconds +
                               (difficulty.Difficulty1 * Mathf.Max(0, FeatureFlags.LastChanceDifficulty1FloorBonusSeconds)) +
                               (difficulty.Difficulty2 * Mathf.Max(0, FeatureFlags.LastChanceDifficulty2FloorBonusSeconds)) +
                               (difficulty.Difficulty3 * Mathf.Max(0, FeatureFlags.LastChanceDifficulty3FloorBonusSeconds));

            if (!IsFinite(floorSeconds))
            {
                usedFiniteFallback = true;
                return Mathf.Max(30f, baseSeconds);
            }

            usedFiniteFallback = false;
            return Mathf.Max(30f, floorSeconds);
        }

        private static string FormatTimerText(float secondsRemaining)
        {
            var seconds = Mathf.CeilToInt(secondsRemaining);
            var minutes = seconds / 60;
            var secs = seconds % 60;
            var color = seconds <= 30 ? FlashColor : TimerColor;
            var colorHex = ColorUtility.ToHtmlStringRGB(color);
            return $"<color=#{colorHex}><b>LAST CHANCE</b>  {minutes:0}:{secs:00}</color>";
        }

        private static void SetLastChanceActive(bool active)
        {
            var wasActive = s_active;
            s_active = active;
            if (active)
            {
                LastChanceRuntimeOrchestrator.EnterActiveRuntime();
                if (!wasActive)
                {
                    LastChanceReviveReleaseTracker.CapturePendingDeathsAtActivation();
                }
                ApplyLastChanceHostRuntimeOverrides();
                return;
            }

            AbilityModule.ReleaseDirectionSlot();
            LastChanceSpectateHelper.ResetOwnedState();
            LastChanceRuntimeOrchestrator.ExitRuntime("lastchance-deactivated");
            LastChanceRuntimeObjectRegistry.ResetForRuntimeDeactivated();
            ClearDirectionPenaltyCache();
            ClearLastChanceHostRuntimeOverrides();
            LastChanceMonstersNoiseAggroModule.ResetRuntimeState();
            LastChanceMonstersSearchModule.ResetRuntimeState();
            LastChanceMonstersVoiceEnemyOnlyModule.ResetRuntimeState();
            LastChanceMonstersCameraForceLockModule.ResetRuntimeState();
            LastChanceMonstersPlayerVisionCheckModule.ResetRuntimeState();
            LastChanceMonstersAnimalHeadVisionFallbackModule.ResetRuntimeState();
            LastChanceMonstersCarryProxyModule.ResetRuntimeState();
            LastChanceMonstersOnScreenCameraModule.ResetRuntimeState();
            LastChanceMonstersThinManStandModule.ResetRuntimeState();
            LastChanceHeadPupilVisualModule.ResetRuntimeState();
            LastChanceHeadEyesOverrideBypassModule.ResetRuntimeState();
        }

        private static void ResetLastChanceRuntimeModules(bool allowVanillaAllPlayersDead, bool allowAutoDelete)
        {
            LastChanceMonstersNoiseAggroModule.ResetRuntimeState();
            LastChanceMonstersSearchModule.ResetRuntimeState();
            LastChanceMonstersVoiceEnemyOnlyModule.ResetRuntimeState();
            LastChanceMonstersCameraForceLockModule.ResetRuntimeState();
            LastChanceMonstersPlayerVisionCheckModule.ResetRuntimeState();
            LastChanceMonstersAnimalHeadVisionFallbackModule.ResetRuntimeState();
            LastChanceMonstersCarryProxyModule.ResetRuntimeState();
            LastChanceMonstersOnScreenCameraModule.ResetRuntimeState();
            LastChanceMonstersThinManStandModule.ResetRuntimeState();
            LastChanceHeadPupilVisualModule.ResetRuntimeState();
            LastChanceHeadEyesOverrideBypassModule.ResetRuntimeState();
            LastChanceSpectateHelper.ResetForceState();

            if (allowAutoDelete)
            {
                LastChanceSaveDeleteState.AllowAutoDelete();
            }
            else
            {
                LastChanceSaveDeleteState.ResetAutoDeleteBlock();
            }

            if (allowVanillaAllPlayersDead)
            {
                AllPlayersDeadGuard.AllowVanillaAllPlayersDead();
            }
            else
            {
                AllPlayersDeadGuard.ResetVanillaAllPlayersDead();
            }
        }

        private static void ApplyLastChanceHostRuntimeOverrides()
        {
            if (!LastChanceRuntimeOrchestrator.IsRuntimeActive)
            {
                return;
            }

            if (!SemiFunc.IsMasterClientOrSingleplayer())
            {
                return;
            }

            ScalerCoreInteropBridge.TryRestoreLocalPlayerCameraState();

            if (s_lastChanceBatteryOverrideApplied)
            {
                return;
            }

            s_lastChanceBatteryOverrideApplied = BatteryJumpOverrideLease.TryAcquireHostOverride(
                LastChanceBatteryLeaseOwnerId,
                batteryJumpEnabled: false);
        }

        internal static void ReleaseBatteryOverrideForExternalTeardown()
        {
            ClearLastChanceHostRuntimeOverrides();
        }

        private static void ClearLastChanceHostRuntimeOverrides()
        {
            if (!s_lastChanceBatteryOverrideApplied)
            {
                return;
            }

            BatteryJumpOverrideLease.ReleaseHostOverride(LastChanceBatteryLeaseOwnerId);
            s_lastChanceBatteryOverrideApplied = false;
        }

        private static object[] BuildSurrenderedActorsPayload()
        {
            if (LastChanceSurrenderedPlayers.Count == 0)
            {
                return System.Array.Empty<object>();
            }

            var payload = new object[LastChanceSurrenderedPlayers.Count];
            var index = 0;
            foreach (var actor in LastChanceSurrenderedPlayers)
            {
                payload[index++] = actor;
            }

            return payload;
        }

        private static float ComputeAuthoritativeRemaining(float secondsRemaining, double hostSentAt)
        {
            if (!SemiFunc.IsMultiplayer())
            {
                return Mathf.Max(0f, secondsRemaining);
            }

            var elapsed = PhotonNetwork.Time - hostSentAt;
            if (double.IsNaN(elapsed) || double.IsInfinity(elapsed) || elapsed < 0d)
            {
                elapsed = 0d;
            }

            return Mathf.Max(0f, secondsRemaining - (float)elapsed);
        }
    }

}

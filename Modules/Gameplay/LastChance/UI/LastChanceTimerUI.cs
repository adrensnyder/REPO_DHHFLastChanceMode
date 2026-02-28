#nullable enable

using System;
using System.Collections.Generic;
using DHHFLastChanceMode.Modules.Config;
using DHHFLastChanceMode.Modules.Utilities;
using DeathHeadHopper.Managers;
using DeathHeadHopper.UI;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.UI
{
    internal static class LastChanceTimerUI
    {
        private const string SemibotWhiteFileName = "SemibotWhite.png";
        private const string TruckWhiteFileName = "TruckWhite.png";
        private const string SemibotSurrenderedFileName = "SemibotSurrendered.png";
        private const string SemibotSafeFileName = "SemibotSafe.png";

        private const int MaxPlayerIcons = 6;
        private const float TopPaddingFromCanvas = 8f;
        private const float DefaultTimerVerticalPosition = -TopPaddingFromCanvas;
        private const float SurrenderHintSpacing = 4f;
        private const float TimerFontSize = 14f;
        private const float SurrenderHintFontSize = 12f;
        private const float PlayerIconSize = 28f;
        private const float PlayerIconSpacing = 8f;
        private const float PlayerStatusOverlayScale = 0.75f;
        private const float TruckWidgetOffsetX = 165f;
        private const float TimerToSurrenderGap = 0.5f;
        private const float SurrenderToIconsGap = 4f;
        private const float TruckCounterOffsetY = -22f;
        private const float TruckCounterBadgeWidth = 74f;
        private const float TruckCounterBadgeHeight = 22f;
        private const float TruckCounterBadgeBorderThickness = 2f;
        private const float TruckCounterFontSize = 11f;

        private static readonly TextAlignmentOptions CenterAlignment = TextAlignmentOptions.Center;
        private static readonly TextAlignmentOptions TopAlignment = TextAlignmentOptions.Top;

        private static TextMeshProUGUI? s_label;
        private static RectTransform? s_rect;
        private static TextMeshProUGUI? s_hintLabel;
        private static RectTransform? s_hintRect;
        private static string s_defaultHintText = string.Empty;
        private static string s_lastTimerText = string.Empty;
        private static string s_lastHintText = string.Empty;
        private static bool s_isVisible;
        private static Transform? s_cachedUiParent;
        private static float s_nextVisibilityRefreshAt;

        private static RectTransform? s_playersRoot;
        private static readonly List<PlayerIconSlot> s_playerSlots = new(MaxPlayerIcons);
        private static RectTransform? s_truckRoot;
        private static Image? s_truckIconImage;
        private static TextMeshProUGUI? s_truckCounterLabel;
        private static string s_lastTruckCounterText = string.Empty;

        private static Sprite? s_semibotWhiteSprite;
        private static Sprite? s_truckWhiteSprite;
        private static Sprite? s_semibotSurrenderedSprite;
        private static Sprite? s_semibotSafeSprite;
        private static bool s_assetLoadAttempted;
        private static float s_nextAssetRetryAt;
        private const float AssetRetryIntervalSeconds = 2f;

        private const float VisibilityRefreshIntervalSeconds = 0.5f;
        private const float AbilitySpotCacheRefreshIntervalSeconds = 0.5f;
        private static AbilitySpot[] s_cachedAbilitySpots = Array.Empty<AbilitySpot>();
        private static float s_nextAbilitySpotCacheRefreshAt;

        private sealed class PlayerIconSlot
        {
            internal RectTransform? Root;
            internal Image? BaseImage;
            internal Image? SurrenderImage;
            internal Image? SafeImage;
        }

        internal static void Show(string defaultHintText)
        {
            s_defaultHintText = defaultHintText;
            if (s_label != null)
            {
                ReparentToPreferredUiRoot();
                EnsureSpritesLoaded();
                RefreshVisibility(force: true);
                return;
            }

            var parent = ResolvePreferredUiParent();
            if (parent == null)
            {
                return;
            }

            EnsureSpritesLoaded();

            var go = new GameObject("LastChanceTimer", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            s_rect = go.GetComponent<RectTransform>();
            s_rect.anchorMin = new Vector2(0.5f, 1f);
            s_rect.anchorMax = new Vector2(0.5f, 1f);
            s_rect.pivot = new Vector2(0.5f, 1f);
            s_rect.anchoredPosition = new Vector2(0f, DefaultTimerVerticalPosition);
            s_rect.sizeDelta = new Vector2(700f, 120f);

            s_label = go.AddComponent<TextMeshProUGUI>();
            SetDefaults(s_label);
            LastChanceTimerChangeEffectsModule.Initialize(
                s_rect,
                s_label,
                TimerFontSize);
            SetEnabled(false);

            var hintGo = new GameObject("LastChanceSurrenderHint", typeof(RectTransform));
            hintGo.transform.SetParent(go.transform, false);
            s_hintRect = hintGo.GetComponent<RectTransform>();
            if (s_hintRect != null)
            {
                s_hintRect.anchorMin = new Vector2(0.5f, 1f);
                s_hintRect.anchorMax = new Vector2(0.5f, 1f);
                s_hintRect.pivot = new Vector2(0.5f, 1f);
                s_hintRect.sizeDelta = new Vector2(700f, 26f);
                s_hintRect.anchoredPosition = new Vector2(0f, GetSurrenderOffsetY());
            }

            s_hintLabel = hintGo.AddComponent<TextMeshProUGUI>();
            SetHintDefaults(s_hintLabel);
            s_lastHintText = string.Empty;
            SetSurrenderHintText(s_defaultHintText);

            CreatePlayerSlots(go.transform);
            CreateTruckWidget(go.transform);

            KeepAtTopPosition();
            RefreshVisibility(force: true);
        }

        internal static void Prewarm(string defaultHintText)
        {
            Show(defaultHintText);
            Hide();
            ResetSurrenderHint();
        }

        internal static void PrewarmAssets()
        {
            EnsureSpritesLoaded();
        }

        internal static void DestroyUi()
        {
            try
            {
                if (s_label is Component component && component != null)
                {
                    var go = component.gameObject;
                    if (go != null)
                    {
                        UnityEngine.Object.Destroy(go);
                    }
                }
            }
            catch
            {
                // Scene unload can invalidate Unity objects between checks.
            }

            s_label = null;
            s_rect = null;
            s_hintLabel = null;
            s_hintRect = null;
            s_lastTimerText = string.Empty;
            s_lastHintText = string.Empty;
            s_isVisible = false;
            s_cachedUiParent = null;
            s_playersRoot = null;
            s_playerSlots.Clear();
            s_truckRoot = null;
            s_truckIconImage = null;
            s_truckCounterLabel = null;
            s_lastTruckCounterText = string.Empty;
            s_cachedAbilitySpots = Array.Empty<AbilitySpot>();
            s_nextAbilitySpotCacheRefreshAt = 0f;
            LastChanceTimerChangeEffectsModule.Reset();
        }

        internal static void UpdateText(string text)
        {
            if (s_label == null)
            {
                return;
            }

            RefreshVisibility(force: false);
            LastChanceTimerChangeEffectsModule.OnBaseTimerTextUpdated(text);
            LastChanceTimerChangeEffectsModule.Tick();
            if (string.Equals(s_lastTimerText, text, StringComparison.Ordinal))
            {
                return;
            }

            s_lastTimerText = text;
        }

        internal static void NotifyTimerDelta(float deltaSeconds, bool isNetworkSync = false)
        {
            if (s_label == null)
            {
                return;
            }

            if (isNetworkSync)
            {
                LastChanceTimerChangeEffectsModule.NotifyNetworkDelta(deltaSeconds);
            }
            else
            {
                LastChanceTimerChangeEffectsModule.NotifyLocalDelta(deltaSeconds);
            }
        }

        internal static void UpdatePlayerStates(IReadOnlyList<PlayerStateExtractionHelper.PlayerStateSnapshot> snapshots, int requiredOnTruck)
        {
            if (s_label == null || s_playersRoot == null)
            {
                return;
            }

            EnsureSpritesLoaded();

            var count = Mathf.Min(MaxPlayerIcons, snapshots?.Count ?? 0);
            var width = count <= 0 ? 0f : (count * PlayerIconSize) + ((count - 1) * PlayerIconSpacing);
            var startX = -0.5f * width + (0.5f * PlayerIconSize);

            var onTruck = 0;
            if (snapshots != null)
            {
                for (var i = 0; i < snapshots.Count; i++)
                {
                    if (!snapshots[i].IsSurrendered && snapshots[i].IsInTruck)
                    {
                        onTruck++;
                    }
                }
            }

            UpdateTruckCounter(onTruck, Mathf.Max(1, requiredOnTruck));

            for (var i = 0; i < s_playerSlots.Count; i++)
            {
                var slot = s_playerSlots[i];
                if (slot.Root == null)
                {
                    continue;
                }

                if (i >= count || snapshots == null)
                {
                    slot.Root.gameObject.SetActive(false);
                    continue;
                }

                var snapshot = snapshots[i];
                slot.Root.gameObject.SetActive(true);
                slot.Root.anchoredPosition = new Vector2(startX + (i * (PlayerIconSize + PlayerIconSpacing)), 0f);

                SetImage(slot.BaseImage, s_semibotWhiteSprite, snapshot.Color, true);
                SetImage(slot.SafeImage, s_semibotSafeSprite, Color.white, !snapshot.IsSurrendered && snapshot.IsInTruck);
                SetImage(slot.SurrenderImage, s_semibotSurrenderedSprite, Color.white, snapshot.IsSurrendered);
            }
        }

        internal static void Hide()
        {
            if (s_label == null)
            {
                return;
            }

            SetEnabled(false);
            LastChanceTimerChangeEffectsModule.SetVisible(false);
        }

        internal static void SetSurrenderHintText(string text)
        {
            if (s_hintLabel == null)
            {
                return;
            }

            if (string.Equals(s_lastHintText, text, StringComparison.Ordinal))
            {
                return;
            }

            s_lastHintText = text;
            s_hintLabel.text = text;
        }

        internal static void ResetSurrenderHint()
        {
            SetSurrenderHintText(s_defaultHintText);
        }

        private static void CreatePlayerSlots(Transform parent)
        {
            var playersGo = new GameObject("LastChancePlayersRow", typeof(RectTransform));
            playersGo.transform.SetParent(parent, false);
            s_playersRoot = playersGo.GetComponent<RectTransform>();
            if (s_playersRoot != null)
            {
                s_playersRoot.anchorMin = new Vector2(0.5f, 1f);
                s_playersRoot.anchorMax = new Vector2(0.5f, 1f);
                s_playersRoot.pivot = new Vector2(0.5f, 1f);
                s_playersRoot.anchoredPosition = new Vector2(0f, GetPlayerIconsOffsetY());
                s_playersRoot.sizeDelta = new Vector2(500f, 36f);
            }

            s_playerSlots.Clear();
            for (var i = 0; i < MaxPlayerIcons; i++)
            {
                var slotGo = new GameObject($"PlayerSlot.{i}", typeof(RectTransform));
                slotGo.transform.SetParent(playersGo.transform, false);
                var slotRect = slotGo.GetComponent<RectTransform>();
                slotRect.anchorMin = new Vector2(0.5f, 1f);
                slotRect.anchorMax = new Vector2(0.5f, 1f);
                slotRect.pivot = new Vector2(0.5f, 1f);
                slotRect.sizeDelta = new Vector2(PlayerIconSize, PlayerIconSize);

                var baseImage = AddImageComponent(slotGo);

                var safeGo = new GameObject("Safe", typeof(RectTransform));
                safeGo.transform.SetParent(slotGo.transform, false);
                var safeRect = safeGo.GetComponent<RectTransform>();
                safeRect.anchorMin = new Vector2(0.5f, 0.5f);
                safeRect.anchorMax = new Vector2(0.5f, 0.5f);
                safeRect.pivot = new Vector2(0.5f, 0.5f);
                safeRect.anchoredPosition = Vector2.zero;
                safeRect.sizeDelta = Vector2.one * (PlayerIconSize * PlayerStatusOverlayScale);
                var safeImage = AddImageComponent(safeGo);

                var surrenderedGo = new GameObject("Surrendered", typeof(RectTransform));
                surrenderedGo.transform.SetParent(slotGo.transform, false);
                var surrenderedRect = surrenderedGo.GetComponent<RectTransform>();
                surrenderedRect.anchorMin = new Vector2(0.5f, 0.5f);
                surrenderedRect.anchorMax = new Vector2(0.5f, 0.5f);
                surrenderedRect.pivot = new Vector2(0.5f, 0.5f);
                surrenderedRect.anchoredPosition = Vector2.zero;
                surrenderedRect.sizeDelta = Vector2.one * (PlayerIconSize * PlayerStatusOverlayScale);
                var surrenderedImage = AddImageComponent(surrenderedGo);

                slotGo.SetActive(false);
                s_playerSlots.Add(new PlayerIconSlot
                {
                    Root = slotRect,
                    BaseImage = baseImage,
                    SafeImage = safeImage,
                    SurrenderImage = surrenderedImage
                });
            }
        }

        private static void CreateTruckWidget(Transform parent)
        {
            var truckGo = new GameObject("LastChanceTruckCounter", typeof(RectTransform));
            truckGo.transform.SetParent(parent, false);
            s_truckRoot = truckGo.GetComponent<RectTransform>();
            if (s_truckRoot != null)
            {
                s_truckRoot.anchorMin = new Vector2(0.5f, 1f);
                s_truckRoot.anchorMax = new Vector2(0.5f, 1f);
                s_truckRoot.pivot = new Vector2(0.5f, 1f);
                s_truckRoot.anchoredPosition = new Vector2(TruckWidgetOffsetX, -1f);
                s_truckRoot.sizeDelta = new Vector2(64f, 56f);
            }

            var truckIconGo = new GameObject("TruckIcon", typeof(RectTransform));
            truckIconGo.transform.SetParent(truckGo.transform, false);
            var iconRect = truckIconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 1f);
            iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.anchoredPosition = new Vector2(0f, 0f);
            iconRect.sizeDelta = new Vector2(26f, 26f);
            s_truckIconImage = AddImageComponent(truckIconGo);

            CreateTruckCounterBadge(truckGo.transform);

            SetTruckCounterText("00 / 00");
        }

        private static void UpdateTruckCounter(int onTruck, int required)
        {
            if (s_truckIconImage != null)
            {
                SetImage(s_truckIconImage, s_truckWhiteSprite, Color.white, true);
            }

            SetTruckCounterText($"{Mathf.Clamp(onTruck, 0, 99):00} / {Mathf.Clamp(required, 0, 99):00}");
        }

        private static void SetTruckCounterText(string text)
        {
            if (s_truckCounterLabel == null)
            {
                return;
            }

            if (string.Equals(s_lastTruckCounterText, text, StringComparison.Ordinal))
            {
                return;
            }

            s_lastTruckCounterText = text;
            s_truckCounterLabel.text = text;
            s_truckCounterLabel.enabled = true;
            s_truckCounterLabel.gameObject.SetActive(true);
        }

        private static void CreateTruckCounterBadge(Transform parent)
        {
            var outerGo = new GameObject("TruckCounterBadgeOuter", typeof(RectTransform));
            outerGo.transform.SetParent(parent, false);
            var outerRect = outerGo.GetComponent<RectTransform>();
            outerRect.anchorMin = new Vector2(0.5f, 1f);
            outerRect.anchorMax = new Vector2(0.5f, 1f);
            outerRect.pivot = new Vector2(0.5f, 1f);
            outerRect.anchoredPosition = new Vector2(0f, TruckCounterOffsetY);
            outerRect.sizeDelta = new Vector2(TruckCounterBadgeWidth, TruckCounterBadgeHeight);
            var outerImage = AddImageComponent(outerGo);
            SetImage(outerImage, null, new Color(1f, 0.84f, 0.12f, 1f), true);

            var innerGo = new GameObject("TruckCounterBadgeInner", typeof(RectTransform));
            innerGo.transform.SetParent(outerGo.transform, false);
            var innerRect = innerGo.GetComponent<RectTransform>();
            innerRect.anchorMin = new Vector2(0.5f, 0.5f);
            innerRect.anchorMax = new Vector2(0.5f, 0.5f);
            innerRect.pivot = new Vector2(0.5f, 0.5f);
            innerRect.anchoredPosition = Vector2.zero;
            innerRect.sizeDelta = new Vector2(
                Mathf.Max(1f, TruckCounterBadgeWidth - (2f * TruckCounterBadgeBorderThickness)),
                Mathf.Max(1f, TruckCounterBadgeHeight - (2f * TruckCounterBadgeBorderThickness)));
            var innerImage = AddImageComponent(innerGo);
            SetImage(innerImage, null, new Color(0f, 0f, 0f, 0.55f), true);

            var labelGo = new GameObject("TruckCounterText", typeof(RectTransform));
            labelGo.transform.SetParent(innerGo.transform, false);
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = Vector2.zero;
            s_truckCounterLabel = labelGo.AddComponent<TextMeshProUGUI>();
            SetHintDefaults(s_truckCounterLabel);
            s_truckCounterLabel.fontSize = TruckCounterFontSize;
            s_truckCounterLabel.color = new Color(1f, 0.92f, 0.3f, 1f);
        }

        private static Image? AddImageComponent(GameObject go)
        {
            return go.AddComponent<Image>();
        }

        private static void SetImage(Image? image, Sprite? sprite, Color color, bool enabled)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = true;
            image.enabled = enabled;
            image.gameObject.SetActive(enabled);
        }

        private static void EnsureSpritesLoaded()
        {
            if (HasAllSprites())
            {
                return;
            }

            var now = Time.unscaledTime;
            if (s_assetLoadAttempted && now < s_nextAssetRetryAt)
            {
                return;
            }

            s_assetLoadAttempted = true;
            s_nextAssetRetryAt = now + AssetRetryIntervalSeconds;
            var baseDir = ImageAssetLoader.GetDefaultAssetsDirectory();
            if (s_semibotWhiteSprite == null)
            {
                ImageAssetLoader.TryLoadSprite(SemibotWhiteFileName, baseDir, out s_semibotWhiteSprite, out _);
            }
            if (s_truckWhiteSprite == null)
            {
                ImageAssetLoader.TryLoadSprite(TruckWhiteFileName, baseDir, out s_truckWhiteSprite, out _);
            }
            if (s_semibotSurrenderedSprite == null)
            {
                ImageAssetLoader.TryLoadSprite(SemibotSurrenderedFileName, baseDir, out s_semibotSurrenderedSprite, out _);
            }
            if (s_semibotSafeSprite == null)
            {
                ImageAssetLoader.TryLoadSprite(SemibotSafeFileName, baseDir, out s_semibotSafeSprite, out _);
            }

            if (FeatureFlags.DebugLogging && LogLimiter.ShouldLog("LastChance.UI.Sprites", 30))
            {
                Debug.Log(
                    $"[LastChance] UI sprites status: semibot={(s_semibotWhiteSprite != null)} truck={(s_truckWhiteSprite != null)} " +
                    $"surrendered={(s_semibotSurrenderedSprite != null)} safe={(s_semibotSafeSprite != null)}");
            }
        }

        private static bool HasAllSprites()
        {
            return s_semibotWhiteSprite != null &&
                   s_truckWhiteSprite != null &&
                   s_semibotSurrenderedSprite != null &&
                   s_semibotSafeSprite != null;
        }

        private static void RefreshVisibility(bool force)
        {
            if (!force && Time.unscaledTime < s_nextVisibilityRefreshAt)
            {
                return;
            }

            s_nextVisibilityRefreshAt = Time.unscaledTime + VisibilityRefreshIntervalSeconds;
            KeepAtTopPosition();
            UpdateSurrenderHintPosition();
            SetEnabled(ShouldBeVisibleNow());
        }

        private static void KeepAtTopPosition()
        {
            if (s_rect == null)
            {
                return;
            }

            var pos = s_rect.anchoredPosition;
            if (Mathf.Abs(pos.y - DefaultTimerVerticalPosition) <= 0.01f)
            {
                return;
            }

            s_rect.anchoredPosition = new Vector2(pos.x, DefaultTimerVerticalPosition);
        }

        private static void UpdateSurrenderHintPosition()
        {
            if (s_rect == null || s_hintRect == null)
            {
                return;
            }

            var targetOffset = GetSurrenderOffsetY();
            var pos = s_hintRect.anchoredPosition;
            if (Mathf.Abs(pos.y - targetOffset) > 0.01f)
            {
                s_hintRect.anchoredPosition = new Vector2(pos.x, targetOffset);
            }

            if (s_playersRoot != null)
            {
                var p = s_playersRoot.anchoredPosition;
                var playersOffset = GetPlayerIconsOffsetY();
                if (Mathf.Abs(p.y - playersOffset) > 0.01f)
                {
                    s_playersRoot.anchoredPosition = new Vector2(p.x, playersOffset);
                }
            }
        }

        private static float GetSurrenderOffsetY()
        {
            return -(TimerFontSize + TimerToSurrenderGap);
        }

        private static float GetPlayerIconsOffsetY()
        {
            return GetSurrenderOffsetY() - (SurrenderHintFontSize + SurrenderToIconsGap);
        }

        private static bool ShouldBeVisibleNow()
        {
            if (!LastChanceTimerController.IsActive)
            {
                return false;
            }

            if (GetPreferredUiParentCached() == null)
            {
                return false;
            }

            if (MenuManager.instance != null && MenuManager.instance.currentMenuPage != null)
            {
                return false;
            }

            return true;
        }

        private static void ReparentToPreferredUiRoot()
        {
            if (s_label is not Component labelComponent)
            {
                return;
            }

            var parent = GetPreferredUiParentCached();
            if (parent == null)
            {
                return;
            }

            if (labelComponent.transform.parent != parent)
            {
                labelComponent.transform.SetParent(parent, false);
            }
        }

        private static Transform? ResolvePreferredUiParent()
        {
            if (DHHUIManager.instance != null)
            {
                var gameHud = DHHUIManager.instance.gameHUD;
                if (gameHud != null)
                {
                    return gameHud.transform;
                }
            }

            return HUDCanvas.instance != null ? HUDCanvas.instance.transform : null;
        }

        private static Transform? GetPreferredUiParentCached()
        {
            if (s_cachedUiParent != null)
            {
                return s_cachedUiParent;
            }

            s_cachedUiParent = ResolvePreferredUiParent();
            return s_cachedUiParent;
        }

        private static void SetDefaults(TextMeshProUGUI label)
        {
            label.color = Color.white;
            label.fontSize = TimerFontSize;
            label.enableAutoSizing = false;
            label.enableWordWrapping = false;
            label.richText = true;
            label.alignment = TopAlignment;
        }

        private static void SetHintDefaults(TextMeshProUGUI label)
        {
            label.color = Color.white;
            label.fontSize = SurrenderHintFontSize;
            label.enableAutoSizing = false;
            label.enableWordWrapping = false;
            label.richText = true;
            label.alignment = CenterAlignment;
        }

        private static void SetEnabled(bool enabled)
        {
            if (s_label == null)
            {
                return;
            }

            if (s_isVisible == enabled)
            {
                return;
            }
            s_isVisible = enabled;

            s_label.enabled = enabled;
            s_label.gameObject.SetActive(enabled);
            if (s_hintLabel != null)
            {
                s_hintLabel.enabled = enabled;
                s_hintLabel.gameObject.SetActive(enabled);
            }

            LastChanceTimerChangeEffectsModule.SetVisible(enabled);
        }

        private static void TryApplyAbilityCostTextStyle(TextMeshProUGUI? label)
        {
            if (label == null)
            {
                return;
            }

            var spots = GetAbilitySpotsCached();
            if (spots == null || spots.Length == 0)
            {
                return;
            }

            for (var i = 0; i < spots.Length; i++)
            {
                if (spots[i].energyCost is not TextMeshProUGUI costLabel)
                {
                    continue;
                }

                label.font = costLabel.font;
                label.fontSharedMaterial = costLabel.fontSharedMaterial;
                label.color = Color.black;

                return;
            }
        }

        private static AbilitySpot[] GetAbilitySpotsCached()
        {
            if (Time.unscaledTime < s_nextAbilitySpotCacheRefreshAt)
            {
                return s_cachedAbilitySpots;
            }

            s_cachedAbilitySpots = UnityEngine.Object.FindObjectsOfType<AbilitySpot>();
            s_nextAbilitySpotCacheRefreshAt = Time.unscaledTime + AbilitySpotCacheRefreshIntervalSeconds;
            return s_cachedAbilitySpots;
        }

        
    }
}


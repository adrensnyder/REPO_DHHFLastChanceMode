#nullable enable

using System;
using System.Text.RegularExpressions;
using DHHFLastChanceMode.Modules.Config;
using UnityEngine;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.UI
{
    internal static class LastChanceTimerChangeEffectsModule
    {
        private static readonly Color PositiveDeltaColor = new(0.35f, 1f, 0.45f, 1f);
        private static readonly Color NegativeDeltaColor = new(1f, 0.35f, 0.35f, 1f);
        private static readonly Regex ColorTagRegex = new("<color=#[0-9A-Fa-f]{6,8}>", RegexOptions.Compiled);

        private static Component? s_timerLabel;
        private static RectTransform? s_timerRect;
        private static Component? s_floatingLabel;
        private static RectTransform? s_floatingRect;

        private static System.Reflection.PropertyInfo? s_textProperty;
        private static System.Reflection.PropertyInfo? s_colorProperty;
        private static System.Reflection.PropertyInfo? s_alignmentProperty;
        private static System.Reflection.PropertyInfo? s_fontSizeProperty;
        private static System.Reflection.PropertyInfo? s_autoSizeProperty;
        private static System.Reflection.PropertyInfo? s_wordWrapProperty;
        private static System.Reflection.PropertyInfo? s_richTextProperty;

        private static object? s_centerAlignment;
        private static float s_timerFontSize;
        private static bool s_initialized;

        private static string s_baseTimerText = string.Empty;
        private static string s_lastRenderedTimerText = string.Empty;
        private static Vector3 s_timerBaseScale = Vector3.one;

        private static float s_pulseRemainingSeconds;
        private static Color s_pulseColor = Color.white;

        private static float s_floatingRemainingSeconds;
        private static float s_floatingDropDistance;
        private static Color s_floatingBaseColor = Color.white;

        internal static void Initialize(
            RectTransform? timerRect,
            Component? timerLabel,
            Type? labelType,
            System.Reflection.PropertyInfo? textProperty,
            System.Reflection.PropertyInfo? colorProperty,
            System.Reflection.PropertyInfo? alignmentProperty,
            System.Reflection.PropertyInfo? fontSizeProperty,
            System.Reflection.PropertyInfo? autoSizeProperty,
            System.Reflection.PropertyInfo? wordWrapProperty,
            System.Reflection.PropertyInfo? richTextProperty,
            object? centerAlignment,
            float timerFontSize)
        {
            s_timerRect = timerRect;
            s_timerLabel = timerLabel;
            s_textProperty = textProperty;
            s_colorProperty = colorProperty;
            s_alignmentProperty = alignmentProperty;
            s_fontSizeProperty = fontSizeProperty;
            s_autoSizeProperty = autoSizeProperty;
            s_wordWrapProperty = wordWrapProperty;
            s_richTextProperty = richTextProperty;
            s_centerAlignment = centerAlignment;
            s_timerFontSize = Mathf.Max(1f, timerFontSize);
            s_timerBaseScale = timerRect != null ? timerRect.localScale : Vector3.one;
            s_baseTimerText = string.Empty;
            s_lastRenderedTimerText = string.Empty;

            EnsureFloatingLabel(labelType);
            ResetVisualState();
            s_initialized = s_timerRect != null && s_timerLabel != null;
        }

        internal static void OnBaseTimerTextUpdated(string text)
        {
            s_baseTimerText = text ?? string.Empty;
            if (s_pulseRemainingSeconds <= 0f)
            {
                RenderTimerText(s_baseTimerText);
            }
        }

        internal static void NotifyLocalDelta(float deltaSeconds)
        {
            TriggerDeltaEffect(deltaSeconds, Mathf.Max(0f, InternalConfig.LastChanceTimerChangeLocalDeltaMinSeconds));
        }

        internal static void NotifyNetworkDelta(float deltaSeconds)
        {
            TriggerDeltaEffect(deltaSeconds, Mathf.Max(0f, InternalConfig.LastChanceTimerChangeNetworkDeltaMinSeconds));
        }

        internal static void Tick()
        {
            if (!s_initialized)
            {
                return;
            }

            var dt = Time.unscaledDeltaTime;
            TickPulse(dt);
            TickFloating(dt);
        }

        internal static void SetVisible(bool visible)
        {
            if (!visible)
            {
                ResetVisualState();
            }
        }

        internal static void Reset()
        {
            s_initialized = false;
            s_timerLabel = null;
            s_timerRect = null;
            s_floatingLabel = null;
            s_floatingRect = null;
            s_textProperty = null;
            s_colorProperty = null;
            s_alignmentProperty = null;
            s_fontSizeProperty = null;
            s_autoSizeProperty = null;
            s_wordWrapProperty = null;
            s_richTextProperty = null;
            s_centerAlignment = null;
            s_timerFontSize = 0f;
            s_baseTimerText = string.Empty;
            s_lastRenderedTimerText = string.Empty;
            s_timerBaseScale = Vector3.one;
            s_pulseRemainingSeconds = 0f;
            s_floatingRemainingSeconds = 0f;
            s_floatingDropDistance = 0f;
            s_floatingBaseColor = Color.white;
            s_pulseColor = Color.white;
        }

        private static void TriggerDeltaEffect(float deltaSeconds, float minAbsSeconds)
        {
            if (!s_initialized || Mathf.Abs(deltaSeconds) < minAbsSeconds)
            {
                return;
            }

            s_pulseRemainingSeconds = PulseDurationSeconds;
            s_pulseColor = deltaSeconds > 0f ? PositiveDeltaColor : NegativeDeltaColor;
            s_floatingRemainingSeconds = Mathf.Max(0.05f, InternalConfig.LastChanceTimerChangeFloatingDurationSeconds);
            s_floatingDropDistance = s_timerFontSize * Mathf.Max(0.1f, InternalConfig.LastChanceTimerChangeFloatingDropFontMultiplier);
            s_floatingBaseColor = s_pulseColor;

            if (s_floatingRect != null)
            {
                s_floatingRect.anchoredPosition = Vector2.zero;
                s_floatingRect.localScale = Vector3.one;
                // Keep delta text above other timer children while staying in the same HUD/menu visibility gate.
                s_floatingRect.SetAsLastSibling();
            }

            if (s_floatingLabel != null)
            {
                var rounded = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(deltaSeconds)));
                var sign = deltaSeconds >= 0f ? "+" : "-";
                s_textProperty?.SetValue(s_floatingLabel, sign + rounded.ToString());
                s_colorProperty?.SetValue(s_floatingLabel, s_floatingBaseColor);
                if (s_floatingLabel is Behaviour floatingBehaviour)
                {
                    floatingBehaviour.enabled = true;
                }
                s_floatingLabel.gameObject.SetActive(true);
            }
        }

        private static void TickPulse(float dt)
        {
            if (s_timerRect == null)
            {
                return;
            }

            if (s_pulseRemainingSeconds <= 0f)
            {
                s_timerRect.localScale = s_timerBaseScale;
                RenderTimerText(s_baseTimerText);
                return;
            }

            s_pulseRemainingSeconds = Mathf.Max(0f, s_pulseRemainingSeconds - dt);
            var pulseDuration = Mathf.Max(0.05f, InternalConfig.LastChanceTimerChangePulseDurationSeconds);
            var pulseBoost = Mathf.Max(0.01f, InternalConfig.LastChanceTimerChangePulseScaleBoost);
            var progress = 1f - (s_pulseRemainingSeconds / pulseDuration);
            var scaleFactor = 1f + Mathf.Sin(progress * Mathf.PI) * pulseBoost;
            s_timerRect.localScale = s_timerBaseScale * scaleFactor;

            if (scaleFactor > 1.02f)
            {
                RenderTimerText(ReplaceFirstColorTag(s_baseTimerText, s_pulseColor));
            }
            else
            {
                RenderTimerText(s_baseTimerText);
            }
        }

        private static void TickFloating(float dt)
        {
            if (s_floatingLabel == null || s_floatingRect == null)
            {
                return;
            }

            if (s_floatingRemainingSeconds <= 0f)
            {
                if (s_floatingLabel is Behaviour floatingBehaviour)
                {
                    floatingBehaviour.enabled = false;
                }
                s_floatingLabel.gameObject.SetActive(false);
                return;
            }

            s_floatingRemainingSeconds = Mathf.Max(0f, s_floatingRemainingSeconds - dt);
            var duration = Mathf.Max(0.05f, InternalConfig.LastChanceTimerChangeFloatingDurationSeconds);
            var progress = 1f - (s_floatingRemainingSeconds / duration);
            var y = -Mathf.Lerp(0f, s_floatingDropDistance, progress);
            s_floatingRect.anchoredPosition = new Vector2(0f, y);

            var color = s_floatingBaseColor;
            color.a = 1f - progress;
            s_colorProperty?.SetValue(s_floatingLabel, color);
        }

        private static void ResetVisualState()
        {
            s_pulseRemainingSeconds = 0f;
            s_floatingRemainingSeconds = 0f;

            if (s_timerRect != null)
            {
                s_timerRect.localScale = s_timerBaseScale;
            }

            if (s_floatingLabel != null)
            {
                if (s_floatingLabel is Behaviour floatingBehaviour)
                {
                    floatingBehaviour.enabled = false;
                }
                s_floatingLabel.gameObject.SetActive(false);
            }

            if (s_floatingRect != null)
            {
                s_floatingRect.anchoredPosition = Vector2.zero;
            }

            RenderTimerText(s_baseTimerText);
        }

        private static void EnsureFloatingLabel(Type? labelType)
        {
            if (s_timerRect == null || s_floatingLabel != null || labelType == null)
            {
                return;
            }

            var floatingGo = new GameObject("LastChanceTimerDelta", typeof(RectTransform));
            var floatingParent = s_timerRect.parent as RectTransform;
            floatingGo.transform.SetParent(floatingParent != null ? floatingParent : s_timerRect, false);
            s_floatingRect = floatingGo.GetComponent<RectTransform>();
            s_floatingRect.anchorMin = new Vector2(0.5f, 0.5f);
            s_floatingRect.anchorMax = new Vector2(0.5f, 0.5f);
            s_floatingRect.pivot = new Vector2(0.5f, 0.5f);
            s_floatingRect.anchoredPosition = Vector2.zero;
            s_floatingRect.sizeDelta = new Vector2(240f, 40f);
            s_floatingRect.SetAsLastSibling();

            s_floatingLabel = floatingGo.AddComponent(labelType);
            if (s_alignmentProperty != null && s_centerAlignment != null)
            {
                s_alignmentProperty.SetValue(s_floatingLabel, s_centerAlignment);
            }
            var floatingFontSize = s_timerFontSize * Mathf.Max(1f, InternalConfig.LastChanceTimerChangeFloatingFontSizeMultiplier);
            s_fontSizeProperty?.SetValue(s_floatingLabel, floatingFontSize);
            s_autoSizeProperty?.SetValue(s_floatingLabel, false);
            s_wordWrapProperty?.SetValue(s_floatingLabel, false);
            s_richTextProperty?.SetValue(s_floatingLabel, false);
            s_colorProperty?.SetValue(s_floatingLabel, Color.white);

            if (s_floatingLabel is Behaviour floatingBehaviour)
            {
                floatingBehaviour.enabled = false;
            }
            s_floatingLabel.gameObject.SetActive(false);
        }

        private static void RenderTimerText(string text)
        {
            if (s_timerLabel == null || s_textProperty == null)
            {
                return;
            }

            if (string.Equals(s_lastRenderedTimerText, text, StringComparison.Ordinal))
            {
                return;
            }

            s_lastRenderedTimerText = text;
            s_textProperty.SetValue(s_timerLabel, text);
        }

        private static string ReplaceFirstColorTag(string text, Color color)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            var replacement = $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>";
            var match = ColorTagRegex.Match(text);
            if (!match.Success)
            {
                return text;
            }

            return text.Substring(0, match.Index) + replacement + text.Substring(match.Index + match.Length);
        }

        private static float PulseDurationSeconds => Mathf.Max(0.05f, InternalConfig.LastChanceTimerChangePulseDurationSeconds);
    }
}

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;

namespace DHHFLastChanceMode.Modules.Config
{
    internal static class ConfigManager
    {
        private struct RangeF { public float Min, Max; }
        private struct RangeI { public int Min, Max; }
        private sealed class HostControlledAccessor
        {
            public HostControlledAccessor(Type valueType, Func<object?> getter, Action<object?> setter)
            {
                ValueType = valueType;
                Getter = getter;
                Setter = setter;
            }

            public Type ValueType { get; }
            public Func<object?> Getter { get; }
            public Action<object?> Setter { get; }
        }

        private static bool s_initialized;
        private static readonly char[] ColorSeparators = { ',', ';' };
        private static readonly Dictionary<string, RangeF> s_floatRanges = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, RangeI> s_intRanges = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, HashSet<string>> s_stringOptions = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> s_stringDefaults = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, HostControlledAccessor> s_hostControlledAccessors = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, ConfigEntryBase> s_hostControlledEntries = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> s_hostRuntimeOverrides = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> s_localHostControlledBaseline = new(StringComparer.Ordinal);
        private static readonly HashSet<string> s_suppressHostControlledEntryChange = new(StringComparer.Ordinal);
        private static int s_localBaselineRestoreDepth;

        internal static event Action? HostControlledChanged;

        internal static void Initialize(ConfigFile config)
        {
            if (s_initialized || config == null)
            {
                return;
            }

            s_initialized = true;
            BindConfigEntries(config);
            ConfigMigrationManager.Apply(config);
            CaptureLocalHostControlledBaseline();
        }

        private static void BindConfigEntries(ConfigFile config)
        {
            foreach (var definition in ConfigMetadata.FeatureFlagEntries)
            {
                if (definition is ConfigMetadata.BoolEntryDefinition boolDefinition)
                {
                    var entry = config.Bind(
                        boolDefinition.Section,
                        boolDefinition.Key,
                        (bool)boolDefinition.GetValue(),
                        new ConfigDescription(boolDefinition.Description, new AcceptableValueList<bool>(false, true)));
                    RegisterHostControlledAccessor(boolDefinition);
                    RegisterHostControlledEntry(boolDefinition, entry);
                    ApplyAndWatch(entry, BuildRangeKey(boolDefinition.Section, boolDefinition.Key), value => boolDefinition.Setter(value), boolDefinition.HostControlled);
                    continue;
                }

                if (definition is ConfigMetadata.IntEntryDefinition intDefinition)
                {
                    var entry = config.Bind(
                        intDefinition.Section,
                        intDefinition.Key,
                        (int)intDefinition.GetValue(),
                        new ConfigDescription(intDefinition.Description, new AcceptableValueRange<int>(intDefinition.Min, intDefinition.Max)));
                    RegisterIntRange(BuildRangeKey(intDefinition.Section, intDefinition.Key), intDefinition.Min, intDefinition.Max);
                    RegisterHostControlledAccessor(intDefinition);
                    RegisterHostControlledEntry(intDefinition, entry);
                    ApplyAndWatch(entry, BuildRangeKey(intDefinition.Section, intDefinition.Key), value => intDefinition.Setter(value), intDefinition.HostControlled);
                    continue;
                }

                if (definition is ConfigMetadata.FloatEntryDefinition floatDefinition)
                {
                    var entry = config.Bind(
                        floatDefinition.Section,
                        floatDefinition.Key,
                        (float)floatDefinition.GetValue(),
                        new ConfigDescription(floatDefinition.Description, new AcceptableValueRange<float>(floatDefinition.Min, floatDefinition.Max)));
                    RegisterFloatRange(BuildRangeKey(floatDefinition.Section, floatDefinition.Key), floatDefinition.Min, floatDefinition.Max);
                    RegisterHostControlledAccessor(floatDefinition);
                    RegisterHostControlledEntry(floatDefinition, entry);
                    ApplyAndWatch(entry, BuildRangeKey(floatDefinition.Section, floatDefinition.Key), value => floatDefinition.Setter(value), floatDefinition.HostControlled);
                    continue;
                }

                if (definition is ConfigMetadata.StringEntryDefinition stringDefinition)
                {
                    var defaultValue = (string)stringDefinition.GetValue();
                    var rangeKey = BuildRangeKey(stringDefinition.Section, stringDefinition.Key);
                    RegisterStringOptions(rangeKey, stringDefinition.Options.Count > 0 ? new List<string>(stringDefinition.Options).ToArray() : null, defaultValue);
                    ConfigEntry<string> entry = stringDefinition.Options.Count > 0
                        ? config.Bind(stringDefinition.Section, stringDefinition.Key, defaultValue, new ConfigDescription(stringDefinition.Description, new AcceptableValueList<string>(new List<string>(stringDefinition.Options).ToArray())))
                        : config.Bind(stringDefinition.Section, stringDefinition.Key, defaultValue, stringDefinition.Description);
                    RegisterHostControlledAccessor(stringDefinition);
                    RegisterHostControlledEntry(stringDefinition, entry);
                    ApplyAndWatch(entry, rangeKey, value => stringDefinition.Setter(value), stringDefinition.HostControlled);
                    continue;
                }
            }
        }

        private static void ApplyAndWatch<T>(ConfigEntry<T> entry, string rangeKey, Action<T> setter, bool notifyHostControlled)
        {
            if (entry == null || setter == null)
            {
                return;
            }

            void Update()
            {
                var hostKey = entry.Definition.Key;
                if (notifyHostControlled &&
                    !IsLocalBaselineRestoreInProgress() &&
                    ShouldRejectClientHostControlledWrite(hostKey, out var authoritativeSerialized) &&
                    TryDeserialize(authoritativeSerialized, typeof(T), out var authoritativeObj) &&
                    authoritativeObj is T authoritativeTyped)
                {
                    if (!IsSuppressedHostControlledEntryChange(hostKey))
                    {
                        SetHostControlledEntryValue(hostKey, authoritativeTyped);
                    }

                    setter(authoritativeTyped);
                    return;
                }

                setter(SanitizeValue(entry.Value, rangeKey));
                if (notifyHostControlled)
                {
                    CaptureLocalHostControlledBaselineValue(hostKey);
                    HostControlledChanged?.Invoke();
                }
            }

            Update();
            entry.SettingChanged += (_, _) => Update();
        }

        private static void ApplyAndWatch(ConfigEntry<string> entry, Func<string, Color> parser, Action<Color> setter, bool notifyHostControlled)
        {
            if (entry == null || parser == null || setter == null)
            {
                return;
            }

            setter(parser(entry.Value));
            if (notifyHostControlled)
            {
                HostControlledChanged?.Invoke();
            }
            entry.SettingChanged += (_, _) =>
            {
                if (notifyHostControlled &&
                    !IsLocalBaselineRestoreInProgress() &&
                    ShouldRejectClientHostControlledWrite(entry.Definition.Key, out var authoritativeSerialized))
                {
                    if (!IsSuppressedHostControlledEntryChange(entry.Definition.Key))
                    {
                        SetHostControlledEntryValue(entry.Definition.Key, authoritativeSerialized);
                    }

                    setter(parser(authoritativeSerialized));
                    return;
                }

                setter(parser(entry.Value));
                if (notifyHostControlled)
                {
                    CaptureLocalHostControlledBaselineValue(entry.Definition.Key);
                    HostControlledChanged?.Invoke();
                }
            };
        }

        private static void RegisterHostControlledAccessor(ConfigMetadata.EntryDefinition definition)
        {
            if (!definition.HostControlled)
            {
                return;
            }

            s_hostControlledAccessors[definition.Key] = new HostControlledAccessor(definition.ValueType, definition.GetValue, definition.SetValue);
        }

        private static void RegisterHostControlledEntry(ConfigMetadata.EntryDefinition definition, ConfigEntryBase entry)
        {
            if (!definition.HostControlled || entry == null)
            {
                return;
            }

            s_hostControlledEntries[definition.Key] = entry;
        }

        internal static Dictionary<string, string> SnapshotHostControlled()
        {
            var snapshot = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kvp in s_hostControlledAccessors)
            {
                if (s_hostRuntimeOverrides.TryGetValue(kvp.Key, out var overrideValue))
                {
                    snapshot[kvp.Key] = overrideValue;
                    continue;
                }

                snapshot[kvp.Key] = SerializeValue(kvp.Value.Getter(), kvp.Value.ValueType);
            }

            return snapshot;
        }

        internal static Dictionary<string, string> SnapshotHostControlledKeys(IEnumerable<string> keys)
        {
            var snapshot = new Dictionary<string, string>(StringComparer.Ordinal);
            if (keys == null)
            {
                return snapshot;
            }

            foreach (var key in keys)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (!s_hostControlledAccessors.TryGetValue(key, out var accessor))
                {
                    continue;
                }

                if (s_hostRuntimeOverrides.TryGetValue(key, out var overrideValue))
                {
                    snapshot[key] = overrideValue;
                    continue;
                }

                snapshot[key] = SerializeValue(accessor.Getter(), accessor.ValueType);
            }

            return snapshot;
        }

        internal static void SetHostRuntimeOverride(string key, string serializedValue)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            var normalized = key.Trim();
            if (!s_hostControlledAccessors.ContainsKey(normalized))
            {
                return;
            }

            var value = serializedValue ?? string.Empty;
            if (s_hostRuntimeOverrides.TryGetValue(normalized, out var current) &&
                string.Equals(current, value, StringComparison.Ordinal))
            {
                return;
            }

            s_hostRuntimeOverrides[normalized] = value;
            HostControlledChanged?.Invoke();
        }

        internal static void ClearHostRuntimeOverride(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            var normalized = key.Trim();
            if (!s_hostRuntimeOverrides.Remove(normalized))
            {
                return;
            }

            HostControlledChanged?.Invoke();
        }

        internal static void ApplyHostSnapshot(Dictionary<string, string> snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            var changed = false;
            // Snapshot values can come from local baseline dictionaries that are updated by
            // SettingChanged callbacks while we apply them. Iterate over a stable copy.
            var entries = new List<KeyValuePair<string, string>>(snapshot);
            foreach (var kvp in entries)
            {
                if (!s_hostControlledAccessors.TryGetValue(kvp.Key, out var accessor))
                {
                    continue;
                }

                var parsed = DeserializeValue(kvp.Value, accessor.ValueType);
                if (parsed != null)
                {
                    var current = accessor.Getter();
                    if (current == null || !current.Equals(parsed))
                    {
                        changed = true;
                    }
                    accessor.Setter(parsed);
                }

                SetHostControlledEntryValue(kvp.Key, kvp.Value);
            }

            if (changed)
            {
                HostControlledChanged?.Invoke();
            }
        }

        internal static void RestoreLocalHostControlledBaseline()
        {
            if (s_localHostControlledBaseline.Count == 0)
            {
                return;
            }

            s_localBaselineRestoreDepth++;
            try
            {
                ApplyHostSnapshot(s_localHostControlledBaseline);
            }
            finally
            {
                s_localBaselineRestoreDepth = Math.Max(0, s_localBaselineRestoreDepth - 1);
            }
        }

        private static string SerializeValue(object? value, Type fieldType)
        {
            if (fieldType == typeof(bool))
            {
                return ((bool)(value ?? false)).ToString(CultureInfo.InvariantCulture);
            }

            if (fieldType == typeof(int))
            {
                return ((int)(value ?? 0)).ToString(CultureInfo.InvariantCulture);
            }

            if (fieldType == typeof(float))
            {
                return ((float)(value ?? 0f)).ToString(CultureInfo.InvariantCulture);
            }

            if (fieldType == typeof(string))
            {
                return value as string ?? string.Empty;
            }

            if (fieldType == typeof(Color))
            {
                return ColorToString((Color)(value ?? Color.black));
            }

            return value?.ToString() ?? string.Empty;
        }

        private static object? DeserializeValue(string value, Type fieldType)
        {
            if (fieldType == typeof(bool))
            {
                return bool.TryParse(value, out var b) && b;
            }

            if (fieldType == typeof(int))
            {
                return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : 0;
            }

            if (fieldType == typeof(float))
            {
                return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f;
            }

            if (fieldType == typeof(string))
            {
                return value ?? string.Empty;
            }

            if (fieldType == typeof(Color))
            {
                return ColorFromString(value ?? string.Empty);
            }

            return null;
        }

        private static bool TryDeserialize(string value, Type targetType, out object? parsed)
        {
            parsed = DeserializeValue(value, targetType);
            if (parsed != null)
            {
                return true;
            }

            if (targetType == typeof(string))
            {
                parsed = value ?? string.Empty;
                return true;
            }

            return false;
        }

        private static string ColorToString(Color input)
        {
            return string.Join(",",
                input.r.ToString(CultureInfo.InvariantCulture),
                input.g.ToString(CultureInfo.InvariantCulture),
                input.b.ToString(CultureInfo.InvariantCulture),
                input.a.ToString(CultureInfo.InvariantCulture));
        }

        private static Color ColorFromString(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return Color.black;
            }

            var segments = input.Split(ColorSeparators, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return Color.black;
            }

            float r = 0f, g = 0f, b = 0f, a = 1f;
            TryParseComponent(segments, 0, ref r);
            TryParseComponent(segments, 1, ref g);
            TryParseComponent(segments, 2, ref b);
            TryParseComponent(segments, 3, ref a);

            return new Color(r, g, b, a);
        }

        private static void TryParseComponent(string[] segments, int index, ref float slot)
        {
            if (index >= segments.Length)
            {
                return;
            }

            var trimmed = segments[index].Trim();
            if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                slot = parsed;
            }
        }

        private static T SanitizeValue<T>(T value, string key)
        {
            if (value is float f && s_floatRanges.TryGetValue(key, out var floatRange))
            {
                var clamped = Math.Min(floatRange.Max, Math.Max(floatRange.Min, f));
                return (T)(object)clamped;
            }

            if (value is int i && s_intRanges.TryGetValue(key, out var intRange))
            {
                var clamped = Math.Min(intRange.Max, Math.Max(intRange.Min, i));
                return (T)(object)clamped;
            }

            if (value is string s &&
                s_stringOptions.TryGetValue(key, out var allowed) &&
                s_stringDefaults.TryGetValue(key, out var fallback) &&
                allowed.Count > 0 &&
                !allowed.Contains(s))
            {
                return (T)(object)fallback;
            }

            return value;
        }


        private static void RegisterStringOptions(string key, string[]? options, string defaultValue)
        {
            if (options == null || options.Length == 0)
            {
                s_stringOptions.Remove(key);
                s_stringDefaults.Remove(key);
                return;
            }

            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string firstAllowed = string.Empty;
            var hasFirstAllowed = false;
            foreach (var option in options)
            {
                if (string.IsNullOrWhiteSpace(option))
                {
                    continue;
                }

                if (!hasFirstAllowed)
                {
                    firstAllowed = option;
                    hasFirstAllowed = true;
                }

                allowed.Add(option);
            }

            if (allowed.Count == 0)
            {
                s_stringOptions.Remove(key);
                s_stringDefaults.Remove(key);
                return;
            }

            s_stringOptions[key] = allowed;
            s_stringDefaults[key] = allowed.Contains(defaultValue) ? defaultValue : firstAllowed;
        }


        private static void RegisterFloatRange(string key, float min, float max)
        {
            s_floatRanges[key] = new RangeF
            {
                Min = min,
                Max = max
            };
        }

        private static void RegisterIntRange(string key, int min, int max)
        {
            s_intRanges[key] = new RangeI
            {
                Min = min,
                Max = max
            };
        }


        private static string BuildRangeKey(string section, string key)
        {
            return $"{section}:{key}";
        }

        private static void CaptureLocalHostControlledBaseline()
        {
            s_localHostControlledBaseline.Clear();
            var snapshot = SnapshotHostControlled();
            foreach (var kvp in snapshot)
            {
                s_localHostControlledBaseline[kvp.Key] = kvp.Value;
            }
        }

        private static void CaptureLocalHostControlledBaselineValue(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            if (!s_hostControlledAccessors.TryGetValue(key, out var accessor))
            {
                return;
            }

            s_localHostControlledBaseline[key] = SerializeValue(accessor.Getter(), accessor.ValueType);
        }

        private static bool ShouldRejectClientHostControlledWrite(string key, out string authoritativeSerialized)
        {
            authoritativeSerialized = string.Empty;
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            // During plugin/config bootstrap SemiFunc internals may not be ready yet.
            // Fail open here and wait for runtime snapshots.
            if (!TryGetClientInRoomState(out var shouldRejectClientWrite) || !shouldRejectClientWrite)
            {
                return false;
            }

            if (!s_hostControlledAccessors.TryGetValue(key, out var accessor))
            {
                return false;
            }

            authoritativeSerialized = SerializeValue(accessor.Getter(), accessor.ValueType);
            return true;
        }

        private static bool TryGetClientInRoomState(out bool shouldRejectClientWrite)
        {
            shouldRejectClientWrite = false;
            try
            {
                if (!SemiFunc.IsMultiplayer())
                {
                    return true;
                }

                shouldRejectClientWrite = !SemiFunc.IsMasterClientOrSingleplayer();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsSuppressedHostControlledEntryChange(string key)
        {
            return !string.IsNullOrWhiteSpace(key) && s_suppressHostControlledEntryChange.Contains(key);
        }

        private static bool IsLocalBaselineRestoreInProgress()
        {
            return s_localBaselineRestoreDepth > 0;
        }

        private static void SetHostControlledEntryValue(string key, object value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            if (!s_hostControlledEntries.TryGetValue(key, out var entry) || entry == null)
            {
                return;
            }

            var valueType = value?.GetType() ?? typeof(string);
            var targetType = GetEntrySettingType(entry) ?? valueType;
            if (!TryConvertForEntry(value, targetType, out var converted))
            {
                return;
            }

            s_suppressHostControlledEntryChange.Add(key);
            try
            {
                entry.BoxedValue = converted;
            }
            finally
            {
                s_suppressHostControlledEntryChange.Remove(key);
            }
        }

        private static Type? GetEntrySettingType(ConfigEntryBase entry)
        {
            return entry?.SettingType ?? entry?.BoxedValue?.GetType();
        }

        private static bool TryConvertForEntry(object? value, Type targetType, out object converted)
        {
            converted = value ?? string.Empty;
            if (value != null && targetType.IsInstanceOfType(value))
            {
                return true;
            }

            var text = value as string ?? value?.ToString() ?? string.Empty;
            if (TryDeserialize(text, targetType, out var parsed) && parsed != null)
            {
                converted = parsed;
                return true;
            }

            try
            {
                converted = Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

    }
}

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using DHHFLastChanceMode.Modules.Config;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace DHHFLastChanceMode.Modules.Utilities
{
    internal static class BundleAssetLoader
    {
        private static readonly ManualLogSource Log = Logger.CreateLogSource("DHHFLastChanceMode.BundleAssetLoader");
        private static AssetBundle? s_bundle;
        private static Sprite[]? s_allSprites;
        private static AudioClip[]? s_allAudioClips;
        private static float s_lastLoadAttemptAt = -999f;
        private const float RetrySeconds = 3f;

        internal static string GetPluginDirectory()
        {
            try
            {
                var codeBase = typeof(DHHFLastChanceMode.Plugin).Assembly.Location;
                if (!string.IsNullOrWhiteSpace(codeBase))
                {
                    var dir = Path.GetDirectoryName(codeBase);
                    if (!string.IsNullOrWhiteSpace(dir))
                    {
                        return dir;
                    }
                }
            }
            catch
            {
                // Ignore and use fallback path.
            }

            return Paths.PluginPath;
        }

        private static void EnsureBundleLoaded()
        {
            if (s_bundle != null)
            {
                return;
            }

            var now = Time.unscaledTime;
            if (s_lastLoadAttemptAt > 0f && (now - s_lastLoadAttemptAt) < RetrySeconds)
            {
                return;
            }
            s_lastLoadAttemptAt = now;

            var candidates = BuildBundleCandidates();
            for (var i = 0; i < candidates.Count; i++)
            {
                var path = candidates[i];
                if (!File.Exists(path))
                {
                    continue;
                }

                try
                {
                    s_bundle = AssetBundle.LoadFromFile(path);
                    if (s_bundle != null)
                    {
                        if (FeatureFlags.DebugLogging && LogLimiter.ShouldLog("LastChance.Bundle.Loaded", 120))
                        {
                            Log.LogDebug($"[LastChance] Loaded asset bundle from: {path}");
                        }
                        return;
                    }
                }
                catch
                {
                    // Keep bundle null and fallback to no-asset behavior.
                }
            }
        }

        private static List<string> BuildBundleCandidates()
        {
            var roots = new List<string>();
            var pluginDir = GetPluginDirectory();
            if (!string.IsNullOrWhiteSpace(pluginDir))
            {
                roots.Add(pluginDir);
            }

            if (!string.IsNullOrWhiteSpace(Paths.PluginPath))
            {
                roots.Add(Paths.PluginPath);
                roots.Add(Path.Combine(Paths.PluginPath, "AdrenSnyder-DHHFLastChanceMode"));
                roots.Add(Path.Combine(Paths.PluginPath, "DHHFLastChanceMode"));
            }

            var fileNames = new[]
            {
                "DHHFLastChanceMode",
                "dhhflastchancemode",
                "DHHFLastChanceMode.bundle",
                "dhhflastchancemode.bundle"
            };

            var candidates = new List<string>();
            for (var i = 0; i < roots.Count; i++)
            {
                var root = roots[i];
                if (string.IsNullOrWhiteSpace(root))
                {
                    continue;
                }

                for (var j = 0; j < fileNames.Length; j++)
                {
                    candidates.Add(Path.Combine(root, fileNames[j]));
                    candidates.Add(Path.Combine(root, "Assets", fileNames[j]));
                }
            }

            // Keep order stable and remove duplicates.
            return candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        internal static bool TryLoadSprite(string fileName, out Sprite? sprite, out string resolvedPath)
        {
            sprite = null;
            resolvedPath = string.Empty;
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            EnsureBundleLoaded();
            if (s_bundle == null)
            {
                return false;
            }

            var name = Path.GetFileNameWithoutExtension(fileName);
            var candidates = new[] { name, fileName, "Assets/" + fileName, "assets/" + fileName };
            for (var i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                try
                {
                    sprite = s_bundle.LoadAsset<Sprite>(candidate);
                    if (sprite != null)
                    {
                        resolvedPath = candidate;
                        if (FeatureFlags.DebugLogging && LogLimiter.ShouldLog("LastChance.Bundle.SpriteHit." + fileName, 60))
                        {
                            Log.LogDebug($"[LastChance] Sprite '{fileName}' resolved as Sprite: {resolvedPath}");
                        }
                        return true;
                    }

                    var texture = s_bundle.LoadAsset<Texture2D>(candidate);
                    if (texture != null)
                    {
                        sprite = Sprite.Create(
                            texture,
                            new Rect(0f, 0f, texture.width, texture.height),
                            new Vector2(0.5f, 0.5f),
                            100f);
                        sprite.name = Path.GetFileNameWithoutExtension(fileName);
                        resolvedPath = candidate;
                        if (FeatureFlags.DebugLogging && LogLimiter.ShouldLog("LastChance.Bundle.SpriteHitTex." + fileName, 60))
                        {
                            Log.LogDebug($"[LastChance] Sprite '{fileName}' resolved from Texture2D: {resolvedPath}");
                        }
                        return true;
                    }
                }
                catch
                {
                }
            }

            try
            {
                var all = s_bundle.GetAllAssetNames();
                for (var i = 0; i < all.Length; i++)
                {
                    var assetName = all[i];
                    if (assetName.EndsWith("/" + fileName, StringComparison.OrdinalIgnoreCase) ||
                        assetName.EndsWith("/" + name, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(assetName, fileName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(assetName, name, StringComparison.OrdinalIgnoreCase))
                    {
                            sprite = s_bundle.LoadAsset<Sprite>(assetName);
                            if (sprite != null)
                            {
                                resolvedPath = assetName;
                                if (FeatureFlags.DebugLogging && LogLimiter.ShouldLog("LastChance.Bundle.SpriteHitAsset." + fileName, 60))
                                {
                                    Log.LogDebug($"[LastChance] Sprite '{fileName}' resolved via asset name: {resolvedPath}");
                                }
                                return true;
                            }

                        var texture = s_bundle.LoadAsset<Texture2D>(assetName);
                        if (texture != null)
                        {
                            sprite = Sprite.Create(
                                texture,
                                new Rect(0f, 0f, texture.width, texture.height),
                                new Vector2(0.5f, 0.5f),
                                100f);
                            sprite.name = Path.GetFileNameWithoutExtension(fileName);
                            resolvedPath = assetName;
                            if (FeatureFlags.DebugLogging && LogLimiter.ShouldLog("LastChance.Bundle.SpriteHitAssetTex." + fileName, 60))
                            {
                                Log.LogDebug($"[LastChance] Sprite '{fileName}' resolved via asset Texture2D: {resolvedPath}");
                            }
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // Ignore bundle enumeration failures.
            }

            var targetName = Path.GetFileNameWithoutExtension(fileName);
            foreach (var loadedSprite in GetAllSprites())
            {
                if (loadedSprite == null)
                {
                    continue;
                }

                if (!string.Equals(loadedSprite.name, targetName, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(loadedSprite.name, fileName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                sprite = loadedSprite;
                resolvedPath = loadedSprite.name;
                if (FeatureFlags.DebugLogging && LogLimiter.ShouldLog("LastChance.Bundle.SpriteHitByName." + fileName, 60))
                {
                    Log.LogDebug($"[LastChance] Sprite '{fileName}' resolved by object name: {resolvedPath}");
                }
                return true;
            }

            if (FeatureFlags.DebugLogging && LogLimiter.ShouldLog("LastChance.Bundle.SpriteMiss." + fileName, 60))
            {
                Log.LogWarning($"[LastChance] Sprite '{fileName}' not found in bundle.");
            }
            return false;
        }

        internal static bool TryLoadAudioClip(string fileName, out AudioClip? clip, out string resolvedPath)
        {
            clip = null;
            resolvedPath = string.Empty;
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            EnsureBundleLoaded();
            if (s_bundle == null)
            {
                return false;
            }

            var name = Path.GetFileNameWithoutExtension(fileName);
            var candidates = new[] { name, fileName, "Assets/" + fileName, "assets/" + fileName };
            for (var i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                try
                {
                    clip = s_bundle.LoadAsset<AudioClip>(candidate);
                    if (clip != null)
                    {
                        resolvedPath = candidate;
                        if (FeatureFlags.DebugLogging && LogLimiter.ShouldLog("LastChance.Bundle.AudioHit." + fileName, 60))
                        {
                            Log.LogDebug($"[LastChance] Audio '{fileName}' resolved as AudioClip: {resolvedPath}");
                        }
                        return true;
                    }
                }
                catch
                {
                }
            }

            try
            {
                var all = s_bundle.GetAllAssetNames();
                for (var i = 0; i < all.Length; i++)
                {
                    var assetName = all[i];
                    if (assetName.EndsWith("/" + fileName, StringComparison.OrdinalIgnoreCase) ||
                        assetName.EndsWith("/" + name, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(assetName, fileName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(assetName, name, StringComparison.OrdinalIgnoreCase))
                    {
                        clip = s_bundle.LoadAsset<AudioClip>(assetName);
                        if (clip != null)
                        {
                            resolvedPath = assetName;
                            if (FeatureFlags.DebugLogging && LogLimiter.ShouldLog("LastChance.Bundle.AudioHitAsset." + fileName, 60))
                            {
                                Log.LogDebug($"[LastChance] Audio '{fileName}' resolved via asset name: {resolvedPath}");
                            }
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // Ignore bundle enumeration failures.
            }

            var targetName = Path.GetFileNameWithoutExtension(fileName);
            foreach (var loadedClip in GetAllAudioClips())
            {
                if (loadedClip == null)
                {
                    continue;
                }

                if (!string.Equals(loadedClip.name, targetName, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(loadedClip.name, fileName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                clip = loadedClip;
                resolvedPath = loadedClip.name;
                if (FeatureFlags.DebugLogging && LogLimiter.ShouldLog("LastChance.Bundle.AudioHitByName." + fileName, 60))
                {
                    Log.LogDebug($"[LastChance] Audio '{fileName}' resolved by object name: {resolvedPath}");
                }
                return true;
            }

            if (FeatureFlags.DebugLogging && LogLimiter.ShouldLog("LastChance.Bundle.AudioMiss." + fileName, 60))
            {
                Log.LogWarning($"[LastChance] Audio '{fileName}' not found in bundle.");
            }
            return false;
        }

        private static Sprite[] GetAllSprites()
        {
            if (s_allSprites != null)
            {
                return s_allSprites;
            }

            if (s_bundle == null)
            {
                s_allSprites = Array.Empty<Sprite>();
                return s_allSprites;
            }

            try
            {
                s_allSprites = s_bundle.LoadAllAssets<Sprite>() ?? Array.Empty<Sprite>();
            }
            catch
            {
                s_allSprites = Array.Empty<Sprite>();
            }

            return s_allSprites;
        }

        private static AudioClip[] GetAllAudioClips()
        {
            if (s_allAudioClips != null)
            {
                return s_allAudioClips;
            }

            if (s_bundle == null)
            {
                s_allAudioClips = Array.Empty<AudioClip>();
                return s_allAudioClips;
            }

            try
            {
                s_allAudioClips = s_bundle.LoadAllAssets<AudioClip>() ?? Array.Empty<AudioClip>();
            }
            catch
            {
                s_allAudioClips = Array.Empty<AudioClip>();
            }

            return s_allAudioClips;
        }
    }
}

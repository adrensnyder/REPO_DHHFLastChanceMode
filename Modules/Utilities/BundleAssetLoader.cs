#nullable enable

using System;
using System.IO;
using BepInEx;
using UnityEngine;

namespace DeathHeadHopperFix.Modules.Utilities
{
    internal static class BundleAssetLoader
    {
        private static AssetBundle? s_bundle;
        private static bool s_loadAttempted;

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
            if (s_loadAttempted)
            {
                return;
            }

            s_loadAttempted = true;

            var baseDir = GetPluginDirectory();
            var candidates = new[]
            {
                Path.Combine(baseDir, "Assets", "DHHFLastChanceMode"),
                Path.Combine(baseDir, "Assets", "dhhflastchancemode"),
                Path.Combine(baseDir, "DHHFLastChanceMode")
            };

            for (var i = 0; i < candidates.Length; i++)
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
                        return;
                    }
                }
                catch
                {
                    // Keep bundle null and fallback to no-asset behavior.
                }
            }
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
                        return true;
                    }
                }
                catch
                {
                }
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
                        return true;
                    }
                }
                catch
                {
                }
            }

            return false;
        }
    }
}

using System;
using System.Collections.Generic;

namespace DHHFLastChanceMode.Modules.Utilities
{
    internal static class LogLimiter
    {
        public const int DefaultFrameInterval = 240;

        private static readonly Dictionary<string, int> s_lastFrameByKey = new(StringComparer.Ordinal);

        public static bool ShouldLog(string key, int frameInterval = DefaultFrameInterval)
        {
            if (string.IsNullOrEmpty(key) || frameInterval <= 0)
            {
                return true;
            }

            var current = UnityEngine.Time.frameCount;
            if (s_lastFrameByKey.TryGetValue(key, out var last) && current - last < frameInterval)
            {
                return false;
            }

            s_lastFrameByKey[key] = current;
            return true;
        }

        public static void Reset(string key)
        {
            if (!string.IsNullOrEmpty(key))
            {
                s_lastFrameByKey.Remove(key);
            }
        }

        public static void Clear()
        {
            s_lastFrameByKey.Clear();
        }
    }
}

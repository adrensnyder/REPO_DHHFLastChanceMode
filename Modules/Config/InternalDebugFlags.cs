#nullable enable

namespace DHHFLastChanceMode.Modules.Config
{
    // Internal-only debug switches for LastChance pipelines.
    internal static class InternalDebugFlags
    {
        public static bool DebugLastChanceHiddenCarryFlow = false;
        public static bool DebugLastChanceCeilingEyeFlow = false;
        public static bool DebugLastChanceHeartHuggerFlow = false;
        public static bool DebugLastChanceTricycleFlow = false;
        public static bool DebugLastChanceSpinnyFlow = false;
        public static bool DebugLastChanceSpinnyVerbose = false;
        public static bool DebugLastChanceThinManFlow = false;
        public static bool DebugLastChanceEyesFlow = false;
        public static bool DebugLastChanceHeadmanSlowMouthFlow = true;
    }
}

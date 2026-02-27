#nullable enable

using System;

namespace DHHFLastChanceMode.Modules.Config
{
    [AttributeUsage(AttributeTargets.Field)]
    internal sealed class FeatureConfigEntryAttribute : Attribute
    {
        public FeatureConfigEntryAttribute(string section, string description)
        {
            Section = section;
            Description = description;
        }

        public string Section { get; }
        public string Description { get; }
        public string Key { get; set; } = string.Empty;
        public float Min { get; set; } = float.NaN;
        public float Max { get; set; } = float.NaN;
        public string[]? Options { get; set; }
        public bool HostControlled { get; set; } = true;
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    internal sealed class FeatureConfigAliasAttribute : Attribute
    {
        public FeatureConfigAliasAttribute(string oldSection, string oldKey)
        {
            OldSection = oldSection ?? string.Empty;
            OldKey = oldKey ?? string.Empty;
        }

        public string OldSection { get; }
        public string OldKey { get; }
    }
}

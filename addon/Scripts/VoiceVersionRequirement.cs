using System;

namespace TanukiBCL.Roles;

public static class VoiceVersionRequirement
{
    public const string Minimum = "3.2.47";
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 64) return null;
        string core = value.Split('-')[0];
        return Version.TryParse(core, out var version) && version.Build >= 0 && version.Revision < 0
            ? version.ToString() : null;
    }
    public static bool IsSupported(string? value)
    {
        string? core = Normalize(value);
        return core != null && Version.Parse(core) >= Version.Parse(Minimum);
    }
}

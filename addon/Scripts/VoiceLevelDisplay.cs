using System;
using System.Globalization;

namespace TanukiBCL.Roles;

// UI scale only. Retain the existing decibel thresholds and saved config keys.
public static class VoiceLevelDisplay
{
    public static string Format(float db) => Math.Round(Math.Clamp((db + 60f) / 60f, 0f, 1f) * 100f)
        .ToString(CultureInfo.InvariantCulture);
}

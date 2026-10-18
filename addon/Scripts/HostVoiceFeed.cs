using System;
using System.Globalization;

namespace TanukiBCL.Roles;

public sealed class HostVoiceFeed
{
    private readonly long[] sequence = new long[256];
    private readonly double[] volume = new double[256];
    private readonly bool[] active = new bool[256];
    private readonly long[] received = new long[256];
    private readonly string?[] versions = new string?[256];
    public string? VersionFor(byte player, long now) => now >= received[player] && now - received[player] <= 500 ? versions[player] : null;
    public void Store(byte player, double rms, bool enabled, long now, string? version = null)
    {
        if (!double.IsFinite(rms) || rms < 0 || rms > 1) return;
        versions[player] = VoiceVersionRequirement.Normalize(version);
        sequence[player]++; volume[player] = rms; active[player] = enabled; received[player] = now;
    }
    public bool TryRead(byte player, ref long consumed, out double rms, out bool enabled, long now)
    {
        rms = volume[player]; enabled = active[player];
        if (now - received[player] > 500 || now < received[player] || consumed >= sequence[player] || sequence[player] == 0) return false;
        consumed = sequence[player]; return true;
    }
    public static bool Parse(string text, string token, long now, out byte player, out long sequence, out double rms, out bool active)
    {
        player = 0; sequence = 0; rms = 0; active = false;
        var p = text.Split('|');
        if ((p.Length != 7 && p.Length != 8) || p[0] != "TBCLH1" || p[1] != token
            || !byte.TryParse(p[2], out player) || !long.TryParse(p[3], out sequence) || sequence <= 0
            || !long.TryParse(p[4], out long time) || now - time > 500 || time - now > 100
            || !double.TryParse(p[5], NumberStyles.Float, CultureInfo.InvariantCulture, out rms)
            || !double.IsFinite(rms) || rms < 0 || rms > 1 || (p[6] != "0" && p[6] != "1")) return false;
        active = p[6] == "1"; return true;
    }
}

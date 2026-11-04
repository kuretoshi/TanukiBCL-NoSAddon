namespace TanukiBCL.Roles;

// The new local transport feeds the existing independent voice consumers.
public static class AddonLinkVoice
{
    private static HostVoiceFeed feed = new();
    public static int Epoch { get; private set; }
    public static void Reset() { feed = new HostVoiceFeed(); Epoch++; }
    public static void Store(byte player, double rms, bool active, long now, string? version) => feed.Store(player, rms, active, now, version);
    public static bool TryRead(byte player, ref long consumed, out double rms, out bool active, long now) =>
        feed.TryRead(player, ref consumed, out rms, out active, now);
    public static string? VersionFor(byte player, long now) => feed.VersionFor(player, now);
}

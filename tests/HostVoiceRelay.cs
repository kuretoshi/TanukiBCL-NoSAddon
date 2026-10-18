using System;
namespace TanukiBCL.Roles;
public static class HostVoiceRelay
{
 public static readonly HostVoiceFeed Feed = new();
 public static string? VersionFor(byte player) => Feed.VersionFor(player, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
 public static bool TryRead(byte player, ref long consumed, out double rms, out bool active)
 => Feed.TryRead(player, ref consumed, out rms, out active, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
}

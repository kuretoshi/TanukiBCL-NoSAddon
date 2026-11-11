using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TanukiBCL.Roles;

public static class WallEarBroadcast
{
    private sealed record State(byte playerId, float x, float y, float range);
    private static readonly Dictionary<byte, State> entries = new();
    private static long lastWrite;
    private static readonly string path = Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA") ??
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TanukiBCL", "NoS", "ears-" + Environment.ProcessId + ".json");
    public static void Set(byte player, float x, float y, float range)
    {
        entries[player] = new State(player, x, y, range); Publish(false);
    }
    public static void Remove(byte player)
    {
        if (entries.Remove(player)) Publish(true);
    }
    private static void Publish(bool immediate)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (!immediate && now - lastWrite < 100) return;
        lastWrite = now;
        string json = JsonSerializer.Serialize(new { protocol = 1, pid = Environment.ProcessId, unixMs = now, players = entries.Values });
        if (!AddonLinkState.Update("ears", json)) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path + ".tmp", json);
            File.Move(path + ".tmp", path, true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

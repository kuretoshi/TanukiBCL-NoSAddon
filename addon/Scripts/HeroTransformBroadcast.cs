using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace TanukiBCL.Roles;

// Every game client publishes its RPC-synchronised transformations for its own BCL.
public static class HeroTransformBroadcast
{
    private static readonly Dictionary<byte, HeroKind> players = new();
    private static long lastWrite;
    private static readonly string path = Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA") ??
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TanukiBCL", "NoS",
        "hero-transforms-" + Environment.ProcessId + ".json");

    public static void Set(byte player, HeroKind kind)
    {
        bool changed = !players.TryGetValue(player, out var previous) || previous != kind;
        players[player] = kind;
        Publish(changed);
    }
    public static void Remove(byte player)
    {
        if (!players.Remove(player)) return;
        Publish(true);
    }
    private static void Publish(bool immediate)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (!immediate && now - lastWrite < 100) return;
        lastWrite = now;
        var entries = players.Select(p => new { playerId = p.Key, kind = p.Value == HeroKind.Super ? "super" : "evil" }).ToArray();
        string json = JsonSerializer.Serialize(new { protocol = 1, pid = Environment.ProcessId, unixMs = now, players = entries });
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

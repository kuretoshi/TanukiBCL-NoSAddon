using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace TanukiBCL.Roles;

public static class BitCharacterBroadcast
{
    private static readonly Dictionary<byte, int> players = new();
    private static long lastWrite;
    public static void Set(byte player, int stage)
    {
        if (stage < 0 || stage > BitCharacterProgress.FinalStage) return;
        bool changed = !players.TryGetValue(player, out int old) || old != stage;
        players[player] = stage;
        Publish(changed);
    }
    public static void Remove(byte player)
    {
        if (players.Remove(player)) Publish(true);
    }
    private static void Publish(bool immediate)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (!immediate && now - lastWrite < 100) return;
        lastWrite = now;
        var rows = players.OrderBy(p => p.Key).Select(p => new { playerId = p.Key, stage = p.Value }).ToArray();
        AddonLinkState.Update("bits", JsonSerializer.Serialize(new { protocol = 1, pid = Environment.ProcessId, unixMs = now, players = rows }));
    }
}

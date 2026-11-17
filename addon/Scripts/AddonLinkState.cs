using System;
using System.Collections.Generic;
using System.Text.Json;

namespace TanukiBCL.Roles;

public static class AddonLinkState
{
    public static readonly string[] Channels = { "heroes", "portable", "megaphones", "dying", "interviews", "jamming", "echoes", "muted", "speech", "ears", "parrots", "bits" };
    private sealed record Entry(JsonElement Players, long Updated);
    private static readonly Dictionary<string, Entry> entries = new();
    public static bool LegacyRequired { get; set; } = true;
    // Existing publisher envelopes remain the source of truth. This avoids
    // maintaining a second role state machine or changing existing Web packets.
    public static bool Update(string channel, string json)
    {
        using var document = JsonDocument.Parse(json);
        var players = document.RootElement.GetProperty("players");
        if (players.ValueKind != JsonValueKind.Array) throw new InvalidOperationException("Invalid addon channel");
        entries[channel] = new Entry(players.Clone(), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        return LegacyRequired;
    }
    public static void Clear(string channel) => entries.Remove(channel);
    public static int FlagsFor(byte player, Dictionary<string, JsonElement>? state = null)
    {
        int flags = 0;
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        state ??= Snapshot(now);
        if (Find(state["muted"], player).HasValue) flags |= 1 << 9;
        var jam = Find(state["jamming"], player);
        if (Boolean(jam, "muted")) flags |= 1 << 10;
        if (Boolean(jam, "lowpass")) flags |= 1 << 11;
        if (jam.HasValue && jam.Value.TryGetProperty("pitch", out var pitch) && pitch.TryGetDouble(out double p) && Math.Abs(p - 1) > .0001) flags |= 1 << 12;
        if (Find(state["megaphones"], player).HasValue) flags |= 1 << 13;
        if (Find(state["dying"], player).HasValue) flags |= 1 << 14;
        if (Find(state["interviews"], player).HasValue) flags |= 1 << 15;
        return flags;
    }
    private static JsonElement? Find(JsonElement rows, byte player)
    {
        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind == JsonValueKind.Number && row.TryGetByte(out byte id) && id == player) return row;
            if (row.ValueKind == JsonValueKind.Object && row.TryGetProperty("playerId", out var key) && key.TryGetByte(out byte pid) && pid == player) return row;
        }
        return null;
    }
    private static bool Boolean(JsonElement? row, string key) => row.HasValue && row.Value.ValueKind == JsonValueKind.Object &&
        row.Value.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.True;
    public static void Reset() { entries.Clear(); LegacyRequired = true; }
    public static Dictionary<string, JsonElement> Snapshot(long now)
    {
        var result = new Dictionary<string, JsonElement>();
        using var empty = JsonDocument.Parse("[]");
        foreach (var channel in Channels)
        {
            int lifetime = channel == "echoes" ? 3000 : 1000;
            result[channel] = entries.TryGetValue(channel, out var entry) && now >= entry.Updated && (channel == "muted" || now - entry.Updated <= lifetime)
                ? entry.Players : empty.RootElement.Clone();
        }
        return result;
    }
}

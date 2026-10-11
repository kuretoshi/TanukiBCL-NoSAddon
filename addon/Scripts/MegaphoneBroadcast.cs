using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace TanukiBCL.Roles;

// Every client publishes the charging states received through the game's RPC.
// BCL never needs to trust another voice peer's claim about their role.
public static class MegaphoneBroadcast
{
    private static readonly Dictionary<byte, (float energy, float range, bool voiceEffect)> players = new();
    private static readonly string path = Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TanukiBCL", "NoS", "megaphone-state.json");
    private static long lastWrite;

    public static void Set(byte id, float energy, float maxRange, bool voiceEffect)
    {
        energy = Math.Clamp(energy, 0f, 1f);
        players[id] = (energy, 2f + energy * (Math.Max(2f, maxRange) - 2f), voiceEffect);
        Publish(false);
    }

    public static void Remove(byte id)
    {
        if (players.Remove(id)) Publish(true);
    }

    private static void Publish(bool immediate)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (!immediate && now - lastWrite < 100) return;
        lastWrite = now;
        var json = new StringBuilder("{\"protocol\":1,\"pid\":").Append(Environment.ProcessId)
            .Append(",\"unixMs\":").Append(now).Append(",\"players\":[");
        bool first = true;
        foreach (var entry in players)
        {
            if (!first) json.Append(',');
            first = false;
            json.Append("{\"playerId\":").Append(entry.Key).Append(",\"energy\":")
                .Append(entry.Value.energy.ToString("R", CultureInfo.InvariantCulture)).Append(",\"range\":")
                .Append(entry.Value.range.ToString("R", CultureInfo.InvariantCulture))
                .Append(",\"voiceEffect\":").Append(entry.Value.voiceEffect ? "true" : "false").Append('}');
        }
        json.Append("]}");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path + ".tmp", json.ToString(), new UTF8Encoding(false));
            File.Move(path + ".tmp", path, true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

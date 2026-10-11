using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace TanukiBCL.Roles;

public static class PortableMegaphoneBroadcast
{
    private static readonly Dictionary<byte, float> players = new();
    private static readonly string path = Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TanukiBCL", "NoS", "portable-megaphone.json");
    private static long lastWrite;
    public static void Set(byte id, float extraRange) { players[id] = Math.Clamp(extraRange, 0, 30); Publish(false); }
    public static void Remove(byte id) { if (players.Remove(id)) Publish(true); }
    private static void Publish(bool immediate)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (!immediate && now - lastWrite < 100) return;
        lastWrite = now;
        var json = new StringBuilder("{\"protocol\":1,\"pid\":").Append(Environment.ProcessId).Append(",\"unixMs\":").Append(now).Append(",\"players\":[");
        bool first = true;
        foreach (var entry in players)
        {
            if (!first) json.Append(','); first = false;
            json.Append("{\"playerId\":").Append(entry.Key).Append(",\"extraRange\":").Append(entry.Value.ToString("R", CultureInfo.InvariantCulture)).Append('}');
        }
        json.Append("]}");
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path + ".tmp", json.ToString(), new UTF8Encoding(false));
            File.Move(path + ".tmp", path, true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

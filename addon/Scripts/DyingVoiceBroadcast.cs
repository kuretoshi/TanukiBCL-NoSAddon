using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace TanukiBCL.Roles;

public static class DyingVoiceBroadcast
{
    private static readonly Dictionary<byte, (long started, float seconds, float range, float x, float y)> entries = new();
    private static long lastWrite;
    private static readonly string path = Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA") ??
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TanukiBCL", "NoS", "dying-voice-" + Environment.ProcessId + ".json");
    public static void Set(byte id, long started, float seconds, float range, float x, float y)
    {
        entries[id] = (started, seconds, range, x, y); Publish(false);
    }
    public static void Remove(byte id)
    {
        if (!entries.Remove(id)) return;
        Publish(true);
        if (entries.Count != 0) return;
        // The empty state already invalidates listeners if file deletion fails.
        try { File.Delete(path); File.Delete(path + ".tmp"); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        lastWrite = 0;
    }
    private static void Publish(bool immediate)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (!immediate && now - lastWrite < 100) return;
        lastWrite = now;
        var json = new StringBuilder("{\"protocol\":1,\"pid\":").Append(Environment.ProcessId)
            .Append(",\"unixMs\":").Append(now).Append(",\"players\":[");
        bool first = true;
        foreach (var pair in entries)
        {
            if (!first) json.Append(','); first = false;
            var e = pair.Value;
            json.Append("{\"playerId\":").Append(pair.Key).Append(",\"started\":").Append(e.started)
                .Append(",\"seconds\":").Append(e.seconds.ToString("R", CultureInfo.InvariantCulture))
                .Append(",\"range\":").Append(e.range.ToString("R", CultureInfo.InvariantCulture))
                .Append(",\"x\":").Append(e.x.ToString("R", CultureInfo.InvariantCulture))
                .Append(",\"y\":").Append(e.y.ToString("R", CultureInfo.InvariantCulture)).Append('}');
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

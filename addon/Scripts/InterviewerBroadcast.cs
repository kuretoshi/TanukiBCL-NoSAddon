using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace TanukiBCL.Roles;

public sealed class InterviewState
{
    public byte playerId { get; set; }
    public long started { get; set; }
    public float seconds { get; set; }
    public long stopped { get; set; }
    public byte[] visible { get; set; } = Array.Empty<byte>();
    public long playStarted { get; set; }
}

public static class InterviewerBroadcast
{
    private static readonly Dictionary<byte, InterviewState> entries = new();
    private static long lastWrite;
    private static readonly string path = Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA") ??
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TanukiBCL", "NoS", "interviews-" + Environment.ProcessId + ".json");
    public static void Set(InterviewState state) { entries[state.playerId] = state; Publish(false); }
    public static void Remove(byte id)
    {
        if (!entries.Remove(id)) return;
        Publish(true);
        if (entries.Count != 0) return;
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
        // byte[] is serialized as Base64 by System.Text.Json; send an integer array.
        var players = entries.Values.Select(e => new { e.playerId, e.started, e.seconds, e.stopped,
            visible = e.visible.Select(id => (int)id).ToArray(), e.playStarted }).ToArray();
        string json = JsonSerializer.Serialize(new { protocol = 1, pid = Environment.ProcessId, unixMs = now, players });
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace TanukiBCL.Roles;

public static class VoiceJammingBroadcast
{
    static VoiceJammingBroadcast() { }
    private sealed record Effect(byte? Target, int Mode, int Seed, long Priority);
    private static readonly Dictionary<byte, Effect> entries = new();
    private static long lastWrite;
    private static readonly string path = Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA") ??
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TanukiBCL", "NoS", "voice-jamming-" + Environment.ProcessId + ".json");

    public static void Set(byte owner, byte? target, int mode, int seed, long priority, byte[] speakers)
    {
        entries[owner] = new Effect(target, mode, seed, priority);
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (now - lastWrite < 100) return;
        lastWrite = now;
        var pitch = entries.OrderByDescending(e => e.Value.Priority).ThenBy(e => e.Key)
            .Select(e => e.Value).FirstOrDefault(e => e.Seed > 0);
        var players = speakers.Distinct().Select(id => new {
            playerId = id,
            muted = entries.Values.Any(e => e.Target == id && e.Mode == 0),
            lowpass = entries.Values.Any(e => e.Target == id && e.Mode == 1),
            pitch = pitch == null ? 1d : VoiceJammingRound.PitchFor(pitch.Seed, id)
        }).ToArray();
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
    public static void Remove(byte owner)
    {
        if (!entries.Remove(owner)) return;
        lastWrite = 0;
        // Invalidate immediately; the other living ability instances republish next frame.
        try { File.Delete(path); File.Delete(path + ".tmp"); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

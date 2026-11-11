using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
namespace TanukiBCL.Roles;

public static class LimitSpeechBroadcast
{
    private static readonly HashSet<byte> muted = new();
    private static readonly string path = Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TanukiBCL", "NoS", "limit-speech-" + Environment.ProcessId + ".json");
    private static long lastWrite;
    public static void Set(byte player, bool mute) { if (mute) muted.Add(player); else muted.Remove(player); Publish(true); }
    public static void Publish(bool immediate = false)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (!immediate && now - lastWrite < 100) return;
        lastWrite = now;
        var json = new StringBuilder("{\"protocol\":1,\"pid\":").Append(Environment.ProcessId).Append(",\"unixMs\":").Append(now).Append(",\"players\":[");
        bool first = true;
        foreach (var id in muted) { if (!first) json.Append(','); first = false; json.Append(id); }
        json.Append("]}");
        if (!AddonLinkState.Update("muted", json.ToString())) return;
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path + ".tmp", json.ToString(), new UTF8Encoding(false));
            File.Move(path + ".tmp", path, true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

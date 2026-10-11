using System;
using System.IO;
using System.Text;
using Nebula.Modules;

namespace TanukiBCL.Roles;

// Shared by Bibiri and deaths while the portable megaphone is active.
// Only the death event is shared. Voice stays in BCL's existing live audio path.
public static class BibiriEcho
{
    private static readonly string path = Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TanukiBCL", "NoS", "bibiri-echo.json");
    private static readonly long[] deaths = new long[256];
    private static readonly RemoteProcess<byte> DeathRpc = new("TanukiBibiriEchoV1", (id, _) => Publish(id));

    public static void Notify(byte id) => DeathRpc.Invoke(id);

    private static void Publish(byte id)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        deaths[id] = now;
        var json = new StringBuilder("{\"protocol\":1,\"pid\":").Append(Environment.ProcessId).Append(",\"players\":[");
        bool first = true;
        for (int i = 0; i < deaths.Length; i++)
        {
            if (now - deaths[i] > 3000) continue;
            if (!first) json.Append(',');
            first = false;
            json.Append("{\"playerId\":").Append(i).Append(",\"unixMs\":").Append(deaths[i]).Append('}');
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

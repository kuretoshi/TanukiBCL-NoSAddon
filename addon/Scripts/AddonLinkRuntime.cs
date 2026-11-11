using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using TanukiBCL.Link;
using Virial;

namespace TanukiBCL.Roles;

// Owns one endpoint per local game session, including lobby microphone checks.
public static class AddonLinkRuntime
{
    private static AddonLinkServer? server;
    private static object? game;
    private static byte owner = 255;
    private static long nextSnapshot, nextDescriptor, revision;
    private static readonly string path = Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA") ??
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TanukiBCL", "NoS", "addon-link-" + Environment.ProcessId + ".json");
    public static int Port => server?.Port ?? 0;
    public static void Update()
    {
        var local = PlayerControl.LocalPlayer;
        var client = AmongUsClient.Instance;
        if (local == null || client == null || client.GameState is not (InnerNet.InnerNetClient.GameStates.Joined or InnerNet.InnerNetClient.GameStates.Started))
        { Close(); return; }
        try { Tick(local.PlayerId); }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Net.Sockets.SocketException)
        { Close(); NebulaAPI.Logging.NebulaLogger("TanukiAddonLink").Error(ex.Message); }
    }
    private static void Tick(byte player)
    {
        if (server == null || owner != player || !ReferenceEquals(game, NebulaAPI.CurrentGame))
        {
            Close(); owner = player; game = NebulaAPI.CurrentGame;
            server = new AddonLinkServer(Environment.ProcessId);
        }
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        AddonLinkState.LegacyRequired = !server.Healthy(now);
        for (int i = 0; i < 64 && server.TryRead(out var command); i++) Dispatch(command, now);
        if (now >= nextSnapshot) Publish(now);
        if (now >= nextDescriptor) WriteDescriptor(now);
    }
    private static void Publish(long now)
    {
        nextSnapshot = now + 100;
        string phase = AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Started ? "lobby" :
            HeroSpeechHost.IsPresent(MeetingHud.Instance) ? "meeting" : "tasks";
        string json = JsonSerializer.Serialize(new { kind = "state", protocol = 1, pid = Environment.ProcessId,
            session = server!.Session, sequence = ++revision, unixMs = now, phase, localPlayer = owner,
            host = AmongUsClient.Instance.AmHost, channels = AddonLinkState.Snapshot(now) });
        server.SetSnapshot(json, revision);
    }
    private static void WriteDescriptor(long now)
    {
        nextDescriptor = now + 1000;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(new { protocol = 1, pid = Environment.ProcessId, port = server!.Port, unixMs = now }));
        File.Move(path + ".tmp", path, true);
    }
    private static void Dispatch(JsonElement command, long now)
    {
        if (!AddonLinkWire.Integer(command, "unixMs", out long timestamp) || now - timestamp > 500 || timestamp > now + 100) return;
        string? kind = AddonLinkWire.Text(command, "kind");
        if (kind == "sample") { Sample(command, now); return; }
        if (kind == "ready") { Ready(command); return; }
        if (kind == "parrot") { Parrot(command); return; }
        if (kind != "word" || !AddonLinkWire.Integer(command, "playerId", out long id) || id < 0 || id > 255 ||
            !AddonLinkWire.Integer(command, "request", out long request) || request <= 0 || request > int.MaxValue) return;
        HeroSpeechHost.AcceptUnifiedWord((byte)id, (int)request, AddonLinkWire.Text(command, "speechKind"), AddonLinkWire.Text(command, "word"));
    }
    private static void Sample(JsonElement command, long now)
    {
        if (!AddonLinkWire.Integer(command, "playerId", out long id) || id < 0 || id > 255 ||
            !command.TryGetProperty("rms", out var volume) || volume.ValueKind != JsonValueKind.Number || !volume.TryGetDouble(out double rms) ||
            !double.IsFinite(rms) || rms < 0 || rms > 1 || !command.TryGetProperty("active", out var active) ||
            active.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return;
        bool host = command.TryGetProperty("host", out var source) && source.ValueKind == JsonValueKind.True;
        string? version = AddonLinkWire.Text(command, "version");
        if (!host && id == owner) AddonLinkVoice.Store((byte)id, rms, active.GetBoolean(), now, version);
        if (host && id != owner && AmongUsClient.Instance.AmHost) HostVoiceRelay.AcceptUnified((byte)id, rms, active.GetBoolean(), version, now);
    }
    private static void Ready(JsonElement command)
    {
        if (!command.TryGetProperty("players", out var ids) || ids.ValueKind != JsonValueKind.Array || ids.GetArrayLength() > 24) return;
        foreach (var id in ids.EnumerateArray())
            if (id.ValueKind == JsonValueKind.Number && id.TryGetByte(out byte player)) HeroSpeechHost.AcceptUnifiedReady(player);
    }
    private static void Parrot(JsonElement command)
    {
        if (AmongUsClient.Instance.AmHost != true || !AddonLinkWire.Integer(command, "ownerId", out long ownerId) || ownerId < 0 || ownerId > 255 ||
            !AddonLinkWire.Integer(command, "birdId", out long bird) || bird <= 0 || bird > int.MaxValue ||
            !AddonLinkWire.Integer(command, "sourceId", out long source) || source < 0 || source > 255) return;
        ParrotTrainer.RequestCapture((byte)ownerId, (int)bird, (byte)source);
    }
    private static void Close()
    {
        if (server == null) return;
        server.Dispose(); server = null; game = null; owner = 255;
        nextSnapshot = nextDescriptor = revision = 0;
        AddonLinkState.Reset(); AddonLinkVoice.Reset();
        try { File.Delete(path); File.Delete(path + ".tmp"); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

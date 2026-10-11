using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using HarmonyLib;
using Nebula.Modules;
using Virial.Attributes;
using Virial.Runtime;

namespace TanukiBCL.Roles;

[NebulaPreprocess(PreprocessPhase.PostRoles)]
public static class HostVoiceRelay
{
    private static UdpClient? socket;
    private static string token = "";
    private static string path = "";
    private static readonly HostVoiceFeed feed = new();
    private static long[] forwarded = new long[256];
    private static long[] throttled = new long[256];
    private static byte owner = 255;
    private static readonly RemoteProcess<(byte player, float rms, bool active)> Rpc = new("TanukiHostVoiceRelayV1",
        (data, _) => feed.Store(data.player, data.rms, data.active, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));

    public static void Preprocess(NebulaPreprocessor preprocessor)
    {
        new Harmony("tanuki.bcl.roles.hostRelay").Patch(AccessTools.Method(typeof(HudManager), nameof(HudManager.Update)),
            postfix: new HarmonyMethod(typeof(HostVoiceRelay), nameof(Update)));
    }
    public static bool TryRead(byte player, ref long consumed, out double rms, out bool active)
    {
        return feed.TryRead(player, ref consumed, out rms, out active, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }
    public static void Update()
    {
        bool host = AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost && PlayerControl.LocalPlayer != null;
        if (!host) { Close(); return; }
        if (owner != PlayerControl.LocalPlayer!.PlayerId) Close();
        if (socket == null) Open();
        if (socket == null) return;
        for (int i = 0; i < 64 && socket.Available > 0; i++) Read();
    }
    private static void Open()
    {
        try
        {
            socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            token = Guid.NewGuid().ToString("N"); owner = PlayerControl.LocalPlayer.PlayerId;
            Array.Clear(forwarded, 0, forwarded.Length); Array.Clear(throttled, 0, throttled.Length);
            var directory = Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TanukiBCL", "NoS");
            Directory.CreateDirectory(directory); path = Path.Combine(directory, "host-voice-" + Environment.ProcessId + ".json");
            File.WriteAllText(path, "{\"protocol\":1,\"pid\":" + Environment.ProcessId + ",\"port\":" + ((IPEndPoint)socket.Client.LocalEndPoint!).Port + ",\"token\":\"" + token + "\"}");
        }
        catch { Close(); }
    }
    private static void Close() { socket?.Dispose(); socket = null; owner = 255; }
    private static void Read()
    {
        IPEndPoint sender = new(IPAddress.Any, 0);
        var bytes = socket!.Receive(ref sender);
        if (!IPAddress.IsLoopback(sender.Address) || bytes.Length > 256) return;
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (!HostVoiceFeed.Parse(Encoding.ASCII.GetString(bytes), token, now, out byte player, out long seq, out double rms, out bool active)) return;
        if (seq <= forwarded[player] || now - throttled[player] < 80) return;
        forwarded[player] = seq; throttled[player] = now;
        Rpc.Invoke((player, (float)rms, active));
    }
}

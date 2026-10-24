using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using HarmonyLib;
using Nebula.Modules;
using Nebula.Utilities;
using Virial;
using Virial.Attributes;
using Virial.Runtime;

namespace TanukiBCL.Roles;

// Every PC publishes its own jobs. Only the host publishes remote candidates;
// BCL admits remote audio only for positively identified Web/mobile peers.
[NebulaPreprocess(PreprocessPhase.PostRoles)]
public static class HeroSpeechHost
{
    private static UdpClient? socket;
    private static string token = "", path = "";
    private static long nextPublish;
    private static int lastStage = -1;
    private static readonly Dictionary<byte, long> readyUntil = new();
    private static readonly RemoteProcess<(byte player, bool ready)> ReadyRpc = new("TanukiSpeechReadyV2", (data, _) =>
    {
        if (data.ready) readyUntil[data.player] = Now + 1200;
        else readyUntil.Remove(data.player);
    });
    private static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public static bool IsPresent(UnityEngine.Component? view) => view != null && view.gameObject.activeInHierarchy;
    public static bool ReadyFor(byte player) => readyUntil.TryGetValue(player, out long until) && until > Now;
    public static void Preprocess(NebulaPreprocessor preprocessor)
    {
        try { NGWord.EnsureTemplate(); }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        { NebulaAPI.Logging.NebulaLogger("TanukiSpeech").Error(ex.Message); }
        new Harmony("tanuki.bcl.roles.heroSpeech").Patch(AccessTools.Method(typeof(HudManager), nameof(HudManager.Update)),
            postfix: new HarmonyMethod(typeof(HeroSpeechHost), nameof(Update)));
    }
    public static void Update()
    {
        var game = AmongUsClient.Instance;
        bool active = game != null && game.GameState == InnerNet.InnerNetClient.GameStates.Started &&
            NebulaAPI.CurrentGame != null && !HeroSpeechHost.IsPresent(ExileController.Instance) && !HeroSpeechHost.IsPresent(IntroCutscene.Instance);
        int stage = (game != null ? 1 : 0) | (game?.GameState == InnerNet.InnerNetClient.GameStates.Started ? 2 : 0) |
            (NebulaAPI.CurrentGame != null ? 4 : 0) | (HeroSpeechHost.IsPresent(IntroCutscene.Instance) ? 8 : 0) | (HeroSpeechHost.IsPresent(ExileController.Instance) ? 16 : 0);
        if (stage != lastStage) { lastStage = stage; NebulaAPI.Logging.NebulaLogger("TanukiSpeech").Message("Bridge stage=" + stage); }
        if (!active) { Close(); return; }
        try { NGWord.EnsureAllPlayersAssigned(); }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
        { NebulaAPI.Logging.NebulaLogger("TanukiSpeech").Error(ex.Message); }
        var players = NebulaAPI.CurrentGame!.GetAllPlayers().Where(p => !p.IsDead && !p.IsDisconnected && (game!.AmHost || p.AmOwner)).ToArray();
        var jobs = new List<object>();
        foreach (var player in players)
        {
            var hero = player.Role.GetAbility<HeroAbility>();
            if (hero != null && hero.SpeechEligible) jobs.Add(hero.SpeechJob);
            if (player.TryGetModifier<NGWord.Instance>(out var ng) && ng.SpeechEligible) jobs.Add(ng.SpeechJob);
        }
        if (jobs.Count == 0) { Close(); return; }
        try { Tick(jobs); }
        catch (Exception ex) when (ex is IOException || ex is SocketException || ex is UnauthorizedAccessException) { Close(); }
    }
    private static void Tick(List<object> jobs)
    {
        if (socket == null) Open();
        for (int i = 0; i < 96 && socket!.Available > 0; i++) Read();
        long now = Now;
        if (now < nextPublish) return;
        nextPublish = now + 200;
        int port = ((IPEndPoint)socket!.Client.LocalEndPoint!).Port;
        string json = JsonSerializer.Serialize(new { protocol = 2, pid = Environment.ProcessId, port, token, updated = now, players = jobs });
        File.WriteAllText(path + ".tmp", json, new UTF8Encoding(false));
        File.Move(path + ".tmp", path, true);
    }
    private static void Open()
    {
        socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        socket.Client.Blocking = false;
        token = Guid.NewGuid().ToString("N");
        string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TanukiBCL", "NoS");
        Directory.CreateDirectory(directory);
        path = Path.Combine(directory, "hero-speech-" + Environment.ProcessId + ".json");
    }
    private static void Read()
    {
        IPEndPoint sender = new(IPAddress.Any, 0);
        byte[] bytes = socket!.Receive(ref sender);
        if (!IPAddress.IsLoopback(sender.Address) || bytes.Length > 256) return;
        var fields = Encoding.ASCII.GetString(bytes).Split('|');
        if (fields.Length < 6 || fields[0] != "TBCLHS2" || fields[1] != token ||
            !long.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out long time) ||
            Now - time > 500 || time - Now > 100 || !byte.TryParse(fields[4], out byte id)) return;
        var player = NebulaAPI.CurrentGame?.GetPlayer(id);
        if (player == null || player.IsDead || player.IsDisconnected || (!AmongUsClient.Instance.AmHost && !player.AmOwner)) return;
        if (fields.Length == 6 && fields[2] == "ready")
        {
            if (fields[5] == "0" || fields[5] == "1") ReadyRpc.Invoke((id, fields[5] == "1"));
            return;
        }
        if (fields.Length != 7 || fields[2] != "word" || !int.TryParse(fields[5], out int request) || !ReadyFor(id)) return;
        if (fields[6] == "henshin") player.Role.GetAbility<HeroAbility>()?.RecogniseSpeech(request);
        if (fields[6] == "ng" && player.TryGetModifier<NGWord.Instance>(out var ng)) ng.RecogniseSpeech(request);
    }
    private static void Close()
    {
        if (socket == null) return;
        socket.Dispose(); socket = null; nextPublish = 0;
        // Other PCs keep their own readiness heartbeats; let them expire.
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

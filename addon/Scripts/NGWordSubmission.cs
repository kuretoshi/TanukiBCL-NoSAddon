using System;
using System.Linq;
using System.Collections;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using Nebula.Behavior;
using Nebula.Modules;
using Nebula.Modules.GUIWidget;
using Nebula.Utilities;
using UnityEngine;
using Virial;
using Virial.Media;
using Virial.Configuration;
using Virial.Text;

namespace TanukiBCL.Roles;

public static class NGWordSubmission
{
    private static object? game;
    private static string session = "";
    private static NGWordSubmissionRound? round;
    private static bool finished, started, preview, previewConfirmed, startGate, showingWaiting;
    private static int host;
    private static MetaScreen? window;
    private static string draft = "", error = "";
    private static long retryAt, nextBegin;
    public static bool Waiting => started && !finished && ReferenceEquals(game, NebulaAPI.CurrentGame) &&
        AmongUsClient.Instance != null && AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started;
    private static string T(string key) => NebulaAPI.Language.Translate("tanuki.ngInput." + key);
    private static byte[] Roster() => NebulaAPI.CurrentGame!.GetAllPlayers()
        .Where(p => !p.IsDisconnected && !p.IsDead).Select(p => p.PlayerId).ToArray();
    private static void SyncGame()
    {
        if (ReferenceEquals(game, NebulaAPI.CurrentGame)) return;
        Close(); game = NebulaAPI.CurrentGame; session = ""; round = null;
        finished = started = startGate = false; draft = error = ""; retryAt = nextBegin = 0;
        host = AmongUsClient.Instance == null ? -1 : AmongUsClient.Instance.HostId;
    }
    private static readonly RemoteProcess<(string session, byte[] players)> BeginRpc = new("TanukiNGInputBeginV1", (data, _) =>
    {
        SyncGame();
        if (game == null || finished || session == data.session) return;
        session = data.session; round = new(data.players); started = true;
    });
    private static readonly RemoteProcess<(string session, byte player, string value)> SubmitRpc = new("TanukiNGInputSubmitV1", (data, _) =>
    {
        SyncGame();
        if (!Waiting || data.session != session || round == null) return;
        round.Submit(data.player, data.value);
    });
    private static readonly RemoteProcess<(string session, bool cancelled)> EndRpc = new("TanukiNGInputEndV1", (data, _) =>
    {
        if (data.session != session) return;
        finished = true; Close();

    });
    public static void Install()
    {
        new Harmony("tanuki.bcl.roles.ngInput").Patch(AccessTools.PropertyGetter(typeof(PlayerControl), nameof(PlayerControl.CanMove)),
            postfix: new HarmonyMethod(typeof(NGWordSubmission), nameof(CanMove)));
        new Harmony("tanuki.bcl.roles.ngInput.start").Patch(AccessTools.Method(typeof(IntroCutscene), nameof(IntroCutscene.CoBegin)),
            postfix: new HarmonyMethod(typeof(NGWordSubmission), nameof(AfterIntro)));
    }
    // Keep CoShowIntro suspended after the role reveal, before spawn/fade/start.
    private static void AfterIntro(ref Il2CppSystem.Collections.IEnumerator __result)
    {
        var original = __result;
        __result = WaitAfterIntro(original).WrapToIl2Cpp();
    }
    private static IEnumerator WaitAfterIntro(Il2CppSystem.Collections.IEnumerator original)
    {
        yield return original;
        if (!NGWord.UsesPlayerInput) yield break;
        SyncGame();
        var current = game;
        startGate = true;
        try
        {
            while (ReferenceEquals(current, NebulaAPI.CurrentGame) && !finished &&
                AmongUsClient.Instance != null && AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started)
            {
                Tick();
                yield return null;
            }
        }
        finally
        {
            if (ReferenceEquals(current, game)) { startGate = false; Close(); }
        }
    }
    private static void CanMove(PlayerControl __instance, ref bool __result)
    {
        if (Waiting && __instance == PlayerControl.LocalPlayer) __result = false;
    }
    public static IConfiguration PreviewOption() => NebulaAPI.Configurations.Configuration(() => null,
        () => NebulaAPI.GUI.LocalizedButton(GUIAlignment.Center, Attr(), "tanuki.ngInput.preview", _ => ShowPreview()), () => true);
    public static void ShowPreview()
    {
        if (Waiting || startGate) return;
        preview = true; previewConfirmed = false; draft = error = "";
        Close(); Open();
    }
    public static void Tick()
    {
        if (preview && window != null) return;
        preview = false;
        SyncGame();
        if (game == null || AmongUsClient.Instance == null || AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Started) { Close(); return; }
        if (!NGWord.UsesPlayerInput || !startGate) return;
        if (!started && AmongUsClient.Instance.AmHost)
            BeginRpc.Invoke((Guid.NewGuid().ToString("N"), Roster()));
        if (!Waiting || round == null) return;
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (AmongUsClient.Instance.AmHost && now >= nextBegin)
        {
            nextBegin = now + 1500;
            BeginRpc.Invoke((session, Roster()));
        }
        // Host changes cancel this collection rather than leaving everybody frozen.
        if (host != AmongUsClient.Instance.HostId) { finished = true; Close(); return; }
        round.Retain(Roster());
        if (AmongUsClient.Instance.AmHost && round.Count < 2)
        { EndRpc.Invoke((session, true)); return; }
        if (AmongUsClient.Instance.AmHost && round.Confirmed == round.Count)
        {
            var allocation = round.Allocate(System.Random.Shared, NGWord.AssignmentCount)!;
            foreach (var player in NebulaAPI.CurrentGame!.GetAllPlayers())
                if (allocation.TryGetValue(player.PlayerId, out var entry))
                    player.AddModifier(NGWord.MyRole, entry.Encode(System.Random.Shared.Next(1, int.MaxValue)));
            EndRpc.Invoke((session, false)); return;
        }
        if (PlayerControl.LocalPlayer == null) return;
        // Every player receives submissions, so progress and confirmation are shared.
        if (round.HasSubmitted(PlayerControl.LocalPlayer.PlayerId)) error = "";
        if (window == null) Open();
        bool confirmed = round.HasSubmitted(PlayerControl.LocalPlayer.PlayerId);
        if (window != null && confirmed != showingWaiting) Render(confirmed);
    }
    private static TextAttribute Attr(float height = .4f) => new(NebulaAPI.GUI.GetAttribute(AttributeAsset.OptionsTitleHalf))
    { Font = NebulaAPI.GUI.GetFont(FontAsset.Gothic), Size = new(6f, height), Wrapping = true,
      Alignment = Virial.Text.TextAlignment.Center, FontSize = new(1.5f, 1.2f, 1.5f) };
    private static void Open()
    {
        var parent = UnityHelper.FindCamera(LayerMask.NameToLayer("UI"))?.transform;
        if (parent == null) return;
        window = MetaScreen.GenerateWindow(new Vector2(6.8f, 3.5f), parent, Vector3.zero, true, false, withCloseButton: false);
        // MetaScreen normally dims the scene; the pre-game entry screen is opaque.
        var black = window.transform.parent.Find("BlackScreen")?.GetComponent<SpriteRenderer>();
        if (black != null) black.color = UnityEngine.Color.black;
        Render(preview && previewConfirmed);
    }
    private static void Render(bool confirmed)
    {
        if (window == null) return;
        showingWaiting = confirmed;
        if (confirmed)
        {
            var waiting = NebulaAPI.GUI.VerticalHolder(GUIAlignment.Center,
                NebulaAPI.GUI.RawText(GUIAlignment.Center, Attr(.8f), T("waiting")),
                NebulaAPI.GUI.RealtimeText(GUIAlignment.Center, Attr(), () =>
                    T("progress") + (preview ? "1/2" : (round?.Confirmed ?? 0) + "/" + (round?.Count ?? 0)), 20));
            window.SetWidget(preview ? NebulaAPI.GUI.VerticalHolder(GUIAlignment.Center, waiting,
                NebulaAPI.GUI.RawButton(GUIAlignment.Center, Attr(), T("previewEnd"), _ => { preview = false; Close(); })) : waiting,
                new Virial.Compat.Vector2(.5f, .5f), out _);
            return;
        }
        var field = new GUITextField(GUIAlignment.Center, new(5.8f, .45f))
        { DefaultText = draft, MaxLines = 1, FontSize = 1.7f, IsSharpField = false, WithMaskMaterial = false,
          LostFocusAction = value => draft = value };
        window.SetWidget(NebulaAPI.GUI.VerticalHolder(GUIAlignment.Center,
            NebulaAPI.GUI.RawText(GUIAlignment.Center, Attr(), T("title")),
            NebulaAPI.GUI.RawText(GUIAlignment.Center, Attr(.8f), T("hint")), field,
            NebulaAPI.GUI.RawButton(GUIAlignment.Center, Attr(), T("confirm"), _ => Confirm()),
            NebulaAPI.GUI.RealtimeText(GUIAlignment.Center, Attr(.65f), () =>
                (preview ? (error.Length > 0 ? error : T("previewHint")) : T("progress") + (round?.Confirmed ?? 0) + "/" + (round?.Count ?? 0) + "\n" +
                (round != null && PlayerControl.LocalPlayer != null && round.HasSubmitted(PlayerControl.LocalPlayer.PlayerId) ? T("waiting") : error)), 20)),
            new Virial.Compat.Vector2(.5f, .5f), out _);
    }
    private static void Confirm()
    {
        TextField.LoseAllFocus();
        if (preview)
        {
            if (!NGWordSubmissionRound.TryParse(draft, out _)) { error = T("invalid"); return; }
            previewConfirmed = true; Render(true); return;
        }
        if (!Waiting || round == null || PlayerControl.LocalPlayer == null || round.HasSubmitted(PlayerControl.LocalPlayer.PlayerId)) return;
        if (!NGWordSubmissionRound.TryParse(draft, out _)) { error = T("invalid"); return; }
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (now < retryAt) return;
        retryAt = now + 1000;
        SubmitRpc.Invoke((session, PlayerControl.LocalPlayer.PlayerId, draft.Trim()));
    }
    private static void Close() { if (window != null) window.CloseScreen(); window = null; }
}

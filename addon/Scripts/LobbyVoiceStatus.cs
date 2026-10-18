using System;
using System.Diagnostics;
using HarmonyLib;
using Nebula.Modules;
using Nebula.Utilities;
using Virial;
using Virial.Attributes;
using Virial.Runtime;
using UnityEngine;

namespace TanukiBCL.Roles;

[NebulaPreprocess(PreprocessPhase.PostRoles)]
public static class LobbyVoiceStatus
{
    private static VoiceBridge? bridge;
    private static byte owner;
    private static readonly Stopwatch clock = Stopwatch.StartNew();
    private static double nextAttempt, entered;
    private static LobbyBehaviour? lobby;
    private static TMPro.TextMeshPro? notice;

    public static void Preprocess(NebulaPreprocessor preprocessor)
    {
        new Harmony("tanuki.bcl.roles.lobbyVoiceStatus").Patch(
            AccessTools.Method(typeof(HudManager), nameof(HudManager.Update)),
            postfix: new HarmonyMethod(typeof(LobbyVoiceStatus), nameof(Update)));
    }

    // The microphone test temporarily owns the same legacy discovery slot.
    public static void ReleaseBridge() { bridge?.Dispose(); bridge = null; }

    public static void Update()
    {
        var current = LobbyBehaviour.Instance;
        var player = PlayerControl.LocalPlayer;
        if (current == null || player == null || AmongUsClient.Instance == null ||
            AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Joined)
        {
            ReleaseBridge();
            if (notice != null) UnityEngine.Object.Destroy(notice.gameObject);
            notice = null; lobby = null;
            return;
        }
        if (lobby != current || owner != player.PlayerId)
        {
            ReleaseBridge();
            lobby = current; owner = player.PlayerId;
            entered = clock.Elapsed.TotalSeconds; nextAttempt = 0;
        }
        bool connected = MicrophoneTest.Connected;
        string? version = MicrophoneTest.ConnectedVersion;
        bool fromHost = MicrophoneTest.FromHostRelay;
        try
        {
            if (!MicrophoneTest.IsRunning)
            {
                if (bridge == null && clock.Elapsed.TotalSeconds >= nextAttempt)
                {
                    nextAttempt = clock.Elapsed.TotalSeconds + 1;
                    bridge = new VoiceBridge(owner, "tanukiMicTest");
                }
                bridge?.TryReadSample(out _, out _, out _);
                connected = bridge?.Connected ?? false;
                version = bridge?.LatestVersion;
                fromHost = bridge?.LatestFromHostRelay ?? false;
            }
        }
        catch (Exception ex) when (ex is System.IO.IOException || ex is System.Net.Sockets.SocketException || ex is UnauthorizedAccessException)
        {
            ReleaseBridge(); nextAttempt = clock.Elapsed.TotalSeconds + 1;
        }
        if (notice == null)
        {
            var hud = HudManager.Instance;
            var template = hud?.KillButton?.cooldownTimerText;
            if (template == null || hud == null) return;
            notice = UnityEngine.Object.Instantiate(template, hud.transform);
            notice.name = "TanukiLobbyVoiceStatus";
            notice.transform.localScale = Vector3.one;
            notice.gameObject.SetAsUIAspectContent(AspectPosition.EdgeAlignments.Top,
                new Vector3(0, .6f, -30));
            notice.GetComponent<AspectPosition>().updateAlways = true;
            // The cooldown template has auto sizing enabled; leaving it on
            // enlarges this notice to fill the wide rectangle.
            notice.enableAutoSizing = false;
            notice.fontSize = 1.35f;
            notice.enableWordWrapping = false;
            notice.overflowMode = TMPro.TextOverflowModes.Ellipsis;
            notice.alignment = TMPro.TextAlignmentOptions.Center;
            notice.rectTransform.sizeDelta = new Vector2(8f, .4f);
            notice.color = new UnityEngine.Color(1f, .65f, .2f);
            notice.text = NebulaAPI.Language.Translate("tanuki.lobbyVoice.disconnected");
        }
        string key = !connected ? "disconnected"
            : version == null ? "unknownVersion" : "outdated";
        if (connected && fromHost) key += "Host";
        notice.text = string.Format(NebulaAPI.Language.Translate("tanuki.lobbyVoice." + key), version ?? "");
        notice.gameObject.SetActive((!connected || !VoiceVersionRequirement.IsSupported(version)) &&
            clock.Elapsed.TotalSeconds - entered >= 3);
    }
}

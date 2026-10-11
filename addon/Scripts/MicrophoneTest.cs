using System;
using System.Diagnostics;
using Nebula;
using Virial;
using Virial.Configuration;
using Virial.Media;
using Virial.Text;

namespace TanukiBCL.Roles;

public static class MicrophoneTest
{
    private static VoiceBridge? bridge;
    private static readonly Stopwatch clock = Stopwatch.StartNew();
    private static double started, lastDraw;
    private static float peak;
    private static string? error;

    public static IConfiguration Option(Func<float> threshold, Func<float>? fastThreshold = null)
        => NebulaAPI.Configurations.Configuration(() => null, () =>
            NebulaAPI.GUI.VerticalHolder(GUIAlignment.Center,
                NebulaAPI.GUI.LocalizedButton(GUIAlignment.Center, NebulaAPI.GUI.GetAttribute(AttributeAsset.OptionsTitleHalf),
                    "tanuki.micTest.start", _ => Start()),
                NebulaAPI.GUI.RealtimeText(GUIAlignment.Center, new TextAttribute(NebulaAPI.GUI.GetAttribute(AttributeAsset.OptionsValue)) { Size = new(6f, 1.8f) },
                    () => Read(threshold, fastThreshold), 55)), () => LobbyBehaviour.Instance != null);

    private static void Start()
    {
        Stop(); peak = 0; error = null;
        if (LobbyBehaviour.Instance == null || PlayerControl.LocalPlayer == null) return;
        try
        {
            bridge = new VoiceBridge(PlayerControl.LocalPlayer.PlayerId, "tanukiMicTest");
            started = lastDraw = clock.Elapsed.TotalSeconds;
            Watch(bridge);
        }
        catch (Exception ex) { error = ex.Message; }
    }

    private static void Watch(VoiceBridge session)
    {
        NebulaManager.Instance.StartDelayAction(.2f, () =>
        {
            if (bridge != session) return;
            if (LobbyBehaviour.Instance == null || clock.Elapsed.TotalSeconds - started >= 20
                || clock.Elapsed.TotalSeconds - lastDraw > .6) Stop();
            else Watch(session);
        });
    }

    private static void Stop() { bridge?.Dispose(); bridge = null; }
    private static string T(string key) => NebulaAPI.Language.Translate("tanuki.micTest." + key);
    private static string Read(Func<float> threshold, Func<float>? fastThreshold)
    {
        lastDraw = clock.Elapsed.TotalSeconds;
        if (bridge == null) return error == null ? T("hint") : T("error");
        try { bridge.TryReadSample(out _, out _, out _); }
        catch (Exception ex) { error = ex.Message; Stop(); return T("error"); }
        if (!bridge.Connected) return T("offline");
        double rms = bridge.LatestActive ? bridge.LatestRms : 0;
        float db = rms > 0 ? (float)(20 * Math.Log10(rms)) : -60;
        float level = Math.Clamp((db + 60) / 60 * 100, 0, 100);
        peak = Math.Max(peak, level);
        int bars = (int)(level / 5);
        string color = db >= threshold() ? "FFB347" : "64DCB4";
        return T("current") + " " + VoiceLevelDisplay.Format(db) + " / 100  " + T("peak") + " " + Math.Round(peak)
            + "\n<color=#" + color + ">" + new string('■', bars) + new string('□', 20 - bars) + "</color>"
            + "\n" + T("line") + " " + VoiceLevelDisplay.Format(threshold())
            + (fastThreshold == null ? "" : "  " + T("fast") + " " + VoiceLevelDisplay.Format(fastThreshold()))
            + "\n" + (bridge.LatestActive ? (db >= threshold() ? T("reached") : T("below")) : T("muted"))
            + "  " + Math.Ceiling(Math.Max(0, 20 - (clock.Elapsed.TotalSeconds - started))) + "s";
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Nebula.Modules;
using Virial;
using Virial.Attributes;
using Virial.Game;
using Virial.Runtime;
using UnityEngine;

namespace TanukiBCL.Roles;

// Every PC can publish its own microphone activity using the existing bridge.
// Web/mobile metrics already reach all addons through HostVoiceRelay.
// Never publish an audio channel or modify BetterCrewLink's audible distance.
[NebulaPreprocess(PreprocessPhase.PostRoles)]
public static class SonarmanVoiceHub
{
    private static object? game;
    private static SharedVoiceBridge? bridge;
    private static double retryAt, sendAt, nextPoll;
    private static readonly Dictionary<byte, SonarmanSignal> signals = new();
    private static readonly RemoteProcess<byte> PulseRpc = new("TanukiSonarmanSpeechPulseV1",(id,_) => Receive(id));
    private static double Now => Time.unscaledTimeAsDouble;
    public static void Preprocess(NebulaPreprocessor preprocessor) { }
    private static void Scope()
    {
        if (ReferenceEquals(game,NebulaAPI.CurrentGame)) return;
        bridge?.Dispose(); bridge = null; signals.Clear();
        retryAt = sendAt = nextPoll = 0; game = NebulaAPI.CurrentGame;
    }
    public static void Update()
    {
        Scope();
        if (Now < nextPoll) return;
        nextPoll = Now + .05;
        var current = NebulaAPI.CurrentGame;
        bool inTaskPhase = AmongUsClient.Instance != null && AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started &&
            !HeroSpeechHost.IsPresent(MeetingHud.Instance) && !HeroSpeechHost.IsPresent(IntroCutscene.Instance) && !HeroSpeechHost.IsPresent(ExileController.Instance);
        bool active = inTaskPhase && current?.LocalPlayer != null && current.GetAllPlayers().Any(p => p.Role?.GetAbility<Sonarman.Ability>()?.Scanning == true);
        if (!active || current!.LocalPlayer.IsDead || current.LocalPlayer.IsDisconnected)
        { bridge?.Dispose(); bridge = null; return; }
        if (bridge == null && Now >= retryAt)
        {
            retryAt = Now + 1;
            try { bridge = new SharedVoiceBridge(current.LocalPlayer.PlayerId); }
            catch (Exception ex) { NebulaAPI.Logging.NebulaLogger("TanukiSonarman").Error(ex.Message); }
        }
        if (bridge == null) return;
        try
        {
            if (!bridge.TryReadSample(out _,out double rms,out bool enabled) || !enabled || rms < .01 || Now < sendAt) return;
            sendAt = Now + .2;
            PulseRpc.Invoke(current.LocalPlayer.PlayerId);
        }
        catch (Exception ex)
        { bridge.Dispose(); bridge = null; NebulaAPI.Logging.NebulaLogger("TanukiSonarman").Error(ex.Message); }
    }
    private static void Receive(byte id)
    {
        Scope();
        var player = NebulaAPI.CurrentGame?.GetPlayer(id);
        if (player == null || player.IsDead || player.IsDisconnected) return;
        var p = player.TruePosition;
        signals[id] = new(id,p.x,p.y,Now);
    }
    public static bool TryRead(byte id, ref double seen, out SonarmanSignal signal)
    {
        if (!signals.TryGetValue(id,out signal) || signal.Time <= seen || Now-signal.Time > .75) return false;
        seen = signal.Time;
        return true;
    }
}

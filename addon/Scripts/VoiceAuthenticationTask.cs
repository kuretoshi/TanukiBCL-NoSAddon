using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Nebula.Modules;
using Virial;
using Virial.Attributes;
using Virial.Configuration;
using Virial.DI;
using Virial.Events.Game;
using Virial.Events.Game.Map;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial.Runtime;
using UnityEngine;

namespace TanukiBCL.Roles;

[NebulaPreprocess(PreprocessPhase.PostRoles)]
public static class VoiceAuthenticationTask
{
    public static readonly BoolConfiguration Enabled = NebulaAPI.Configurations.Configuration("options.tanukiVoiceAuthentication.enabled", false);
    public static readonly IntegerConfiguration WordCount = NebulaAPI.Configurations.Configuration("options.tanukiVoiceAuthentication.words", (1, 5), 3);
    public static Round? Current { get; private set; }
    private static int nextRequest = 1000000000;
    private static readonly RemoteProcess<(Player player, uint task, int request)> WordsRequestRpc = new("TanukiVoiceAuthenticationWordsRequestV1",
        (m, _) => Current?.RequestWords(m.player, m.task, m.request));
    private static readonly RemoteProcess<(Player player, uint task, int request, string words)> WordsReplyRpc = new("TanukiVoiceAuthenticationWordsReplyV1",
        (m, _) => Current?.ReceiveWords(m.player, m.task, m.request, m.words));
    private static readonly RemoteProcess<(Player player, int request, uint task, int step)> BeginRpc = new("TanukiVoiceAuthenticationBeginV2",
        (m, _) => Current?.Begin(m.player, m.request, m.task, m.step));
    private static readonly RemoteProcess<(Player player, int request)> CloseRpc = new("TanukiVoiceAuthenticationCloseV1",
        (m, _) => Current?.Close(m.player.PlayerId, m.request));
    private static readonly RemoteProcess<(Player player, int request)> KeepRpc = new("TanukiVoiceAuthenticationKeepV1",
        (m, _) => Current?.Keep(m.player.PlayerId, m.request));
    private static readonly RemoteProcess<(Player player, int request)> HitRpc = new("TanukiVoiceAuthenticationHitV1",
        (m, _) => Current?.Hit(m.player, m.request));

    public static string Text(string key) => NebulaAPI.Language.Translate("tanuki.voiceAuthentication." + key);
    public static bool HasSpeech(byte player) => Current?.HasSpeech(player) == true;
    public static object? SpeechJob(byte player) => Current?.SpeechJob(player);
    public static bool Recognise(Player player, int request)
    {
        if (Current?.Accepts(player.PlayerId, request) != true) return false;
        HitRpc.Invoke((player, request));
        return true;
    }
    public static void Preprocess(NebulaPreprocessor preprocessor)
    {
        NebulaAPI.Configurations.Holder("options.tanukiVoiceAuthentication", new[] { ConfigurationTab.Settings },
            new[] { GameModes.Standard, GameModes.FreePlay }).AppendConfigurations(new IConfiguration[] { Enabled, WordCount, VoiceAuthenticationWordSettings.Option() }).AddTags(AddonRoleTag.Tag);
        VoiceAuthenticationMap.InstallPatches(new Harmony("tanuki.bcl.roles.voiceAuthentication"));
        // Preprocessing runs before a game/operator manager exists. Attach the
        // map listeners when NoS constructs each supported game-mode container.
        preprocessor.DIManager.RegisterGeneralModule<IGameModeStandard>(() => new Installer().Register(NebulaAPI.CurrentGame!));
        preprocessor.DIManager.RegisterGeneralModule<IGameModeFreePlay>(() => new Installer().Register(NebulaAPI.CurrentGame!));
    }
    public sealed class Installer : IModule, IGameOperator
    {
        private double nextAttempt;
        public Installer() => NebulaAPI.Logging.NebulaLogger("TanukiVoiceAuthentication").Message("Map installer attached to game.");
        private void Modify(ModifyMapEvent ev) => Install();
        // Freeplay exposes settings only after its map has already loaded.
        private void Freeplay(GameUpdateEvent ev)
        {
            if (Current == null && Enabled && Now >= nextAttempt && NebulaAPI.CurrentGame?.GameMode is IGameModeFreePlay)
                Install();
        }
        private void Install()
        {
            var ship = ShipStatus.Instance;
            var game = NebulaAPI.CurrentGame;
            if (!Enabled || ship == null || game == null || Current?.Ship == ship) return;
            nextAttempt = Now + 2;
            Current?.Release();
            var round = new Round(ship);
            try { if (!round.Map.Install()) { round.Release(); return; } }
            catch (Exception ex) { round.Release(); NebulaAPI.Logging.NebulaLogger("TanukiVoiceAuthentication").Error(ex.ToString()); return; }
            Current = round;
            round.Register(game);
        }
    }
    public sealed class ActiveSpeech
    {
        public readonly int Request;
        public readonly NGWordEntry Word;
        public double Until;
        public ActiveSpeech(int request, NGWordEntry word) { Request = request; Word = word; Until = Now + 8; }
    }
    public sealed class Round : IGameOperator
    {
        public readonly ShipStatus Ship;
        public readonly VoiceAuthenticationMap Map;
        private readonly Dictionary<uint, VoiceAuthenticationProgress> progress = new();
        private readonly Dictionary<byte, ActiveSpeech> active = new();
        private readonly Dictionary<(byte player, uint task), NGWordEntry[]> taskWords = new();
        private readonly VoiceAuthenticationWordAssignments? hostWords;
        private int pendingWords;
        private double nextWordsRequest;
        private NormalPlayerTask? localTask;
        private VoiceAuthenticationProgress? localProgress;
        private VoiceAuthenticationScreen? screen;
        private double nextKeep;
        public bool WindowOpen => screen?.IsOpen == true;
        public Round(ShipStatus ship)
        {
            Ship = ship; Map = new VoiceAuthenticationMap(ship);
            if (AmongUsClient.Instance.AmHost) hostWords = new(VoiceAuthenticationWordSettings.LoadForGame());
        }
        public bool HasSpeech(byte player) => active.TryGetValue(player, out var state) && state.Until > Now && Available;
        public bool Accepts(byte player, int request) => HasSpeech(player) && active[player].Request == request;
        public object? SpeechJob(byte player)
        {
            if (!HasSpeech(player)) return null;
            var state = active[player];
            return new { playerId = player, kind = "hero", request = state.Request,
                expires = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 4000,
                words = state.Word.Words };
        }
        public void Open()
        {
            if (!Available || WindowOpen || localTask != null || PlayerControl.LocalPlayer == null) return;
            var task = PlayerControl.LocalPlayer.myTasks.ToArray().Select(t => t.TryCast<NormalPlayerTask>())
                .FirstOrDefault(t => t != null && VoiceAuthenticationMap.IsTask(t) && !t.IsComplete);
            if (task == null) return;
            localTask = task;
            if (progress.TryGetValue(task.Id, out var state))
            {
                if (state.Completed != task.taskStep) progress[task.Id] = state = new VoiceAuthenticationProgress(state.Words, task.taskStep);
                ShowTask(state);
                return;
            }
            pendingWords = ++nextRequest;
            AskForWords();
        }
        private void AskForWords()
        {
            var player = NebulaAPI.CurrentGame?.LocalPlayer;
            if (player == null || localTask == null || pendingWords == 0) return;
            nextWordsRequest = Now + 2;
            WordsRequestRpc.Invoke((player, localTask.Id, pendingWords));
        }
        public void RequestWords(Player player, uint task, int request)
        {
            if (hostWords == null || !AmongUsClient.Instance.AmHost || !Available || player.IsDisconnected ||
                request <= 1000000000 || !Map.Near(player.TruePosition)) return;
            var selected = hostWords.GetOrCreate(player.PlayerId, task, WordCount, System.Random.Shared);
            WordsReplyRpc.Invoke((player,task,request,VoiceAuthenticationWordAssignments.Encode(selected)));
        }
        public void ReceiveWords(Player player, uint task, int request, string encoded)
        {
            NGWordEntry[] selected;
            try { selected = VoiceAuthenticationWordAssignments.Decode(encoded); }
            catch (ArgumentException) { return; }
            taskWords[(player.PlayerId,task)] = selected;
            if (!player.AmOwner || request != pendingWords || localTask == null || localTask.Id != task) return;
            if (!Available || !Map.Near(player.TruePosition) || localTask.taskStep > selected.Length) { CloseLocal(); return; }
            pendingWords = 0;
            progress[task] = new VoiceAuthenticationProgress(selected,localTask.taskStep);
            ShowTask(progress[task]);
        }
        private void ShowTask(VoiceAuthenticationProgress state)
        {
            if (localTask == null) return;
            localTask.MaxStep = state.Words.Count;
            localProgress = state;
            screen = new VoiceAuthenticationScreen(state, () => HeroSpeechHost.ReadyFor(PlayerControl.LocalPlayer.PlayerId), CloseLocal);
            StartWord();
        }
        private void StartWord()
        {
            var player = NebulaAPI.CurrentGame?.GetPlayer(PlayerControl.LocalPlayer.PlayerId);
            if (player == null || localTask == null || localProgress == null || localProgress.Finished) return;
            int request = ++nextRequest;
            if (localProgress.Begin(request)) BeginRpc.Invoke((player, request, localTask.Id, localProgress.Completed));
            nextKeep = Now + 2;
        }
        public void Begin(Player player, int request, uint task, int step)
        {
            if (!Available || player.IsDisconnected || request <= 1000000000 || !Map.Near(player.TruePosition) ||
                !taskWords.TryGetValue((player.PlayerId,task), out var words) || step < 0 || step >= words.Length) return;
            if (active.TryGetValue(player.PlayerId, out var old) && old.Request >= request) return;
            active[player.PlayerId] = new ActiveSpeech(request, words[step]);
        }
        public void Keep(byte player, int request)
        {
            if (active.TryGetValue(player, out var state) && state.Request == request) state.Until = Now + 8;
        }
        public void Close(byte player, int request)
        {
            if (active.TryGetValue(player, out var state) && state.Request == request) active.Remove(player);
        }
        public void Hit(Player player, int request)
        {
            if (!Accepts(player.PlayerId, request)) return;
            active.Remove(player.PlayerId);
            if (!player.AmOwner || localTask == null || localProgress == null || !WindowOpen || localTask.IsComplete ||
                !localProgress.Accept(request)) return;
            localTask.NextStep();
            if (localProgress.Finished) CloseLocal();
            else StartWord();
        }
        public void CloseLocal()
        {
            var player = NebulaAPI.CurrentGame?.GetPlayer(PlayerControl.LocalPlayer?.PlayerId ?? 255);
            if (player != null && localProgress?.Listening == true) CloseRpc.Invoke((player, localProgress.Request));
            pendingWords = 0;
            localProgress?.Pause();
            screen?.Dispose(); screen = null; localTask = null; localProgress = null;
        }
        private void Update(GameUpdateEvent ev)
        {
            VoiceAuthenticationMap.UpdatePicker();
            Map.UpdateCollision();
            if (pendingWords != 0)
            {
                if (!Available || localTask == null || !Map.Near(PlayerControl.LocalPlayer.GetTruePosition())) CloseLocal();
                else if (Now >= nextWordsRequest) AskForWords();
            }
            foreach (byte id in active.Keys.ToArray())
                if (active[id].Until <= Now || NebulaAPI.CurrentGame?.GetPlayer(id)?.IsDisconnected != false) active.Remove(id);
            if (screen == null) return;
            screen.Update();
            if (!Available || !WindowOpen || localTask == null || localTask.IsComplete ||
                !PlayerControl.LocalPlayer.myTasks.ToArray().Any(t => t.Id == localTask.Id)) { CloseLocal(); return; }
            if (localProgress != null && localProgress.Completed != localTask.taskStep) { CloseLocal(); return; }
            if (Now < nextKeep || localProgress?.Listening != true) return;
            var player = NebulaAPI.CurrentGame?.GetPlayer(PlayerControl.LocalPlayer.PlayerId);
            if (player != null) KeepRpc.Invoke((player, localProgress.Request));
            nextKeep = Now + 2;
        }
        private void Meeting(MeetingStartEvent ev) { CloseLocal(); active.Clear(); }
        private void Died(PlayerDieEvent ev) { if (ev.Player.AmOwner) CloseLocal(); }
        private void End(GameEndEvent ev) => CloseLocal();
        public void Release()
        {
            CloseLocal(); active.Clear(); progress.Clear(); taskWords.Clear(); Map.Dispose();
            if (ReferenceEquals(Current, this)) Current = null;
        }
        void IGameOperator.OnReleased() => Release();
    }
    private static double Now => Time.unscaledTimeAsDouble;
    private static bool Available => AmongUsClient.Instance != null &&
        AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started &&
        !HeroSpeechHost.IsPresent(MeetingHud.Instance) && !HeroSpeechHost.IsPresent(IntroCutscene.Instance) && !HeroSpeechHost.IsPresent(ExileController.Instance);
}

using System;
using System.Linq;
using Nebula.Modules;
using Nebula.Utilities;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Game;
using Virial.Text;
using UnityEngine;

namespace TanukiBCL.Roles;

public sealed class NGWord : DefinedAllocatableModifierTemplate, DefinedAllocatableModifier, IAssignableDocument
{
    private static readonly BoolConfiguration Enabled = NebulaAPI.Configurations.Configuration("options.role.tanukiNGWord.enabled", false);
    private static readonly BoolConfiguration Meetings = NebulaAPI.Configurations.Configuration("options.role.tanukiNGWord.meetings", true);
    private static readonly BoolConfiguration HideOwnWord = NebulaAPI.Configurations.Configuration("options.role.tanukiNGWord.hideOwnWord", false);
    private static readonly BoolConfiguration PlayerInput = NebulaAPI.Configurations.Configuration("options.role.tanukiNGWord.playerInput", false);
    private static readonly IntegerConfiguration AssignCount = NebulaAPI.Configurations.Configuration(
        "options.role.tanukiNGWord.assignCount", (0, 24), 0, null,
        count => count == 0 ? NebulaAPI.Language.Translate("options.role.tanukiNGWord.everyone") : count.ToString());
    public static int AssignmentCount => (int)AssignCount;
    private static int TargetCount(int available) => AssignmentCount == 0 ? available : Math.Min(AssignmentCount, available);
    public static bool UsesPlayerInput => Enabled && PlayerInput;
    public static readonly NGWord MyRole = new();
    private static object? assignedGame;
    private NGWord() : base("tanukiNGWord", "TNG", new Virial.Color(245, 140, 70), new IConfiguration[] { Enabled, Meetings, HideOwnWord, PlayerInput, AssignCount })
    {
        ConfigurationHolder!.Illustration = VoiceRoleIcons.Talkative;
        ConfigurationHolder.AddTags(AddonRoleTag.Tag);
        ConfigurationHolder.AppendConfiguration(NGWordSettings.Option());
        ConfigurationHolder.SetDisplayState(() => Enabled ? ConfigurationHolderState.Activated : ConfigurationHolderState.Inactivated);
        NebulaAPI.Logging.NebulaLogger("TanukiSpeech").Message("NG all-player option=" + (bool)Enabled + "; config=" + DataSaver.DataSaverFolderPath);
    }
    Virial.Media.Image? DefinedAssignable.IconImage => VoiceRoleIcons.Talkative;
    bool IAssignableDocument.HasTips => true;
    bool ISpawnable.IsSpawnable => Enabled;
    void HasAssignmentRoutine.TryAssign(IRoleTable table)
    {
        if (!Enabled || PlayerInput) return;
        var entries = LoadWords().OrderBy(_ => Guid.NewGuid()).ToArray();
        int index = 0;
        var roster = table.GetAllPlayers().ToArray();
        foreach (var player in roster.OrderBy(_ => Guid.NewGuid()).Take(TargetCount(roster.Length)))
        {
            var word = entries[index++ % entries.Length];
            table.SetModifier(player.playerId, this, word.Encode(System.Random.Shared.Next(1, int.MaxValue)));
        }
    }
    // NoS freeplay bypasses modifier allocation. Apply the all-player option
    // once after intro there as well, without restoring removed modifiers.
    public static void EnsureAllPlayersAssigned()
    {
        var game = NebulaAPI.CurrentGame;
        if (!Enabled || PlayerInput || game == null || !AmongUsClient.Instance.AmHost || ReferenceEquals(game, assignedGame)) return;
        if (PlayerControl.LocalPlayer == null || !PlayerControl.LocalPlayer.CanMove) return;
        var roster = game.GetAllPlayers().Where(p => !p.IsDisconnected && !p.IsDead).ToArray();
        if (roster.Length == 0) return;
        assignedGame = game;
        int existing = roster.Count(p => p.TryGetModifier<Instance>(out _));
        var players = roster.Where(p => !p.TryGetModifier<Instance>(out _)).OrderBy(_ => Guid.NewGuid())
            .Take(Math.Max(0, TargetCount(roster.Length) - existing)).ToArray();
        if (players.Length == 0) return;
        var entries = LoadWords().OrderBy(_ => Guid.NewGuid()).ToArray();
        for (int i = 0; i < players.Length; i++)
            players[i].AddModifier(MyRole, entries[i % entries.Length].Encode(System.Random.Shared.Next(1, int.MaxValue)));
    }
    public static string EnsureTemplate() => NGWordSettings.Files.EnsureDefault();
    private static NGWordEntry[] LoadWords() => NGWordSettings.Files.Load();
    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(Player player, int[] args) => new Instance(player, args);
    private static readonly RemoteProcess<(Player player, int request)> HitRpc = new("TanukiNGWordHitV1", (data, _) =>
    {
        if (data.player.TryGetModifier<Instance>(out var instance)) instance.Die(data.request);
    });
    public sealed class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;
        private readonly NGWordEntry? entry;
        private readonly int request;
        private bool used;
        private TMPro.TextMeshPro? text;
        private bool meetingText;
        private readonly NGWordNameLabel nameLabel = new();
        public Instance(Player player, int[] args) : base(player)
        { entry = NGWordEntry.Decode(args); request = args.Length > 0 ? args[0] : 0; }
        void RuntimeAssignable.OnActivated() { }
        public bool SpeechEligible => !NGWordSubmission.Waiting && entry != null && !used && !MyPlayer.IsDead && !MyPlayer.IsDisconnected &&
            AmongUsClient.Instance != null && AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started &&
            !HeroSpeechHost.IsPresent(IntroCutscene.Instance) && !HeroSpeechHost.IsPresent(ExileController.Instance) && (!HeroSpeechHost.IsPresent(MeetingHud.Instance) || Meetings);
        public object SpeechJob => new { playerId = MyPlayer.PlayerId, kind = "ng", request,
            expires = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 1400, words = entry!.Words };
        public void RecogniseSpeech(int value)
        {
            if (value != request || !SpeechEligible) return;
            used = true;
            HitRpc.Invoke((MyPlayer, request));
        }
        public void Die(int value)
        {
            if (!AmOwner || value != request || entry == null || MyPlayer.IsDead || MyPlayer.IsDisconnected) return;
            if (AmongUsClient.Instance == null || AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Started ||
                HeroSpeechHost.IsPresent(IntroCutscene.Instance) || HeroSpeechHost.IsPresent(ExileController.Instance)) return;
            if (HeroSpeechHost.IsPresent(MeetingHud.Instance) && !Meetings) return;
            used = true;
            MyPlayer.Suicide(PlayerStates.Suicide, null, HeroSpeechHost.IsPresent(MeetingHud.Instance) ? KillParameter.MeetingKill : KillParameter.NormalKill);
        }
        private void Update(GameUpdateEvent ev)
        {
            UpdateNameLabel();
            if (!AmOwner) return;
            bool meeting = HeroSpeechHost.IsPresent(MeetingHud.Instance);
            if (text != null && meeting != meetingText) DestroyText();
            if (text == null) CreateText(meeting);
            if (text == null) return;
            text.gameObject.SetActive(!MyPlayer.IsDead && !MyPlayer.IsDisconnected);
            string status = !SpeechEligible ? "paused" : HeroSpeechHost.ReadyFor(MyPlayer.PlayerId) ? "active" : "disconnected";
            string state = NebulaAPI.Language.Translate("role.tanukiNGWord." + status);
            text.text = HideOwnWord ? "<size=65%>" + state + "</size>" :
                NebulaAPI.Language.Translate("role.tanukiNGWord.hud") + "「" + (entry?.Display ?? "—") + "」\n<size=65%>" + state + "</size>";
        }
        private void UpdateNameLabel()
        {
            bool playing = AmongUsClient.Instance != null && AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started;
            // Reveal every assigned word to a dead viewer, including their own
            // and dead players' words, regardless of the living-player mode.
            bool ghostViewer = PlayerControl.LocalPlayer?.Data?.IsDead == true;
            bool reveal = ghostViewer || (HideOwnWord && !AmOwner && !MyPlayer.IsDead);
            bool visible = reveal && entry != null && playing && !MyPlayer.IsDisconnected &&
                !HeroSpeechHost.IsPresent(IntroCutscene.Instance) && !HeroSpeechHost.IsPresent(ExileController.Instance);
            nameLabel.Update(MyPlayer.PlayerId, entry?.Display ?? "", visible);
        }
        private void CreateText(bool meeting)
        {
            var template = HudManager.Instance?.KillButton?.cooldownTimerText;
            var parent = meeting ? MeetingHud.Instance?.transform : HudManager.Instance?.transform;
            if (template == null || parent == null) return;
            text = UnityEngine.Object.Instantiate(template, parent);
            text.name = "TanukiNGWord";
            text.gameObject.SetAsUIAspectContent(AspectPosition.EdgeAlignments.Top, new Vector3(0, .95f, -30));
            text.GetComponent<AspectPosition>().updateAlways = true;
            text.enableAutoSizing = false; text.fontSize = 1.3f;
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = new Vector2(8f, .8f);
            text.enableWordWrapping = false; text.overflowMode = TMPro.TextOverflowModes.Ellipsis;
            text.color = new UnityEngine.Color(1f, .65f, .3f);
            meetingText = meeting;
        }
        private void DestroyText() { if (text != null) UnityEngine.Object.Destroy(text.gameObject); text = null; }
        protected override void OnReleased() { DestroyText(); nameLabel.Dispose(); base.OnReleased(); }
    }
}

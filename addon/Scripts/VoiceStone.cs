using System;
using System.Collections.Generic;
using System.Linq;
using Nebula.Game;
using Nebula.Modules;
using Nebula.Roles.Crewmate;
using Virial;
using Virial.Assignable;
using Virial.Attributes;
using Virial.Components;
using Virial.Compat;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;

namespace TanukiBCL.Roles;

public sealed class VoiceStone : DefinedRoleTemplate, DefinedRole, IAssignableDocument
{
    private static readonly FloatConfiguration Cooldown = NebulaAPI.Configurations.Configuration(
        "options.role.tanukiVoiceStone.cooldown", (5f, 120f, 5f), 25f, FloatConfigurationDecorator.Second);
    private static readonly FloatConfiguration Lifetime = NebulaAPI.Configurations.Configuration(
        "options.role.tanukiVoiceStone.lifetime", (5f, 60f, 5f), 15f, FloatConfigurationDecorator.Second);
    private static readonly FloatConfiguration Wait = NebulaAPI.Configurations.Configuration(
        "options.role.tanukiVoiceStone.wait", (1f, 10f, 1f), 5f, FloatConfigurationDecorator.Second);
    private static readonly IntegerConfiguration Maximum = NebulaAPI.Configurations.Configuration(
        "options.role.tanukiVoiceStone.maximum", (1, 12), 8);
    private static readonly IntegerConfiguration Limit = NebulaAPI.Configurations.Configuration(
        "options.role.tanukiVoiceStone.limit", (1, 5), 2);
    private static readonly FloatConfiguration CharacterWidth = NebulaAPI.Configurations.Configuration(
        "options.role.tanukiVoiceStone.width", (.2f, .8f, .05f), .45f);
    public static readonly VoiceStone MyRole = new();
    private VoiceStone() : base("tanukiVoiceStone", Virial.Color.ImpostorColor, RoleCategory.CrewmateRole, Crewmate.MyTeam,
        new IConfiguration[] { Cooldown, Wait, Lifetime, Maximum, CharacterWidth, Limit })
    {
        ConfigurationHolder!.Illustration = VoiceStoneVisual.Icon;
        ConfigurationHolder.AddTags(AddonRoleTag.Tag);
    }
    bool DefinedRole.IsMadmate => true;
    Virial.Media.Image? DefinedAssignable.IconImage => VoiceStoneVisual.Icon;
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasWinCondition => true;
    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(Player player, int[] args) => new Instance(player);
    public sealed class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        private Ability? ability;
        public Instance(Player player) : base(player) { }
        DefinedRole RuntimeRole.Role => MyRole;
        RoleTaskType RuntimeRole.TaskType => RoleTaskType.NoTask;
        bool RuntimeAssignable.CanFixComm => false;
        bool RuntimeAssignable.CanFixLight => false;
        IEnumerable<IPlayerAbility?> RuntimeAssignable.MyAbilities => new IPlayerAbility?[] { ability };
        void RuntimeAssignable.OnActivated() => ability = new Ability(MyPlayer).Register(this);
        [OnlyMyPlayer]
        private void Win(PlayerCheckWinEvent ev) => ev.SetWinIf(ev.GameEnd == NebulaGameEnd.ImpostorWin);
        [OnlyMyPlayer]
        private void BlockWin(PlayerBlockWinEvent ev) => ev.SetBlockedIf(ev.GameEnd == NebulaGameEnd.CrewmateWin);
    }
    private static readonly RemoteProcess<(Player player, int sequence)> ArmRpc = new("TanukiVoiceStoneArmV1",
        (m, _) => m.player.Role.GetAbility<Ability>()?.Begin(m.sequence));
    private static readonly RemoteProcess<(Player player, int sequence, string word, float x, float y)> PlaceRpc = new("TanukiVoiceStonePlaceV1",
        (m, _) => m.player.Role.GetAbility<Ability>()?.Place(m.sequence, m.word, m.x, m.y));
    public sealed class Ability : AbstractPlayerAbility, IPlayerAbility
    {
        private static int nextSequence;
        private readonly VoiceStoneRequest request = new();
        private readonly List<VoiceStoneVisual> stones = new();
        private ModAbilityButton? button;
        private double nextUse;
        private double Now => UnityEngine.Time.unscaledTimeAsDouble;
        private bool Alive => !MyPlayer.IsDead && !MyPlayer.IsDisconnected;
        private bool Listening => request.Waiting && Now < request.Expires;
        private bool Eligible => Alive && MyPlayer.CanMove && AmongUsClient.Instance != null &&
            AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started &&
            !HeroSpeechHost.IsPresent(MeetingHud.Instance) && !HeroSpeechHost.IsPresent(IntroCutscene.Instance) &&
            !HeroSpeechHost.IsPresent(ExileController.Instance);
        public bool SpeechEligible => Eligible;
        public object SpeechJob => new { playerId = MyPlayer.PlayerId, kind = "stone", request = Listening ? request.Sequence : 0,
            expires = Listening ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (long)((request.Expires - Now) * 1000) : 0,
            words = Array.Empty<string>(), maxCharacters = (int)Maximum };
        public Ability(Player player) : base(player)
        {
            if (!AmOwner) return;
            button = NebulaAPI.Modules.AbilityButton(this, player, VirtualKeyInput.FixedAbility, Cooldown,
                "tanukiVoiceStone.arm", VoiceStoneVisual.Icon,
                _ => Eligible && player.CanMove && !request.Waiting && Now >= nextUse && HeroSpeechHost.ReadyFor(player.PlayerId),
                _ => Alive);
            button.EffectTimer = new HeroTransformTimer(() => Math.Max(0, request.Expires - Now), () => (float)Wait, () => Listening);
            button.OnClick = _ => ArmRpc.Invoke((MyPlayer, ++nextSequence));
            button.StartCoolDown();
        }
        internal void Begin(int sequence)
        {
            if (!Eligible || Now + .75 < nextUse || !request.Arm(sequence, Now, Wait)) return;
            nextUse = Now + (float)Cooldown;
            if (AmOwner) { button?.StartEffect(); button?.StartCoolDown(); }
        }
        public void RecogniseSpeech(int sequence, string word)
        {
            if (!Eligible || !request.Waiting || sequence != request.Sequence || Now >= request.Expires ||
                !VoiceStoneRules.TryWord(word, Maximum, out string accepted)) return;
            var position = MyPlayer.TruePosition;
            PlaceRpc.Invoke((MyPlayer, sequence, accepted, position.x, position.y));
        }
        internal void Place(int sequence, string word, float x, float y)
        {
            if (!Eligible || !float.IsFinite(x) || !float.IsFinite(y)) return;
            var position = MyPlayer.TruePosition;
            if (Math.Abs(position.x - x) > 2f || Math.Abs(position.y - y) > 2f ||
                !request.Accept(sequence, Now, word, Maximum, out string accepted, 1)) return;
            while (stones.Count >= (int)Limit) { stones[0].Dispose(); stones.RemoveAt(0); }
            CreateStone(accepted, x, y);
            StopListening();
        }
        private void CreateStone(string word, float x, float y)
        {
            try { stones.Add(new VoiceStoneVisual(word, x, y, CharacterWidth, Lifetime)); }
            catch (Exception ex) { NebulaAPI.Logging.NebulaLogger("TanukiVoiceStone").Error(ex.ToString()); }
        }
        private void Update(GameUpdateEvent ev)
        {
            // Stop accepting local recognition at the exact deadline. Keep the
            // RPC pending briefly so an in-time result can reach slower peers.
            if (request.Waiting && (!Eligible || Now >= request.Expires + 1)) StopListening();
            foreach (var stone in stones) stone.Update();
            stones.RemoveAll(stone => stone.Expired);
            if (MyPlayer.IsDisconnected) Clear();
            if (AmOwner) button?.SetLabel(Listening ? "tanukiVoiceStone.listening" : "tanukiVoiceStone.arm");
        }
        private void StopListening() { request.Cancel(); if (AmOwner) button?.InterruptEffect(); }
        private void Clear() { StopListening(); foreach (var stone in stones) stone.Dispose(); stones.Clear(); }
        private void OnMeeting(MeetingStartEvent ev) => Clear();
        private void OnGameEnd(GameEndEvent ev) => Clear();
        void IGameOperator.OnReleased() => Clear();
    }
}

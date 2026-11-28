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
    private static readonly RemoteProcess<(Player player, int sequence, bool neural)> ArmRpc = new("TanukiVoiceStoneArmV3",
        (m, _) => m.player.Role.GetAbility<Ability>()?.Begin(m.sequence, m.neural));
    private static readonly RemoteProcess<(Player player, int sequence, string word, float x, float y)> PlaceRpc = new("TanukiVoiceStonePlaceV1",
        (m, _) => m.player.Role.GetAbility<Ability>()?.Place(m.sequence, m.word, m.x, m.y));
    public sealed class Ability : AbstractPlayerAbility, IPlayerAbility
    {
        private static int nextSequence;
        private readonly VoiceStoneRequest request = new();
        private readonly List<VoiceStoneVisual> stones = new();
        private ModAbilityButton? button;
        private IVisualTimer? normalCooldown, retryCooldown;
        private double nextUse;
        private double Now => UnityEngine.Time.unscaledTimeAsDouble;
        private bool Alive => !MyPlayer.IsDead && !MyPlayer.IsDisconnected;
        private bool Listening => request.Waiting && Now < request.RecordUntil;
        private bool Available => Alive && AmongUsClient.Instance != null &&
            AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started &&
            !HeroSpeechHost.IsPresent(MeetingHud.Instance) && !HeroSpeechHost.IsPresent(IntroCutscene.Instance) &&
            !HeroSpeechHost.IsPresent(ExileController.Instance);
        private bool Eligible => Available && MyPlayer.CanMove;
        public bool SpeechEligible => Available;
        public object SpeechJob => new { playerId = MyPlayer.PlayerId, kind = "stone", request = request.Waiting ? request.Sequence : 0,
            expires = request.Waiting ? request.SpeechExpiresUtc : 0,
            recognizer = (request.Waiting ? request.LocalNeural : HeroSpeechHost.NeuralFor(MyPlayer.PlayerId)) ? "local-neural" : "local", recordUntil = request.Waiting ? request.RecordUntilUtc : 0, recordStarted = request.Waiting ? request.RecordStartedUtc : 0,
            words = Array.Empty<string>(), maxCharacters = (int)Maximum };
        public Ability(Player player) : base(player)
        {
            if (!AmOwner) return;
            button = NebulaAPI.Modules.AbilityButton(this, player, VirtualKeyInput.FixedAbility, Cooldown,
                "tanukiVoiceStone.arm", VoiceStoneVisual.Icon,
                _ => Eligible && !request.Waiting && Now >= nextUse && HeroSpeechHost.ReadyFor(player.PlayerId),
                _ => Alive);
            button.EffectTimer = new HeroTransformTimer(() => Math.Max(0, request.RecordUntil - Now), () => (float)Wait, () => Listening);
            normalCooldown = button.CoolDownTimer;
            retryCooldown = NebulaAPI.Modules.Timer(this, .5f);
            button.OnClick = _ => ArmRpc.Invoke((MyPlayer, ++nextSequence, HeroSpeechHost.NeuralFor(MyPlayer.PlayerId)));
            button.StartCoolDown();
        }
        internal void Begin(int sequence, bool neural)
        {
            if (!Eligible || Now + .75 < nextUse || !request.Arm(sequence, Now, Wait, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), neural)) return;
            nextUse = Now + (float)Cooldown;
            Log("Listening request=" + sequence);
            if (AmOwner && button != null) { button.CoolDownTimer = normalCooldown; button.StartEffect(); button.StartCoolDown(); }
        }
        public void RecogniseSpeech(int sequence, string word)
        {
            bool validWord = VoiceStoneRules.TryWord(word, Maximum, out string accepted);
            Log("Result received request=" + sequence + "; expected=" + request.Sequence +
                "; waiting=" + request.Waiting + "; eligible=" + Eligible + "; expired=" + (Now >= request.Expires) +
                "; validWord=" + validWord + "; maximum=" + (int)Maximum + "; characters=" + VoiceStoneRules.Count(word));
            if (!Eligible || !request.Waiting || sequence != request.Sequence || Now >= request.Expires ||
                !validWord) return;
            var position = MyPlayer.TruePosition;
            PlaceRpc.Invoke((MyPlayer, sequence, accepted, position.x, position.y));
        }
        private void Log(string message) => NebulaAPI.Logging.NebulaLogger("TanukiVoiceStone").Message(
            "Player=" + MyPlayer.PlayerId + "; " + message);
        // RPC callbacks belong to the containing role's generated closure,
        // so their entry points must also be accessible at runtime. NoS's
        // hidden-member compiler otherwise accepts calls the CLR rejects.
        internal void Place(int sequence, string word, float x, float y)
        {
            if (!Available || !float.IsFinite(x) || !float.IsFinite(y)) return;
            var position = MyPlayer.TruePosition;
            if (Math.Abs(position.x - x) > 2f || Math.Abs(position.y - y) > 2f ||
                !request.Accept(sequence, Now, word, Maximum, out string accepted, 1)) return;
            while (stones.Count >= (int)Limit) { stones[0].Dispose(); stones.RemoveAt(0); }
            CreateStone(accepted, x, y);
            Log("Placed request=" + sequence);
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
            if (request.Waiting && (!Available || Now >= request.Expires + 1))
            {
                bool failed = request.Waiting && Available;
                Log("Listening ended without a result");
                StopListening();
                if (failed)
                {
                    nextUse = Now + .5;
                    if (AmOwner && button != null) { button.CoolDownTimer = retryCooldown; button.StartCoolDown(); }
                }
            }
            foreach (var stone in stones) stone.Update();
            stones.RemoveAll(stone => stone.Expired);
            if (MyPlayer.IsDisconnected) Clear();
            if (AmOwner) button?.SetLabel(Listening ? (request.LocalNeural ? "tanukiVoiceStone.localRecording" : "tanukiVoiceStone.listening") : request.Waiting ? "tanukiVoiceStone.processing" : "tanukiVoiceStone.arm");
        }
        private void StopListening() { request.Cancel(); if (AmOwner) button?.InterruptEffect(); }
        private void Clear() { StopListening(); foreach (var stone in stones) stone.Dispose(); stones.Clear(); }
        private void OnMeeting(MeetingStartEvent ev) => Clear();
        private void OnGameEnd(GameEndEvent ev) => Clear();
        void IGameOperator.OnReleased() => Clear();
    }
}

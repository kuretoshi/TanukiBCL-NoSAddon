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
    private static readonly RemoteProcess<(Player player, int sequence, string word)> PreviewRpc = new("TanukiVoiceStonePreviewV1",
        (m, _) => m.player.Role.GetAbility<Ability>()?.Preview(m.sequence, m.word));
    private static readonly RemoteProcess<(Player player, int sequence, int next, bool neural)> RetryRpc = new("TanukiVoiceStoneRetryV3",
        (m, _) => m.player.Role.GetAbility<Ability>()?.Retry(m.sequence, m.next, m.neural));
    private static readonly RemoteProcess<(Player player, int sequence)> CancelRpc = new("TanukiVoiceStoneCancelV1",
        (m, _) => m.player.Role.GetAbility<Ability>()?.Cancel(m.sequence));
    public sealed class Ability : AbstractPlayerAbility, IPlayerAbility
    {
        private static int nextSequence;
        private readonly VoiceStoneRequest request = new();
        private readonly List<VoiceStoneVisual> stones = new();
        private ModAbilityButton? button;
        private MetaScreen? confirmation;
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
                _ => Eligible && !request.Waiting && !request.Confirming && Now >= nextUse && HeroSpeechHost.ReadyFor(player.PlayerId),
                _ => Alive);
            button.EffectTimer = new HeroTransformTimer(() => Math.Max(0, request.RecordUntil - Now), () => (float)Wait, () => Listening);
            button.OnClick = _ => ArmRpc.Invoke((MyPlayer, ++nextSequence, HeroSpeechHost.NeuralFor(MyPlayer.PlayerId)));
            button.StartCoolDown();
        }
        internal void Begin(int sequence, bool neural)
        {
            if (!Eligible || Now + .75 < nextUse || !request.Arm(sequence, Now, Wait, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), neural)) return;
            nextUse = Now + (float)Cooldown;
            Log("Listening request=" + sequence);
            if (AmOwner) { button?.StartEffect(); button?.StartCoolDown(); }
        }
        public void RecogniseSpeech(int sequence, string word)
        {
            bool validWord = VoiceStoneRules.TryWord(word, Maximum, out string accepted);
            Log("Result received request=" + sequence + "; expected=" + request.Sequence +
                "; waiting=" + request.Waiting + "; eligible=" + Eligible + "; expired=" + (Now >= request.Expires) +
                "; validWord=" + validWord + "; maximum=" + (int)Maximum + "; characters=" + VoiceStoneRules.Count(word));
            if (!Eligible || !request.Waiting || sequence != request.Sequence || Now >= request.Expires ||
                !validWord) return;
            PreviewRpc.Invoke((MyPlayer, sequence, accepted));
        }
        // RPC callbacks belong to the containing role's generated closure,
        // so their entry points must also be accessible at runtime. NoS's
        // hidden-member compiler otherwise accepts calls the CLR rejects.
        internal void Preview(int sequence, string word)
        {
            if (!Available || !request.Propose(sequence, Now, word, Maximum, 1)) return;
            Log("Recognized request=" + sequence + "; characters=" + VoiceStoneRules.Count(word));
            if (!AmOwner) return;
            button?.InterruptEffect();
            ShowConfirmation();
        }
        private static string T(string key) => NebulaAPI.Language.Translate("tanuki.voiceStone." + key);
        private void Log(string message) => NebulaAPI.Logging.NebulaLogger("TanukiVoiceStone").Message(
            "Player=" + MyPlayer.PlayerId + "; " + message);
        private static Virial.Text.TextAttribute Attr(float height = .45f) =>
            new(NebulaAPI.GUI.GetAttribute(Virial.Text.AttributeAsset.OptionsTitleHalf))
            { Font = NebulaAPI.GUI.GetFont(Virial.Text.FontAsset.Gothic), Size = new(5f, height), Wrapping = true,
              Alignment = Virial.Text.TextAlignment.Center, FontSize = new(1.6f, 1.3f, 1.6f) };
        private void ShowConfirmation()
        {
            if (confirmation != null || !request.Confirming) return;
            var parent = Nebula.Utilities.UnityHelper.FindCamera(UnityEngine.LayerMask.NameToLayer("UI"))?.transform;
            if (parent == null) return;
            confirmation = MetaScreen.GenerateWindow(new UnityEngine.Vector2(5.8f, 4.2f), parent,
                UnityEngine.Vector3.zero, true, false, withCloseButton: false);
            Log("Confirmation opened request=" + request.Sequence);
            confirmation.SetWidget(NebulaAPI.GUI.VerticalHolder(Virial.Media.GUIAlignment.Center,
                NebulaAPI.GUI.RawText(Virial.Media.GUIAlignment.Center, Attr(), T("title")),
                NebulaAPI.GUI.RawText(Virial.Media.GUIAlignment.Center, Attr(.8f), request.Candidate),
                NebulaAPI.GUI.RawText(Virial.Media.GUIAlignment.Center, Attr(.65f), T("hint")),
                NebulaAPI.GUI.RawButton(Virial.Media.GUIAlignment.Center, Attr(), T("place"), _ => Confirm()),
                NebulaAPI.GUI.RawButton(Virial.Media.GUIAlignment.Center, Attr(), T("retry"),
                    _ => RetryRpc.Invoke((MyPlayer, request.Sequence, ++nextSequence, HeroSpeechHost.NeuralFor(MyPlayer.PlayerId)))),
                NebulaAPI.GUI.RawButton(Virial.Media.GUIAlignment.Center, Attr(), T("cancel"),
                    _ => CancelRpc.Invoke((MyPlayer, request.Sequence)))), new Virial.Compat.Vector2(.5f, .5f), out _);
        }
        private void Confirm()
        {
            if (!AmOwner || !Available || !request.Confirming || Now >= request.Expires) return;
            var position = MyPlayer.TruePosition;
            PlaceRpc.Invoke((MyPlayer, request.Sequence, request.Candidate, position.x, position.y));
        }
        internal void Retry(int sequence, int next, bool neural)
        {
            if (!Available || !request.Retry(sequence, next, Now, Wait, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), neural)) return;
            Log("Listening retry=" + next);
            CloseConfirmation();
            if (AmOwner) button?.StartEffect();
        }
        internal void Cancel(int sequence)
        {
            if (sequence == request.Sequence) { Log("Cancelled request=" + sequence); StopListening(); }
        }
        private void CloseConfirmation()
        {
            if (confirmation != null) confirmation.CloseScreen();
            confirmation = null;
        }
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
            if ((request.Waiting || request.Confirming) && (!Available || Now >= request.Expires + 1))
            {
                Log(request.Waiting ? "Listening ended without a result" : "Confirmation ended");
                StopListening();
            }
            if (AmOwner && request.Confirming && Available) ShowConfirmation();
            foreach (var stone in stones) stone.Update();
            stones.RemoveAll(stone => stone.Expired);
            if (MyPlayer.IsDisconnected) Clear();
            if (AmOwner) button?.SetLabel(Listening ? (request.LocalNeural ? "tanukiVoiceStone.localRecording" : "tanukiVoiceStone.listening") : request.Waiting ? "tanukiVoiceStone.processing" : "tanukiVoiceStone.arm");
        }
        private void StopListening() { request.Cancel(); CloseConfirmation(); if (AmOwner) button?.InterruptEffect(); }
        private void Clear() { StopListening(); foreach (var stone in stones) stone.Dispose(); stones.Clear(); }
        private void OnMeeting(MeetingStartEvent ev) => Clear();
        private void OnGameEnd(GameEndEvent ev) => Clear();
        void IGameOperator.OnReleased() => Clear();
    }
}

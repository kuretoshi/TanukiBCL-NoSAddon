using System;
using System.Linq;
using Nebula.Modules;
using Nebula.Roles.Crewmate;
using Nebula.Utilities;
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
using UnityEngine;
using Player = Virial.Game.Player;
using Vector2 = UnityEngine.Vector2;

namespace TanukiBCL.Roles;

public sealed class Interviewer : DefinedSingleAbilityRoleTemplate<Interviewer.Ability>, DefinedRole, IAssignableDocument
{
    private static readonly FloatConfiguration Seconds = NebulaAPI.Configurations.Configuration(
        "options.role.tanukiInterviewer.seconds", (1f, 30f, 1f), 5f, FloatConfigurationDecorator.Second);
    private static readonly FloatConfiguration Cooldown = NebulaAPI.Configurations.Configuration(
        "options.role.tanukiInterviewer.cooldown", (0f, 120f, 5f), 20f, FloatConfigurationDecorator.Second);
    private static readonly IntegerConfiguration PlaybackLimit = NebulaAPI.Configurations.Configuration(
        "options.role.tanukiInterviewer.playbackLimit", (0, 10), 0, null,
        count => count == 0 ? NebulaAPI.Language.Translate("options.noLimit") : count.ToString());
    private static readonly BoolConfiguration DisableInComms = NebulaAPI.Configurations.Configuration(
        "options.role.tanukiInterviewer.disableInComms", false);
    public static readonly Interviewer MyRole = new();
    private Interviewer() : base("tanukiInterviewer", new Virial.Color(80, 190, 225), RoleCategory.CrewmateRole, Crewmate.MyTeam,
        new IConfiguration[] { Seconds, Cooldown, PlaybackLimit, DisableInComms })
    {
        ConfigurationHolder!.Illustration = DyingVoiceVisual.Icon;
        ConfigurationHolder.AddTags(AddonRoleTag.Tag);
    }
    Virial.Media.Image? DefinedAssignable.IconImage => DyingVoiceVisual.Icon;
    bool IAssignableDocument.HasTips => true;
    public override Ability CreateAbility(Player player, int[] arguments) => new(player);
    AbilityAssignmentStatus DefinedRole.AssignmentStatus => AbilityAssignmentStatus.CanLoadToMadmate;
    MultipleAssignmentType DefinedRole.MultipleAssignment => MultipleAssignmentType.Allowed;

    private static readonly RemoteProcess<(Player player, int sequence, ulong started, float seconds, byte[] visible)> CaptureRpc =
        new("TanukiInterviewCaptureV1", (m, _) => m.player.Role.GetAbility<Ability>()?.Capture(m.sequence, (long)m.started, m.seconds, m.visible));
    private static readonly RemoteProcess<(Player player, int sequence, byte[] visible)> VisibleRpc =
        new("TanukiInterviewVisibleV1", (m, _) => m.player.Role.GetAbility<Ability>()?.SetVisible(m.sequence, m.visible));
    private static readonly RemoteProcess<(Player player, int sequence, ulong stopped)> StopRpc =
        new("TanukiInterviewStopV1", (m, _) => m.player.Role.GetAbility<Ability>()?.Stop(m.sequence, (long)m.stopped));
    private static readonly RemoteProcess<(Player player, int sequence, ulong started)> PlayRpc =
        new("TanukiInterviewPlayV1", (m, _) => m.player.Role.GetAbility<Ability>()?.Play(m.sequence, (long)m.started));

    public sealed class Ability : AbstractPlayerAbility, IPlayerAbility
    {
        private static int nextSequence;
        private int sequence;
        private long sourceStarted;
        private readonly InterviewerPlaybackLimit playback = new();
        private readonly InterviewState state;
        private ModAbilityButton? button;
        private InterviewerMeetingButton? meetingButton;
        private float nextVisibility;
        private static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        private bool Recording => InterviewerTiming.IsRecording(state.started, state.stopped, state.seconds, Now);
        private bool Ready => state.started > 0 && state.stopped > state.started;
        private bool Playing => state.playStarted > 0 && Now < state.playStarted + state.stopped - state.started;
        private bool CommsBlocked => DisableInComms && AmongUsUtil.InCommSab;
        private bool Eligible => !MyPlayer.IsDead && !MyPlayer.IsDisconnected && MeetingHud.Instance == null &&
            ExileController.Instance == null && AmongUsClient.Instance != null &&
            AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started;

        public Ability(Player player) : base(player)
        {
            state = new InterviewState { playerId = player.PlayerId };
            if (!AmOwner) return;
            button = NebulaAPI.Modules.AbilityButton(this, player, VirtualKeyInput.FixedAbility, (float)Cooldown,
                "tanukiInterviewer.record", DyingVoiceVisual.Icon, _ => Eligible && !CommsBlocked && player.CanMove && !Recording,
                _ => !player.IsDead);
            button.EffectTimer = new VoiceEnergyTimer(() => (float)Math.Max(0, 1 - (Now - state.started) / Math.Max(1, state.seconds * 1000d)), () => Recording);
            button.OnClick = _ => StartRecording();
            button.StartCoolDown();
        }
        private void StartRecording()
        {
            if (!Eligible || CommsBlocked || Recording || !MyPlayer.CanMove) return;
            CaptureRpc.Invoke((MyPlayer, ++nextSequence, (ulong)Now, Seconds, VisiblePlayers()));
        }
        internal void Capture(int id, long started, float seconds, byte[] visible)
        {
            if (id <= sequence || started <= 0 || MyPlayer.IsDead || MeetingHud.Instance != null || seconds < 1 || seconds > 30 || !float.IsFinite(seconds)) return;
            sequence = id;
            // Each receiving PC owns its IPC clock. Do not require participants'
            // operating-system clocks to agree with the recording player's clock.
            sourceStarted = started;
            state.started = Now; state.seconds = seconds; state.stopped = 0; state.playStarted = 0;
            state.visible = visible.Distinct().ToArray();
            if (AmOwner) { button?.SetLabel("tanukiInterviewer.recording"); button?.StartEffect(); }
            Publish();
        }
        internal void SetVisible(int id, byte[] visible)
        {
            if (id != sequence || state.stopped != 0) return;
            state.visible = visible.Distinct().ToArray();
        }
        internal void Stop(int id, long stopped)
        {
            if (id != sequence || state.started == 0 || state.stopped != 0) return;
            state.stopped = InterviewerTiming.CapturedEnd(state.started, sourceStarted, stopped, state.seconds);
            state.visible = Array.Empty<byte>();
            if (AmOwner) { button?.SetLabel("tanukiInterviewer.record"); button?.InterruptEffect(); button?.StartCoolDown(); }
            Publish();
        }
        internal void Play(int id, long started)
        {
            if (id != sequence || !Ready || MyPlayer.IsDead || MeetingHud.Instance == null || !playback.TryUse(PlaybackLimit, started)) return;
            state.playStarted = Now;
            Publish();
        }
        private void RequestPlayback() { if (Ready && !Playing && !MyPlayer.IsDead && playback.Remaining(PlaybackLimit) != 0) PlayRpc.Invoke((MyPlayer, sequence, (ulong)Now)); }
        private void Publish() { if (state.started > 0) InterviewerBroadcast.Set(state); }

        private byte[] VisiblePlayers()
        {
            var origin = MyPlayer.TruePosition;
            float radius = ShipStatus.Instance.CalculateLightRadius(PlayerControl.LocalPlayer.Data);
            return NebulaAPI.CurrentGame!.GetAllPlayers().Where(p => p.AmOwner || CanSee(p, origin.x, origin.y, radius))
                .Select(p => p.PlayerId).OrderBy(id => id).ToArray();
        }
        private static bool CanSee(Player target, float x, float y, float radius)
        {
            if (target.IsDead || target.IsDisconnected || target.IsInvisible || target.IsDived) return false;
            var pos = target.TruePosition;
            var delta = new Vector2(pos.x - x, pos.y - y);
            if (delta.magnitude > radius) return false;
            return !Physics2D.Linecast(new Vector2(x, y), new Vector2(pos.x, pos.y), Constants.ShadowMask);
        }
        private void Update(GameUpdateEvent ev)
        {
            if (MyPlayer.IsDisconnected) { Clear(); return; }
            if (AmOwner) UpdateOwner();
            Publish();
            if (meetingButton != null) meetingButton.Update(!MyPlayer.IsDead && MeetingHud.Instance != null, Ready, Playing, playback.Remaining(PlaybackLimit));
        }
        private void UpdateOwner()
        {
            if (!Recording) button?.SetLabel(CommsBlocked ? "tanukiInterviewer.comms" : "tanukiInterviewer.record");
            if (state.started == 0 || state.stopped != 0) return;
            if (!Eligible || CommsBlocked || !Recording) { StopRpc.Invoke((MyPlayer, sequence, (ulong)Now)); return; }
            if (Time.time < nextVisibility) return;
            nextVisibility = Time.time + .1f;
            var visible = VisiblePlayers();
            if (!visible.SequenceEqual(state.visible)) VisibleRpc.Invoke((MyPlayer, sequence, visible));
        }
        private void OnMeeting(MeetingPreStartEvent ev) { if (AmOwner && Recording) StopRpc.Invoke((MyPlayer, sequence, (ulong)Now)); }
        private void OnMeetingStart(MeetingStartEvent ev)
        {
            playback.ResetMeeting();
            if (!AmOwner || MyPlayer.IsDead || MeetingHud.Instance == null) return;
            meetingButton?.Dispose();
            meetingButton = new InterviewerMeetingButton(MeetingHud.Instance.transform, RequestPlayback);
        }
        private void OnMeetingEnd(MeetingEndEvent ev) { meetingButton?.Dispose(); meetingButton = null; }
        [OnlyMyPlayer]
        private void OnDeath(PlayerDieOrDisconnectEvent ev) { if (AmOwner && Recording) StopRpc.Invoke((MyPlayer, sequence, (ulong)Now)); }
        private void OnGameEnd(GameEndEvent ev) => Clear();
        private void Clear()
        {
            InterviewerBroadcast.Remove(MyPlayer.PlayerId);
            meetingButton?.Dispose(); meetingButton = null;
            state.started = 0; state.visible = Array.Empty<byte>();
        }
        void IGameOperator.OnReleased() => Clear();
    }
}

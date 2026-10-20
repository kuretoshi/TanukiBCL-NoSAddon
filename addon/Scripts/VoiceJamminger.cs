using System;
using System.Diagnostics;
using System.Linq;
using Nebula.Modules;
using Nebula.Roles.Impostor;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Compat;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Game;
using Player = Virial.Game.Player;

namespace TanukiBCL.Roles;

public sealed class VoiceJamminger : DefinedSingleAbilityRoleTemplate<VoiceJamminger.Ability>, DefinedRole, IAssignableDocument
{
    private static readonly FloatConfiguration Cooldown = NebulaAPI.Configurations.Configuration(
        "options.role.tanukiVoiceJamminger.cooldown", (0f, 120f, 5f), 25f, FloatConfigurationDecorator.Second);
    private static readonly ValueConfiguration<int> TargetMode = NebulaAPI.Configurations.Configuration(
        "options.role.tanukiVoiceJamminger.mode", new[] { "options.role.tanukiVoiceJamminger.mute", "options.role.tanukiVoiceJamminger.lowpass" }, 0);
    private static readonly IntegerConfiguration Uses = NebulaAPI.Configurations.Configuration(
        "options.role.tanukiVoiceJamminger.uses", (0, 10), 2, null,
        count => count == 0 ? NebulaAPI.Language.Translate("options.noLimit") : count.ToString());
    private static readonly FloatConfiguration PitchSeconds = NebulaAPI.Configurations.Configuration(
        "options.role.tanukiVoiceJamminger.seconds", (1f, 120f, 1f), 10f, FloatConfigurationDecorator.Second);
    public static readonly VoiceJamminger MyRole = new();

    private VoiceJamminger() : base("tanukiVoiceJamminger", Virial.Color.ImpostorColor, RoleCategory.ImpostorRole, Impostor.MyTeam,
        new IConfiguration[] { Cooldown, TargetMode, PitchSeconds, Uses })
    {
        ConfigurationHolder!.Illustration = VoiceJammingerIcon.Icon;
        ConfigurationHolder.AddTags(AddonRoleTag.Tag);
    }
    Virial.Media.Image? DefinedAssignable.IconImage => VoiceJammingerIcon.Icon;
    bool IAssignableDocument.HasTips => true;
    public override Ability CreateAbility(Player player, int[] arguments) => new(player);
    AbilityAssignmentStatus DefinedRole.AssignmentStatus => AbilityAssignmentStatus.Killers;
    MultipleAssignmentType DefinedRole.MultipleAssignment => MultipleAssignmentType.AsUniqueKillAbility;

    private static readonly RemoteProcess<(Player player, int sequence, Player target)> MarkRpc = new("TanukiVoiceJamMarkV1",
        (m, _) => m.player.Role.GetAbility<Ability>()?.Mark(m.sequence, m.target));
    private static readonly RemoteProcess<(Player player, int sequence, int seed, ulong priority)> PitchRpc = new("TanukiVoiceJamPitchV1",
        (m, _) => m.player.Role.GetAbility<Ability>()?.Pitch(m.sequence, m.seed, (long)m.priority));

    public sealed class Ability : AbstractPlayerAbility, IPlayerAbility
    {
        private static int nextSequence;
        private readonly VoiceJammingRound round = new();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private double Now => clock.Elapsed.TotalSeconds;
        private VoiceJammingMeetingButton? meetingButton;
        private float nextPublish;
        private bool Living => !MyPlayer.IsDead && !MyPlayer.IsDisconnected;
        private bool CanMark => Living && MeetingHud.Instance == null && ExileController.Instance == null &&
            AmongUsClient.Instance != null && AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started;

        public Ability(Player player) : base(player)
        {
            if (!AmOwner) return;
            var tracker = NebulaAPI.Modules.PlayerTracker(this, player, p => p != player && !p.IsDead && !p.IsDisconnected);
            var button = NebulaAPI.Modules.InteractButton(this, player, tracker, VirtualKeyInput.FixedAbility, null,
                Cooldown, "tanukiVoiceJamminger.mark", VoiceJammingerIcon.Icon,
                (target, usedButton) => {
                    MarkRpc.Invoke((player, ++nextSequence, target));
                    usedButton.StartCoolDown();
                },
                _ => CanMark && player.CanMove, _ => Living);
            button.StartCoolDown();
        }
        internal void Mark(int sequence, Player target)
        {
            if (!CanMark || target == MyPlayer || target.IsDead || target.IsDisconnected) return;
            round.Mark(sequence, target.PlayerId);
        }
        private void RequestPitch()
        {
            if (!Living || MeetingHud.Instance == null || !round.CanPitch(Uses)) return;
            PitchRpc.Invoke((MyPlayer, ++nextSequence, Random.Shared.Next(1, int.MaxValue),
                (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        }
        internal void Pitch(int sequence, int seed, long priority)
        {
            if (!Living || MeetingHud.Instance == null) return;
            round.Pitch(sequence, seed, priority, Uses, Now, PitchSeconds);
        }
        private void OnMeetingStart(MeetingStartEvent ev)
        {
            round.BeginMeeting();
            if (!AmOwner || !Living || MeetingHud.Instance == null) return;
            meetingButton?.Dispose();
            meetingButton = new VoiceJammingMeetingButton(MeetingHud.Instance.transform, RequestPitch);
        }
        private void Update(GameUpdateEvent ev)
        {
            meetingButton?.Update(Living && MeetingHud.Instance != null, round.PitchRemaining(Now), round.Remaining(Uses));
            if (MyPlayer.IsDisconnected) { EndMeeting(); return; }
            if (!round.InMeeting || MeetingHud.Instance == null) return;
            if (UnityEngine.Time.unscaledTime < nextPublish) return;
            nextPublish = UnityEngine.Time.unscaledTime + .1f;
            var target = round.ActiveTarget;
            var players = NebulaAPI.CurrentGame!.GetAllPlayers().Where(p => !p.IsDisconnected).ToArray();
            if (!players.Any(p => p.PlayerId == target && !p.IsDead)) target = null;
            VoiceJammingBroadcast.Set(MyPlayer.PlayerId, target, TargetMode.GetValue(), round.PitchActive(Now) ? round.Seed : 0,
                round.Priority, players.Select(p => p.PlayerId).ToArray());
        }
        private void OnMeetingEnd(MeetingEndEvent ev) => EndMeeting();
        private void EndMeeting()
        {
            round.EndMeeting();
            meetingButton?.Dispose(); meetingButton = null;
            VoiceJammingBroadcast.Remove(MyPlayer.PlayerId);
        }
        private void OnGameEnd(GameEndEvent ev) => EndMeeting();
        void IGameOperator.OnReleased() => EndMeeting();
    }
}

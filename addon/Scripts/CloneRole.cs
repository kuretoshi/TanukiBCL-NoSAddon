using System;
using Nebula.Modules;
using Nebula.Roles.Crewmate;
using Virial;
using Virial.Assignable;
using Virial.Attributes;
using Virial.Compat;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Player = Virial.Game.Player;

namespace TanukiBCL.Roles;

public sealed class CloneRole : DefinedSingleAbilityRoleTemplate<CloneRole.Ability>, DefinedRole, IAssignableDocument
{
    private static readonly FloatConfiguration Cooldown = NebulaAPI.Configurations.Configuration(
        "options.role.tanukiClone.cooldown", (0f, 120f, 5f), 30f, FloatConfigurationDecorator.Second);
    private static readonly FloatConfiguration Range = NebulaAPI.Configurations.Configuration(
        "options.role.tanukiClone.range", (1f, 10f, .5f), 3.5f);
    private static readonly BoolConfiguration SpoofTalking = NebulaAPI.Configurations.Configuration(
        "options.role.tanukiClone.spoofTalking", true);
    public static readonly CloneRole MyRole = new();
    private CloneRole() : base("tanukiClone", new Virial.Color(170, 130, 240), RoleCategory.CrewmateRole, Crewmate.MyTeam,
        new IConfiguration[] { Cooldown, Range, SpoofTalking })
    {
        ConfigurationHolder!.Illustration = CloneRuntime.Icon; ConfigurationHolder.AddTags(AddonRoleTag.Tag);
    }
    Virial.Media.Image? DefinedAssignable.IconImage => CloneRuntime.Icon;
    bool IAssignableDocument.HasTips => true;
    public override Ability CreateAbility(Player player, int[] arguments) => new(player);
    AbilityAssignmentStatus DefinedRole.AssignmentStatus => AbilityAssignmentStatus.CanLoadToMadmate;
    MultipleAssignmentType DefinedRole.MultipleAssignment => MultipleAssignmentType.Allowed;
    private static readonly RemoteProcess<(Player owner, int id)> RecordRpc = new("TanukiCloneRecordV1",
        (m, _) => m.owner.Role.GetAbility<Ability>()?.Record(m.id));
    private static readonly RemoteProcess<(Player owner, int id, int fake)> SummonRpc = new("TanukiCloneSummonV1",
        (m, _) => m.owner.Role.GetAbility<Ability>()?.Summon(m.id, m.fake));
    private static readonly RemoteProcess<(Player owner, int id)> DespawnRpc = new("TanukiCloneDespawnV1",
        (m, _) => m.owner.Role.GetAbility<Ability>()?.Despawn(m.id));
    public sealed class Ability : AbstractPlayerAbility, IPlayerAbility
    {
        private static int nextRecord, nextSpawn;
        private readonly CloneRound round = new();
        private CloneRuntime? runtime;
        private ModAbilityButton? record, summon;
        private static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        private static double Time => UnityEngine.Time.unscaledTimeAsDouble;
        private bool Alive => !MyPlayer.IsDead && !MyPlayer.IsDisconnected;
        private bool Tasks => Alive && AmongUsClient.Instance != null &&
            AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started &&
            !HeroSpeechHost.IsPresent(MeetingHud.Instance) && !HeroSpeechHost.IsPresent(ExileController.Instance) &&
            !HeroSpeechHost.IsPresent(IntroCutscene.Instance);
        public Ability(Player player) : base(player)
        {
            if (!AmOwner) return;
            record = NebulaAPI.Modules.AbilityButton(this, player, VirtualKeyInput.FixedAbility, 0f,
                "tanukiClone.record", DyingVoiceVisual.Icon, _ => Tasks && player.CanMove && !round.Recording, _ => Alive);
            record.EffectTimer = new HeroTransformTimer(() => round.Remaining(Now), () => CloneRound.Seconds, () => round.Recording);
            record.OnClick = _ => RecordRpc.Invoke((MyPlayer, ++nextRecord));
            summon = NebulaAPI.Modules.AbilityButton(this, player, VirtualKeyInput.SecondaryAbility, 0f,
                "tanukiClone.summon", CloneRuntime.Icon, _ => Tasks && player.CanMove && round.CanSummon(Time) && runtime == null, _ => Alive);
            summon.CoolDownTimer = new HeroTransformTimer(() => round.CooldownRemaining(Time), () => Cooldown,
                () => round.CooldownRemaining(Time) > 0);
            summon.OnClick = _ => CreateClone();
        }
        internal void Record(int id)
        {
            if (!Tasks || !round.Begin(id, Now, Time, Cooldown)) return;
            runtime?.Dispose(); runtime = null;
            if (AmOwner) record?.StartEffect();
            Publish();
        }
        private void CreateClone()
        {
            if (!AmOwner || !Tasks || !round.CanSummon(Time) || runtime != null) return;
            try
            {
                runtime = new CloneRuntime(MyPlayer, this);
                SummonRpc.Invoke((MyPlayer, ++nextSpawn, runtime.Id));
            }
            catch (Exception ex)
            {
                runtime?.Dispose(); runtime = null;
                NebulaAPI.Logging.NebulaLogger("TanukiClone").Error(ex.ToString());
            }
        }
        internal void Summon(int id, int fake)
        {
            if (!Tasks || (fake & 255) != MyPlayer.PlayerId || !round.Summon(id, fake, Now, Time)) return;
            Publish();
        }
        internal void Despawn(int id)
        {
            if (id <= 0 || !round.Despawn(Time, Cooldown, id)) return;
            runtime?.Dispose(); runtime = null; Publish();
        }
        private void Publish()
        {
            if (round.RecordId == 0) { ParrotBroadcast.RemoveClone(MyPlayer.PlayerId); return; }
            var fake = round.Active ? IPlayerlike.GetPlayerlike(round.FakeId) : null;
            var position = fake?.TruePosition ?? MyPlayer.TruePosition;
            bool playing = round.Active && fake?.IsActive == true;
            ParrotBroadcast.SetClone(MyPlayer.PlayerId, new {
                mode = "clone", spoofTalking = (bool)SpoofTalking, ownerId = MyPlayer.PlayerId, birdId = 1000000 + MyPlayer.PlayerId,
                x = position.x, y = position.y, range = (float)Range, seconds = CloneRound.Seconds,
                recordId = round.RecordId, sourceId = MyPlayer.PlayerId, started = round.Started,
                clips = round.Ready ? new[] { round.RecordId } : Array.Empty<int>(),
                playId = playing ? round.SpawnId : 0, playClip = playing ? round.RecordId : 0,
                playStarted = playing ? round.PlayStarted : 0
            });
        }
        private void Update(GameUpdateEvent ev)
        {
            if (!Alive) { Clear(); return; }
            bool recording = round.Recording;
            round.Complete(Now);
            if (recording && !round.Recording && AmOwner) record?.InterruptEffect();
            if (AmOwner && round.Active && runtime?.Active != true) DespawnRpc.Invoke((MyPlayer, round.SpawnId));
            if (AmOwner) record?.SetLabel(round.Recording ? "tanukiClone.recording" : "tanukiClone.record");
            Publish();
        }
        [Local]
        private void OnKill(PlayerKillFakePlayerEvent ev) { if (ev.Target.PlayerlikeId == round.FakeId) DespawnRpc.Invoke((MyPlayer, round.SpawnId)); }
        private void Meeting(MeetingPreStartEvent ev)
        {
            round.Pause(Now, Time, Cooldown); runtime?.Dispose(); runtime = null;
            if (AmOwner) record?.InterruptEffect(); Publish();
        }
        [OnlyMyPlayer]
        private void Death(PlayerDieOrDisconnectEvent ev) => Clear();
        private void End(GameEndEvent ev) => Clear();
        private void Clear()
        {
            runtime?.Dispose(); runtime = null; round.Clear();
            if (AmOwner) record?.InterruptEffect(); ParrotBroadcast.RemoveClone(MyPlayer.PlayerId);
        }
        void IGameOperator.OnReleased() => Clear();
    }
}

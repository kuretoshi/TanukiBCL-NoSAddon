using System;
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
using Virial.Runtime;
using UnityEngine;
using Player = Virial.Game.Player;

namespace TanukiBCL.Roles;

public sealed class Sonarman : DefinedSingleAbilityRoleTemplate<Sonarman.Ability>, DefinedRole, IAssignableDocument
{
    private static readonly FloatConfiguration Duration = NebulaAPI.Configurations.Configuration("options.role.tanukiSonarman.duration",(1f,120f,1f),10f,FloatConfigurationDecorator.Second);
    private static readonly FloatConfiguration Cooldown = NebulaAPI.Configurations.Configuration("options.role.tanukiSonarman.cooldown",(0f,180f,5f),30f,FloatConfigurationDecorator.Second);
    private static readonly FloatConfiguration Range = NebulaAPI.Configurations.Configuration("options.role.tanukiSonarman.range",(1f,20f,.5f),7f);
    private static readonly FloatConfiguration SweepTime = NebulaAPI.Configurations.Configuration("options.role.tanukiSonarman.sweep",(.5f,10f,.5f),2f,FloatConfigurationDecorator.Second);
    public static readonly Sonarman MyRole = new();
    private Sonarman() : base("tanukiSonarman",new Virial.Color(95,230,170),RoleCategory.CrewmateRole,Crewmate.MyTeam,
        new IConfiguration[] { Duration,Cooldown,Range,SweepTime })
    {
        ConfigurationHolder!.Illustration = VoiceRoleIcons.Sonarman;
        ConfigurationHolder.AddTags(AddonRoleTag.Tag);
    }
    Virial.Media.Image? DefinedAssignable.IconImage => VoiceRoleIcons.Sonarman;
    bool IAssignableDocument.HasTips => true;
    public override Ability CreateAbility(Player player,int[] arguments) => new(player);
    AbilityAssignmentStatus DefinedRole.AssignmentStatus => AbilityAssignmentStatus.CanLoadToMadmate;
    MultipleAssignmentType DefinedRole.MultipleAssignment => MultipleAssignmentType.Allowed;
    private static readonly RemoteProcess<(Player player,int sequence)> StartRpc = new("TanukiSonarmanStartV1",(m,_) => m.player.Role.GetAbility<Ability>()?.Start(m.sequence));

    public sealed class Ability : AbstractPlayerAbility, IPlayerAbility
    {
        private static int nextSequence;
        private readonly SonarmanRound state = new();
        private readonly double[] seen = new double[256];
        private readonly long[] hostSeen = new long[256];
        private ModAbilityButton? button;
        private SonarmanVisual? visual;
        private static double Now => Time.unscaledTimeAsDouble;
        public bool Scanning => state.Active(Now) && Eligible;
        private bool Eligible => !MyPlayer.IsDead && !MyPlayer.IsDisconnected && AmongUsClient.Instance != null &&
            AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started &&
            !HeroSpeechHost.IsPresent(MeetingHud.Instance) && !HeroSpeechHost.IsPresent(IntroCutscene.Instance) && !HeroSpeechHost.IsPresent(ExileController.Instance);
        public Ability(Player player) : base(player)
        {
            if (!AmOwner) return;
            button = NebulaAPI.Modules.AbilityButton(this,player,VirtualKeyInput.FixedAbility,Cooldown,
                "tanukiSonarman.scan",VoiceRoleIcons.Sonarman,_ => Eligible && MyPlayer.CanMove && !state.Running && Now >= state.ReadyAt,_ => !MyPlayer.IsDead);
            button.EffectTimer = new HeroTransformTimer(() => state.Remaining(Now),() => Math.Max(.1,state.Ends-state.Started),() => Scanning);
            button.OnClick = _ => StartRpc.Invoke((MyPlayer,++nextSequence));
            button.StartCoolDown();
        }
        internal void Start(int sequence)
        {
            if (!Eligible || !state.Activate(sequence,Now,Duration,Cooldown,SweepTime)) return;
            Array.Clear(seen,0,seen.Length); Array.Clear(hostSeen,0,hostSeen.Length);
            if (!AmOwner) return;
            foreach (var player in NebulaAPI.CurrentGame!.GetAllPlayers())
                HostVoiceRelay.TryRead(player.PlayerId,ref hostSeen[player.PlayerId],out _,out _);
            button?.SetLabel("tanukiSonarman.scanning"); button?.StartEffect();
            visual = new SonarmanVisual();
        }
        internal void Stop()
        {
            if (!state.Stop(Now)) return;
            visual?.Dispose(); visual = null;
            if (!AmOwner) return;
            button?.InterruptEffect(); button?.SetLabel("tanukiSonarman.scan"); button?.StartCoolDown();
        }
        private void Update(GameUpdateEvent ev)
        {
            if (!state.Running) return;
            if (!Scanning) { Stop(); return; }
            if (!AmOwner) return;
            Collect();
            var p = MyPlayer.TruePosition;
            if (state.Sweep(Now,p.x,p.y,Range)) visual?.Ping();
            visual?.Update(p.x,p.y,(float)state.Phase(Now),state.Marks);
        }
        private void Collect()
        {
            var origin = MyPlayer.TruePosition;
            foreach (var player in NebulaAPI.CurrentGame!.GetAllPlayers())
            {
                if (player.PlayerId == MyPlayer.PlayerId || player.IsDead || player.IsDisconnected) continue;
                byte id = player.PlayerId;
                if (SonarmanVoiceHub.TryRead(id,ref seen[id],out var pulse)) state.Observe(pulse,MyPlayer.PlayerId,origin.x,origin.y,Range,Now);
                if (!HostVoiceRelay.TryRead(id,ref hostSeen[id],out double rms,out bool enabled) || !enabled || rms < .01) continue;
                var p = player.TruePosition;
                state.Observe(new(id,p.x,p.y,Now),MyPlayer.PlayerId,origin.x,origin.y,Range,Now);
            }
        }
        private void Meeting(MeetingPreStartEvent ev) => Stop();
        [OnlyMyPlayer] private void Died(PlayerDieOrDisconnectEvent ev) => Stop();
        private void End(GameEndEvent ev) => Stop();
        void IGameOperator.OnReleased() { Stop(); state.Clear(); visual?.Dispose(); visual = null; }
    }
}

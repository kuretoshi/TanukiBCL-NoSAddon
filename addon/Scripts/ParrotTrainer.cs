using System;
using System.Collections.Generic;
using System.Linq;
using Nebula.Modules;
using Nebula.Roles.Crewmate;
using Virial;
using Virial.Assignable;
using Virial.Compat;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Player = Virial.Game.Player;

namespace TanukiBCL.Roles;

public sealed class ParrotTrainer : DefinedSingleAbilityRoleTemplate<ParrotTrainer.Ability>, DefinedRole, IAssignableDocument
{
    private static readonly IntegerConfiguration Maximum = NebulaAPI.Configurations.Configuration("options.role.tanukiParrot.maximum", (1, 5), 2);
    private static readonly FloatConfiguration Seconds = NebulaAPI.Configurations.Configuration(
        "options.role.tanukiParrot.seconds", (.5f, 5f, .5f), 1.5f, FloatConfigurationDecorator.Second);
    private static readonly FloatConfiguration Cooldown = NebulaAPI.Configurations.Configuration(
        "options.role.tanukiParrot.cooldown", (0f, 120f, 5f), 15f, FloatConfigurationDecorator.Second);
    private static readonly FloatConfiguration Range = NebulaAPI.Configurations.Configuration("options.role.tanukiParrot.range", (1f, 10f, .5f), 3.5f);
    public static readonly ParrotTrainer MyRole = new();
    private ParrotTrainer() : base("tanukiParrot", new Virial.Color(95, 225, 90), RoleCategory.CrewmateRole, Crewmate.MyTeam,
        new IConfiguration[] { Maximum, Seconds, Cooldown, Range })
    {
        ConfigurationHolder!.Illustration = ParrotVisual.Icon; ConfigurationHolder.AddTags(AddonRoleTag.Tag);
    }
    Virial.Media.Image? DefinedAssignable.IconImage => ParrotVisual.Icon;
    bool IAssignableDocument.HasTips => true;
    public override Ability CreateAbility(Player player, int[] arguments) => new(player);
    AbilityAssignmentStatus DefinedRole.AssignmentStatus => AbilityAssignmentStatus.CanLoadToMadmate;
    MultipleAssignmentType DefinedRole.MultipleAssignment => MultipleAssignmentType.Allowed;
    private static readonly RemoteProcess<(Player owner, int id, float x, float y)> PlaceRpc = new("TanukiParrotPlaceV1",
        (m, _) => m.owner.Role.GetAbility<Ability>()?.Place(m.id, m.x, m.y));
    private static readonly RemoteProcess<(Player owner, int bird, int sequence, byte source)> RecordRpc = new("TanukiParrotRecordV1",
        (m, _) => m.owner.Role.GetAbility<Ability>()?.Record(m.bird, m.sequence, m.source));
    private static readonly RemoteProcess<(Player owner, int bird, int sequence, int clip)> PlayRpc = new("TanukiParrotPlayV1",
        (m, _) => m.owner.Role.GetAbility<Ability>()?.Play(m.bird, m.sequence, m.clip));
    private static int nextHostSequence;
    public static void RequestCapture(byte owner, int bird, byte source)
    {
        if (AmongUsClient.Instance?.AmHost != true) return;
        NebulaAPI.CurrentGame?.GetPlayer(owner)?.Role.GetAbility<Ability>()?.RequestCapture(bird, source);
    }
    public sealed class Ability : AbstractPlayerAbility, IPlayerAbility
    {
        private static int nextPlacement;
        private readonly ParrotRound round = new();
        private readonly Dictionary<int, ParrotVisual> visuals = new();
        private readonly System.Random random = new();
        private ModAbilityButton? button;
        private double nextVisits;
        private static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        private static double Time => UnityEngine.Time.unscaledTimeAsDouble;
        private bool Alive => !MyPlayer.IsDead && !MyPlayer.IsDisconnected;
        private bool DuringTasks => Alive && AmongUsClient.Instance != null &&
            AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started &&
            !HeroSpeechHost.IsPresent(MeetingHud.Instance) && !HeroSpeechHost.IsPresent(ExileController.Instance) &&
            !HeroSpeechHost.IsPresent(IntroCutscene.Instance);
        public Ability(Player player) : base(player)
        {
            if (!AmOwner) return;
            button = NebulaAPI.Modules.AbilityButton(this, player, VirtualKeyInput.FixedAbility, Cooldown,
                "tanukiParrot.place", ParrotVisual.Icon,
                _ => DuringTasks && player.CanMove && round.CanPlace(Time, Maximum), _ => Alive);
            button.ShowUsesIcon(0, ((int)Maximum).ToString()); button.StartCoolDown();
            button.OnClick = _ => { var p = MyPlayer.TruePosition; PlaceRpc.Invoke((MyPlayer, ++nextPlacement, p.x, p.y)); };
        }
        internal void Place(int id, float x, float y)
        {
            var p = MyPlayer.TruePosition;
            if (!DuringTasks || (p.x - x) * (p.x - x) + (p.y - y) * (p.y - y) > 4 ||
                !round.Place(id, x, y, Time, Maximum, Cooldown, Seconds)) return;
            var bird = round.Find(id)!; visuals[id] = new ParrotVisual(bird);
            if (AmOwner) { button?.StartCoolDown(); button?.ShowUsesIcon(0, ((int)Maximum - round.Birds.Count).ToString()); }
        }
        private bool Near(ParrotRound.Bird bird, Player player, float extra = 0)
        {
            if (player.IsDead || player.IsDisconnected || player.IsDived) return false;
            var p = player.TruePosition;
            float radius = (float)Range + extra;
            return (p.x - bird.X) * (p.x - bird.X) + (p.y - bird.Y) * (p.y - bird.Y) <= radius * radius &&
                !(NebulaAPI.CurrentGame?.CurrentMap?.AnyWallsBetween(p, new Virial.Compat.Vector2(bird.X, bird.Y)) ?? true);
        }
        internal void RequestCapture(int id, byte source)
        {
            var bird = round.Find(id); var speaker = NebulaAPI.CurrentGame?.GetPlayer(source);
            if (!DuringTasks || bird == null || speaker == null || bird.Recording(Now) || !Near(bird, speaker)) return;
            RecordRpc.Invoke((MyPlayer, id, ++nextHostSequence, source));
        }
        internal void Record(int id, int sequence, byte source)
        {
            if (!DuringTasks) return;
            round.Find(id)?.Begin(sequence, source, Now);
        }
        internal void Play(int id, int sequence, int clip)
        {
            if (!DuringTasks) return;
            var bird = round.Find(id);
            bird?.Complete(Now);
            bird?.Play(sequence, clip, Now);
        }
        private void Visits()
        {
            if (!DuringTasks || AmongUsClient.Instance.AmHost != true || Time < nextVisits) return;
            nextVisits = Time + .15;
            var players = NebulaAPI.CurrentGame!.GetAllPlayers().ToArray();
            foreach (var bird in round.Birds) Visit(bird, players);
        }
        private void Visit(ParrotRound.Bird bird, Player[] players)
        {
            bool entered = bird.Visit(players.Where(p => Near(bird, p, bird.WasInside(p.PlayerId) ? .5f : 0)).Select(p => p.PlayerId));
            if (!entered || bird.Clips.Count == 0 || bird.Playing(Now)) return;
            PlayRpc.Invoke((MyPlayer, bird.Id, ++nextHostSequence, bird.Clips[random.Next(bird.Clips.Count)]));
        }
        private void Update(GameUpdateEvent ev)
        {
            if (!Alive) { Clear(); return; }
            foreach (var bird in round.Birds)
            {
                if (DuringTasks) bird.Complete(Now);
                visuals[bird.Id].Update(DuringTasks, bird.Recording(Now), bird.Playing(Now));
            }
            Visits();
            if (round.Birds.Count > 0) ParrotBroadcast.Set(MyPlayer.PlayerId, round.Birds, Range);
        }
        private void Meeting(MeetingPreStartEvent ev) { foreach (var bird in round.Birds) bird.Pause(Now); }
        [Virial.Attributes.OnlyMyPlayer]
        private void Death(PlayerDieOrDisconnectEvent ev) => Clear();
        private void End(GameEndEvent ev) => Clear();
        private void Clear()
        {
            ParrotBroadcast.Remove(MyPlayer.PlayerId);
            foreach (var visual in visuals.Values) visual.Dispose();
            visuals.Clear(); round.Clear();
        }
        void IGameOperator.OnReleased() => Clear();
    }
}

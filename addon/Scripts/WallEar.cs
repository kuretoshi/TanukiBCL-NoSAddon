using System;
using System.Collections.Generic;
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
using UnityEngine;
using Player = Virial.Game.Player;

namespace TanukiBCL.Roles;

public sealed class WallEar : DefinedSingleAbilityRoleTemplate<WallEar.Ability>, DefinedRole, IAssignableDocument
{
    private static readonly IntegerConfiguration Maximum = NebulaAPI.Configurations.Configuration("options.role.tanukiWallEar.maximum", (1, 10), 3);
    private static readonly FloatConfiguration Cooldown = NebulaAPI.Configurations.Configuration(
        "options.role.tanukiWallEar.cooldown", (0f, 120f, 5f), 15f, FloatConfigurationDecorator.Second);
    private static readonly FloatConfiguration Range = NebulaAPI.Configurations.Configuration("options.role.tanukiWallEar.range", (1f, 10f, .5f), 4f);
    public static readonly WallEar MyRole = new();
    private WallEar() : base("tanukiWallEar", new Virial.Color(112, 225, 177), RoleCategory.CrewmateRole, Crewmate.MyTeam,
        new IConfiguration[] { Maximum, Cooldown, Range })
    {
        ConfigurationHolder!.Illustration = WallEarVisual.Icon;
        ConfigurationHolder.AddTags(AddonRoleTag.Tag);
    }
    Virial.Media.Image? DefinedAssignable.IconImage => WallEarVisual.Icon;
    bool IAssignableDocument.HasTips => true;
    public override Ability CreateAbility(Player player, int[] arguments) => new(player);
    AbilityAssignmentStatus DefinedRole.AssignmentStatus => AbilityAssignmentStatus.CanLoadToMadmate;
    MultipleAssignmentType DefinedRole.MultipleAssignment => MultipleAssignmentType.Allowed;
    private static readonly RemoteProcess<(Player player, int sequence, float x, float y)> PlaceRpc = new("TanukiWallEarPlaceV1",
        (m, _) => m.player.Role.GetAbility<Ability>()?.Place(m.sequence, m.x, m.y));
    private static readonly RemoteProcess<(Player player, int sequence, int ear)> ListenRpc = new("TanukiWallEarListenV1",
        (m, _) => m.player.Role.GetAbility<Ability>()?.Listen(m.sequence, m.ear));

    public sealed class Ability : AbstractPlayerAbility, IPlayerAbility
    {
        private const string FreezeTag = "TanukiWallEar:listening";
        private static int nextSequence;
        private readonly WallEarRound round = new();
        private readonly List<WallEarVisual> visuals = new();
        private ModAbilityButton? button;
        private ModAbilityButton? useButton;
        private bool frozen;
        private static double Now => Time.unscaledTimeAsDouble;
        private bool Alive => !MyPlayer.IsDead && !MyPlayer.IsDisconnected;
        private bool Eligible => Alive && AmongUsClient.Instance != null &&
            AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started &&
            !HeroSpeechHost.IsPresent(MeetingHud.Instance) && !HeroSpeechHost.IsPresent(ExileController.Instance) &&
            !HeroSpeechHost.IsPresent(IntroCutscene.Instance);
        public Ability(Player player) : base(player)
        {
            if (!AmOwner) return;
            button = NebulaAPI.Modules.AbilityButton(this, player, VirtualKeyInput.FixedAbility, Cooldown,
                "tanukiWallEar.place", WallEarVisual.Icon,
                _ => Eligible && player.CanMove && round.CanPlace(Now, Maximum), _ => Alive);
            button.ShowUsesIcon(0, ((int)Maximum).ToString());
            button.OnClick = _ => { var p = MyPlayer.TruePosition; PlaceRpc.Invoke((MyPlayer, ++nextSequence, p.x, p.y)); };
            button.StartCoolDown();
            useButton = NebulaAPI.Modules.AbilityButton(this, player, VirtualKeyInput.SecondaryAbility, 0f,
                "tanukiWallEar.use", WallEarVisual.Icon,
                _ => Eligible && (round.Active != null || player.CanMove && NearbyEar() != null),
                _ => Eligible && (round.Active != null || NearbyEar() != null));
            useButton.OnClick = _ =>
            {
                if (round.Active != null) { ListenRpc.Invoke((MyPlayer, ++nextSequence, -1)); return; }
                var ear = NearbyEar();
                if (ear != null) Request(ear);
            };
        }
        internal void Place(int sequence, float x, float y)
        {
            var p = MyPlayer.TruePosition;
            if (!Eligible || (p.x - x) * (p.x - x) + (p.y - y) * (p.y - y) > 4 ||
                !round.Place(sequence, x, y, Now, Maximum, Cooldown)) return;
            if (!AmOwner) return;
            var ear = round.Ears[round.Ears.Count - 1];
            visuals.Add(new WallEarVisual(ear));
            button?.ShowUsesIcon(0, ((int)Maximum - round.Ears.Count).ToString());
            button?.StartCoolDown();
        }
        private void Request(WallEarRound.Ear ear)
        {
            if (round.Active?.Id == ear.Id) { ListenRpc.Invoke((MyPlayer, ++nextSequence, -1)); return; }
            if (!Eligible || !MyPlayer.CanMove || !CanReach(ear)) return;
            ListenRpc.Invoke((MyPlayer, ++nextSequence, ear.Id));
        }
        private bool CanReach(WallEarRound.Ear ear)
        {
            var p = MyPlayer.TruePosition;
            return WallEarRound.Nearby(ear, p.x, p.y) &&
                !(NebulaAPI.CurrentGame?.CurrentMap?.AnyWallsBetween(p, new Virial.Compat.Vector2(ear.X, ear.Y)) ?? true);
        }
        private WallEarRound.Ear? NearbyEar()
        {
            var p = MyPlayer.TruePosition;
            return round.Nearest(p.x, p.y, CanReach);
        }
        internal void Listen(int sequence, int ear)
        {
            if (ear != -1 && !Eligible) return;
            var p = MyPlayer.TruePosition;
            if (!round.Listen(sequence, ear, p.x, p.y)) return;
            if (round.Active == null) Stop();
        }
        private void Update(GameUpdateEvent ev)
        {
            if (!Eligible) Stop();
            if (AmOwner) UpdateOwner();
            if (round.Active is { } ear) WallEarBroadcast.Set(MyPlayer.PlayerId, ear.X, ear.Y, Range);
        }
        private void UpdateOwner()
        {
            useButton?.SetLabel(round.Active == null ? "tanukiWallEar.use" : "tanukiWallEar.release");
            if (round.Active is { } ear)
            {
                if (!MyPlayer.CanMove || !CanReach(ear)) { ListenRpc.Invoke((MyPlayer, ++nextSequence, -1)); return; }
                if (!frozen)
                {
                    MyPlayer.GainSpeedAttribute(0f, float.MaxValue, false, 100, FreezeTag);
                    frozen = true;
                }
            }
            for (int i = 0; i < visuals.Count; i++)
                visuals[i].Update(Eligible, CanReach(round.Ears[i]), round.Active?.Id == round.Ears[i].Id);
        }
        private void Stop()
        {
            round.Stop(); WallEarBroadcast.Remove(MyPlayer.PlayerId);
            if (AmOwner && frozen) { MyPlayer.RemoveAttributeByTag(FreezeTag); frozen = false; }
        }
        private void OnMeeting(MeetingPreStartEvent ev) => Stop();
        [Virial.Attributes.OnlyMyPlayer]
        private void OnDeath(PlayerDieOrDisconnectEvent ev) => Clear();
        private void OnGameEnd(GameEndEvent ev) => Clear();
        private void Clear()
        {
            Stop();
            foreach (var visual in visuals) visual.Dispose();
            visuals.Clear();
            round.Clear();
        }
        void IGameOperator.OnReleased() => Clear();
    }
}

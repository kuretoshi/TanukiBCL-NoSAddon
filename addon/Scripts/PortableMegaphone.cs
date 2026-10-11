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
using Virial.Events.Player;
using Virial.Game;
using UnityEngine;
using Player = Virial.Game.Player;
using Vector3 = UnityEngine.Vector3;

namespace TanukiBCL.Roles;

public sealed class PortableMegaphone : DefinedSingleAbilityRoleTemplate<PortableMegaphone.Ability>, DefinedRole
{
    private static readonly FloatConfiguration ExtraRange = NebulaAPI.Configurations.Configuration("options.role.tanukiPortableMegaphone.extraRange", (0f, 30f, .5f), 5f);
    private static readonly FloatConfiguration CapacitySeconds = NebulaAPI.Configurations.Configuration("options.role.tanukiPortableMegaphone.capacity", (5f, 180f, 5f), 60f, FloatConfigurationDecorator.Second);
    private static readonly FloatConfiguration TaskRecharge = NebulaAPI.Configurations.Configuration("options.role.tanukiPortableMegaphone.recharge", (0f, 100f, 5f), 10f, value => value.ToString("0") + "%");
    public static readonly PortableMegaphone MyRole = new();
    private PortableMegaphone() : base("tanukiPortableMegaphone", new Virial.Color(85, 215, 170), RoleCategory.CrewmateRole, Crewmate.MyTeam,
        new IConfiguration[] { ExtraRange, CapacitySeconds, TaskRecharge })
    {
        ConfigurationHolder!.Illustration = MegaphoneVisual.PortableIcon;
    }
    Virial.Media.Image? DefinedAssignable.IconImage => MegaphoneVisual.PortableIcon;
    public override Ability CreateAbility(Player player, int[] arguments) => new(player, arguments);
    AbilityAssignmentStatus DefinedRole.AssignmentStatus => AbilityAssignmentStatus.CanLoadToMadmate;
    MultipleAssignmentType DefinedRole.MultipleAssignment => MultipleAssignmentType.Allowed;

    private static readonly RemoteProcess<(Player player, int sequence, bool active)> StateRpc = new("TanukiPortableMegaphoneV1",
        (message, _) => message.player.Role.GetAbility<Ability>()?.Receive(message.sequence, message.active));

    public sealed class Ability : AbstractPlayerAbility, IPlayerAbility
    {
        private static int nextSequence;
        private int lastSequence;
        private bool transmitting;
        private readonly MegaphoneBattery battery;
        private ModAbilityButton? button;
        private GameObject? model;
        int[] IPlayerAbility.AbilityArguments => new int[] { (int)Math.Round(battery.Charge * 1000000) };

        public Ability(Player player, int[] arguments) : base(player)
        {
            battery = new MegaphoneBattery(arguments.Length > 0 ? arguments[0] / 1000000d : 1);
            if (!AmOwner) return;
            button = NebulaAPI.Modules.AbilityButton(this, player, VirtualKeyInput.FixedAbility, 0f,
                "tanukiPortableMegaphone.on", MegaphoneVisual.PortableIcon,
                _ => Eligible && (battery.Active || (player.CanMove && battery.Charge > 0)),
                _ => !player.IsDead);
            button.ShowUsesIcon(0, "100%");
            button.EffectTimer = new VoiceEnergyTimer(() => (float)battery.Charge, () => battery.Active);
            button.OnClick = _ => {
                if (battery.Toggle(Eligible && player.CanMove)) Synchronize();
            };
            UpdateButton();
        }

        private bool Eligible => !MyPlayer.IsDead && !MyPlayer.IsDisconnected && MeetingHud.Instance == null && ExileController.Instance == null
            && AmongUsClient.Instance != null && AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started;

        private void Synchronize() => StateRpc.Invoke((MyPlayer, ++nextSequence, battery.Active));

        internal void Receive(int sequence, bool active)
        {
            if (sequence <= lastSequence) return;
            lastSequence = sequence;
            transmitting = active;
            if (AmOwner) {
                if (active) button?.StartEffect(); else button?.InterruptEffect();
                UpdateButton();
            }
            if (!active) HideModel();
        }

        private void UpdateButton()
        {
            button?.SetLabel(battery.Active ? "tanukiPortableMegaphone.off" : battery.Charge <= 0 ? "tanukiPortableMegaphone.empty" : "tanukiPortableMegaphone.on");
            button?.UpdateUsesIcon("<size=75%><nobr>" + Math.Ceiling(battery.Charge * 100).ToString("0") + "%</nobr></size>");
        }

        private void Update(GameUpdateEvent ev)
        {
            if (AmOwner) {
                bool before = battery.Active;
                battery.Update(Time.deltaTime, CapacitySeconds, Eligible);
                if (before != battery.Active) Synchronize();
                UpdateButton();
            }
            if (!transmitting || !Eligible) { HideModel(); return; }
            PortableMegaphoneBroadcast.Set(MyPlayer.PlayerId, ExtraRange);
            if (model == null) {
                model = new GameObject("TanukiPortableMegaphone");
                var sprite = model.AddComponent<SpriteRenderer>();
                sprite.sprite = MegaphoneVisual.PortableSprite;
                sprite.sortingOrder = 15;
                model.transform.localScale = Vector3.one * .65f;
            }
            var position = MyPlayer.TruePosition;
            model.transform.position = new Vector3(position.x + .35f, position.y, -.5f);
        }

        [OnlyMyPlayer]
        private void OnDeath(PlayerDieEvent ev)
        {
            if (!AmOwner || !battery.Active) return;
            BibiriEcho.Notify(MyPlayer.PlayerId);
            battery.Stop();
            Synchronize();
            HideModel();
        }

        [OnlyMyPlayer]
        private void TaskCompleted(PlayerTaskCompleteLocalEvent ev)
        {
            if (!AmOwner || MyPlayer.IsDead) return;
            battery.Recharge(TaskRecharge);
            UpdateButton();
        }

        private void HideModel()
        {
            PortableMegaphoneBroadcast.Remove(MyPlayer.PlayerId);
            if (model != null) UnityEngine.Object.Destroy(model);
            model = null;
        }
        void IGameOperator.OnReleased()
        {
            battery.Stop();
            if (AmOwner && transmitting) Synchronize();
            transmitting = false;
            HideModel();
        }
    }
}

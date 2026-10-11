using System;
using System.Collections.Generic;
using System.Diagnostics;
using Nebula.Game;
using Nebula.Modules;
using Nebula.Player;
using Nebula.Roles.Impostor;
using Nebula.Utilities;
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
using Virial.Helpers;
using Virial.Text;
using UnityEngine;
using Player = Virial.Game.Player;

namespace TanukiBCL.Roles;

public sealed class MegaphoneBazooka : DefinedSingleAbilityRoleTemplate<MegaphoneBazooka.Ability>, DefinedRole
{
    private static readonly FloatConfiguration Cooldown = NebulaAPI.Configurations.Configuration("options.role.tanukiMegaphone.cooldown", (2.5f, 60f, 2.5f), 30f, FloatConfigurationDecorator.Second);
    private static readonly FloatConfiguration ChargeTime = NebulaAPI.Configurations.Configuration("options.role.tanukiMegaphone.charge", (.5f, 10f, .5f), 3f, FloatConfigurationDecorator.Second);
    private static readonly FloatConfiguration MinWidth = NebulaAPI.Configurations.Configuration("options.role.tanukiMegaphone.minWidth", (.05f, .5f, .05f), .05f);
    private static readonly FloatConfiguration MaxWidth = NebulaAPI.Configurations.Configuration("options.role.tanukiMegaphone.maxWidth", (.5f, 10f, .5f), 3.5f);
    private static readonly FloatConfiguration Capacity = NebulaAPI.Configurations.Configuration("options.role.tanukiMegaphone.capacity", (.5f, 10f, .5f), 1.5f);
    private static readonly FloatConfiguration QuietDb = NebulaAPI.Configurations.Configuration("options.role.tanukiMegaphone.quietDb", (-60f, -25f, 1f), -45f, VoiceLevelDisplay.Format);
    private static readonly FloatConfiguration LoudDb = NebulaAPI.Configurations.Configuration("options.role.tanukiMegaphone.loudDb", (-24f, -1f, 1f), -12f, VoiceLevelDisplay.Format);
    private static readonly BoolConfiguration FriendlyFire = NebulaAPI.Configurations.Configuration("options.role.tanukiMegaphone.friendlyFire", false);
    private static readonly BoolConfiguration NormalKill = NebulaAPI.Configurations.Configuration("options.role.tanukiMegaphone.normalKill", true);
    private static readonly BoolConfiguration VoiceEffect = NebulaAPI.Configurations.Configuration("options.role.tanukiMegaphone.voiceEffect", true);
    private static readonly FloatConfiguration VoiceRange = NebulaAPI.Configurations.Configuration("options.role.tanukiMegaphone.voiceRange", (2f, 40f, 1f), 20f);
    private static readonly ValueConfiguration<int> AimMode = NebulaAPI.Configurations.Configuration("options.role.tanukiMegaphone.aimMode", new string[] { "options.role.tanukiMegaphone.free", "options.role.tanukiMegaphone.horizontal", "options.role.tanukiMegaphone.cardinal" }, 0);
    private static readonly BoolConfiguration SoundEnabled = NebulaAPI.Configurations.Configuration("options.role.tanukiMegaphone.soundEnabled", true);
    private static readonly FloatConfiguration SoundVolume = NebulaAPI.Configurations.Configuration("options.role.tanukiMegaphone.soundVolume", (0f, 1f, .1f), .7f);
    public static readonly MegaphoneBazooka MyRole = new();

    private MegaphoneBazooka() : base("tanukiMegaphone", Virial.Color.ImpostorColor, RoleCategory.ImpostorRole, Impostor.MyTeam,
        new IConfiguration[] { AimMode, Cooldown, ChargeTime, MinWidth, MaxWidth, Capacity, QuietDb, LoudDb, FriendlyFire, NormalKill, VoiceRange, VoiceEffect, SoundEnabled, SoundVolume })
    {
        ConfigurationHolder!.Illustration = MegaphoneVisual.Icon;
        ConfigurationHolder.AppendConfiguration(MicrophoneTest.Option(() => QuietDb, () => LoudDb));
    }

    Virial.Media.Image? DefinedAssignable.IconImage => MegaphoneVisual.Icon;

    public override Ability CreateAbility(Player player, int[] arguments) => new(player);
    AbilityAssignmentStatus DefinedRole.AssignmentStatus => AbilityAssignmentStatus.Killers;
    MultipleAssignmentType DefinedRole.MultipleAssignment => MultipleAssignmentType.AsUniqueKillAbility;

    // 0=charge, 1=fire, 2=cancel, 3=charge preview. IDs reject old/duplicate shots.
    // NoS visits Item1..Item7 but not ValueTuple.Rest. Keep each tuple at <=7 fields.
    private static readonly RemoteProcess<(Player player, int id, int command, float angle, float width, (float x, float y) position, float energy)> StateRpc =
        new("TanukiMegaphoneStateV3", (m, _) => m.player.Role.GetAbility<Ability>()?.Receive(m.id, m.command, m.angle, m.width, m.position.x, m.position.y, m.energy));

    public sealed class Ability : AbstractPlayerAbility, IPlayerAbility
    {
        private enum Phase { Idle, Charging, Firing }
        private Phase phase;
        private int shotId;
        private static int nextShotId;
        private float elapsed;
        private float angle;
        private float width;
        private float voiceEnergy;
        private float originX, originY;
        private float previewTimer;
        private bool movementLocked;
        private readonly MegaphoneCharge charge = new();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly HashSet<byte> attempted = new();
        private SharedVoiceBridge? bridge;
        private MegaphoneVisual? visual;
        private MegaphoneAudio? audio;
        private ModAbilityButton? button;
        public static string? LastBridgeError { get; private set; }
        bool IPlayerAbility.HideKillButton => !NormalKill || phase != Phase.Idle;
        bool IPlayerAbility.BlockUsingUtility => phase != Phase.Idle;

        public Ability(Player player) : base(player)
        {
            if (!AmOwner) return;
            try { bridge = new SharedVoiceBridge(player.PlayerId); LastBridgeError = null; System.Console.WriteLine("[TanukiBCL] Megaphone voice bridge opened for PID " + Environment.ProcessId + " at " + bridge.DescriptorPath); }
            catch (Exception ex) { LastBridgeError = ex.Message; System.Console.WriteLine("[TanukiBCL] Megaphone voice bridge failed: " + ex); }
            button = NebulaAPI.Modules.AbilityButton(this, player, VirtualKeyInput.FixedAbility, (float)Cooldown,
                "tanukiMegaphone.fire", MegaphoneVisual.Icon,
                _ => phase == Phase.Idle && player.CanMove,
                _ => !player.IsDead);
            button.OnClick = _ =>
            {
                if (phase != Phase.Idle || player.IsDead || MeetingHud.Instance != null) return;
                float startAngle = ReadAim();
                var position = player.TruePosition;
                int id = ++nextShotId;
                StateRpc.Invoke((player, id, 0, startAngle, MinWidth, (position.x, position.y), 0));
            };
            button.StartCoolDown();
            // Like Dancer, the visual ring follows the ability's actual progress.
            button.EffectTimer = new VoiceEnergyTimer(() => (float)charge.Energy, () => phase == Phase.Charging);
        }

        internal void Receive(int id, int command, float newAngle, float newWidth, float x, float y, float newEnergy)
        {
            if (!float.IsFinite(newAngle) || !float.IsFinite(newWidth) || !float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(newEnergy)) return;
            if (command == 0)
            {
                if (id <= shotId || MyPlayer.IsDead || MeetingHud.Instance != null || ExileController.Instance != null) return;
                Clear();
                shotId = id;
                phase = Phase.Charging;
                charge.Reset();
                elapsed = 0;
                visual = new MegaphoneVisual();
                if (SoundEnabled) audio = new MegaphoneAudio(x, y, SoundVolume);
                if (AmOwner) { button?.SetLabel("tanukiMegaphone.charge"); button?.StartEffect(); }
                // This API broadcasts its own RPC: only the shooter sends the lock.
                if (AmOwner)
                {
                    MyPlayer.GainSpeedAttribute(0f, (float)ChargeTime + 6f, false, 100, "TanukiMegaphone:charge");
                    movementLocked = true;
                }
            }
            else if (id != shotId) return;
            else if (command == 2) { Finish(); return; }
            else if (command == 1)
            {
                if (phase != Phase.Charging) return;
                phase = Phase.Firing;
                elapsed = 0;
                attempted.Clear();
                if (AmOwner) { button?.InterruptEffect(); button?.SetLabel("tanukiMegaphone.firing"); }
            }
            else if (command == 3)
            {
                if (phase != Phase.Charging) return;
            }
            else return;
            angle = (float)MegaphoneCharge.Aim(newAngle, AimMode.GetValue());
            width = Mathf.Clamp(newWidth, MinWidth, MaxWidth);
            voiceEnergy = Mathf.Clamp01(newEnergy);
            originX = x;
            originY = y;
            if (command == 1) audio?.Fire(voiceEnergy);
            if (phase == Phase.Firing) MegaphoneBroadcast.Remove(MyPlayer.PlayerId);
        }

        private void Update(GameUpdateEvent ev)
        {
            bool eligible = !MyPlayer.IsDead && !MyPlayer.IsDisconnected && AmongUsClient.Instance != null &&
                AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started &&
                MeetingHud.Instance == null && ExileController.Instance == null;
            // Drain samples even while idle, so a previous charge cannot donate energy.
            if (AmOwner && bridge != null)
            {
                try
                {
                    if (bridge.TryReadSample(out _, out double rms, out bool active) && eligible && phase == Phase.Charging)
                        charge.Sample(rms, active, clock.Elapsed.TotalSeconds, QuietDb, LoudDb, Capacity, bridge.LatestFromHostRelay ? .35 : .15);
                }
                catch (Exception ex) { LastBridgeError = ex.Message; bridge.Dispose(); bridge = null; }
            }
            if (!eligible) { if (phase != Phase.Idle) Cancel(); return; }
            if (phase == Phase.Idle) return;
            elapsed += ev.DeltaTime;
            if (phase == Phase.Charging)
            {
                if (AmOwner)
                {
                    angle = ReadAim();
                    width = (float)charge.Width(MinWidth, MaxWidth);
                    voiceEnergy = (float)charge.Energy;
                    var position = MyPlayer.TruePosition;
                    originX = position.x; originY = position.y;
                    previewTimer += ev.DeltaTime;
                    if (elapsed >= (float)ChargeTime)
                    {
                        float beamX = originX + Mathf.Cos(angle) * 1.85f;
                        float beamY = originY + Mathf.Sin(angle) * 1.85f;
                        StateRpc.Invoke((MyPlayer, shotId, 1, angle, width, (beamX, beamY), voiceEnergy));
                    }
                    else if (previewTimer >= .1f)
                    {
                        previewTimer = 0;
                        StateRpc.Invoke((MyPlayer, shotId, 3, angle, width, (originX, originY), voiceEnergy));
                    }
                }
                // Timeout protects other clients if the shooter disconnects before sending fire.
                else if (elapsed > (float)ChargeTime + 2f) { Finish(); return; }
                if (phase == Phase.Charging)
                {
                    MegaphoneBroadcast.Set(MyPlayer.PlayerId, voiceEnergy, VoiceRange, VoiceEffect);
                }
            }
            if (phase == Phase.Firing)
            {
                if (elapsed >= 5f) { Finish(); return; } // Nova's beam duration default.
                if (AmOwner)
                {
                    foreach (var target in NebulaAPI.CurrentGame!.GetAllPlayers())
                    {
                        if (target.IsDead || target.IsDisconnected || target.PlayerId == MyPlayer.PlayerId || attempted.Contains(target.PlayerId)) continue;
                        if (!FriendlyFire && target.IsImpostor) continue;
                        var position = target.TruePosition;
                        if (!MegaphoneCharge.Hits(position.x - originX, position.y - originY, angle, width)) continue;
                        attempted.Add(target.PlayerId);
                        MyPlayer.MurderPlayer(target, PlayerStates.Dead, null, KillParameter.RemoteKill);
                    }
                }
            }
            // The beam renderer begins .75 units after the megaphone root.
            float offset = phase == Phase.Firing ? -.75f : 1.1f;
            audio?.Tick(voiceEnergy);
            visual?.Draw(originX + Mathf.Cos(angle) * offset, originY + Mathf.Sin(angle) * offset, angle, width, phase == Phase.Firing, voiceEnergy);
        }

        private void Cancel()
        {
            if (AmOwner) StateRpc.Invoke((MyPlayer, shotId, 2, 0, MinWidth, (0f, 0f), 0));
            else Finish();
        }

        private void OnMeeting(MeetingPreStartEvent ev) { if (phase != Phase.Idle) Cancel(); }

        [OnlyMyPlayer]
        private void OnDeath(PlayerDieOrDisconnectEvent ev) { if (phase != Phase.Idle) Cancel(); }

        private float ReadAim()
        {
            var camera = Camera.main;
            if (camera == null) return angle;
            var screen = Input.mousePosition;
            screen.z = -camera.transform.position.z;
            var mouse = camera.ScreenToWorldPoint(screen);
            var position = MyPlayer.TruePosition;
            return (float)MegaphoneCharge.Aim(Mathf.Atan2(mouse.y - position.y, mouse.x - position.x), AimMode.GetValue());
        }

        private void Clear()
        {
            MegaphoneBroadcast.Remove(MyPlayer.PlayerId);
            visual?.Dispose(); visual = null;
            audio?.Dispose(); audio = null;
            if (AmOwner && movementLocked)
            {
                MyPlayer.RemoveAttributeByTag("TanukiMegaphone:charge");
                movementLocked = false;
            }
            charge.Reset(); attempted.Clear();
            phase = Phase.Idle;
            elapsed = previewTimer = 0;
        }

        private void Finish()
        {
            Clear();
            if (AmOwner)
            {
                button?.InterruptEffect();
                button?.SetLabel("tanukiMegaphone.fire");
                button?.StartCoolDown();
            }
        }

        void IGameOperator.OnReleased()
        {
            Clear();
            bridge?.Dispose(); bridge = null;
        }
    }
}

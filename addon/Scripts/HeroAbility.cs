using System;
using System.Diagnostics;
using Nebula.Modules;
using Nebula.Utilities;
using Virial;
using Virial.Assignable;
using Virial.Attributes;
using Virial.Components;
using Virial.Compat;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial.Text;

namespace TanukiBCL.Roles;

public sealed class HeroAbility : AbstractPlayerAbility, IPlayerAbility
{
    private static readonly RemoteProcess<(Player player, int sequence)> TransformRpc = new("TanukiHeroTransformV1",
        (message, _) => message.player.Role.GetAbility<HeroAbility>()?.Transform(message.sequence));
    private static readonly RemoteProcess<(Player player, int sequence)> ArmRpc = new("TanukiHeroSpeechArmV1",
        (message, _) => message.player.Role.GetAbility<HeroAbility>()?.BeginWait(message.sequence));
    private static int nextSequence;
    public static void EnsureRpcRegistered() { }
    private readonly HeroKind kind;
    private readonly HeroRoleSettings settings;
    private readonly HeroTransformState state = new();
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private HeroVisual? visual;
    private ModAbilityButton? transformButton, killButton;
    private IVisualTimer? normalCooldown, retryCooldown;
    private int armedSequence;
    private long speechExpiry;
    private const string HiddenNameTag = "tanuki.bcl.hero.transformedName";
    private bool nameHidden;
    private double Now => clock.Elapsed.TotalSeconds;
    private bool Alive => !MyPlayer.IsDead && !MyPlayer.IsDisconnected;
    private bool Eligible => Alive && !HeroSpeechHost.IsPresent(MeetingHud.Instance) && !HeroSpeechHost.IsPresent(ExileController.Instance) &&
        AmongUsClient.Instance != null && AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started;
    private bool Transformed => state.Active && Eligible && state.Remaining(Now) > 0;
    public bool SpeechEligible => Eligible;
    public object SpeechJob => new { playerId = MyPlayer.PlayerId, kind = "hero", request = state.Waiting ? armedSequence : 0,
        expires = state.Waiting ? speechExpiry : 0, words = Array.Empty<string>() };
    bool IPlayerAbility.HideKillButton => true;

    public HeroAbility(Player player, HeroKind kind, HeroRoleSettings settings) : base(player)
    {
        this.kind = kind; this.settings = settings;
        if (!AmOwner) return;
        var icon = kind == HeroKind.Super ? HeroVisual.SuperTransformIcon : HeroVisual.EvilTransformIcon;
        transformButton = NebulaAPI.Modules.AbilityButton(this, player, VirtualKeyInput.FixedAbility, settings.Cooldown,
            "tanukiHero.transform", icon,
            _ => Eligible && player.CanMove && !state.Waiting && !state.Active && HeroSpeechHost.ReadyFor(player.PlayerId),
            _ => Alive);
        transformButton.OnClick = _ => Arm();
        normalCooldown = transformButton.CoolDownTimer;
        // A separate timer keeps failure at one second even when the normal cooldown is zero.
        retryCooldown = NebulaAPI.Modules.Timer(this, 1f);
        transformButton.EffectTimer = new HeroTransformTimer(() => state.Remaining(Now), () => state.Duration, () => state.Waiting || state.Active);
        transformButton.StartCoolDown();
        killButton = NebulaAPI.Modules.KillButton(this, player, true, VirtualKeyInput.Kill, settings.KillCooldown,
            kind == HeroKind.Super ? "tanukiHero.justice" : "kill", ModAbilityButton.LabelType.Impostor, null, Kill,
            target => kind == HeroKind.Super || !target.IsImpostor,
            _ => Transformed && player.CanMove, _ => Transformed);
    }
    private void Arm()
    {
        if (!Eligible || !MyPlayer.CanMove || !HeroSpeechHost.ReadyFor(MyPlayer.PlayerId) || state.Waiting || state.Active) return;
        ArmRpc.Invoke((MyPlayer, ++nextSequence));
    }
    private void BeginWait(int sequence)
    {
        if (!Eligible || sequence <= armedSequence) return;
        if (!state.Arm(Now, settings.WaitSeconds)) return;
        armedSequence = sequence;
        speechExpiry = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (long)(state.Duration * 1000);
        if (AmOwner)
        {
            transformButton!.CoolDownTimer = normalCooldown;
            transformButton.StartEffect();
        }
    }
    public void RecogniseSpeech(int sequence)
    {
        if (!Eligible || !state.CanHearTrigger(Now) || sequence != armedSequence ||
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() >= speechExpiry) return;
        TransformRpc.Invoke((MyPlayer, sequence));
    }
    private void Transform(int sequence)
    {
        if (!Eligible || sequence != armedSequence || !state.CanHearTrigger(Now) ||
            !state.Transform(sequence, Now, settings.Seconds)) return;
        HeroTransformBroadcast.Set(MyPlayer.PlayerId, kind);
        if (AmOwner && settings.HideName)
        {
            nameHidden = true;
            MyPlayer.GainAttribute(PlayerAttributes.HiddenName, (float)state.Duration, false, 0, HiddenNameTag);
        }
        visual?.Dispose();
        try { visual = new HeroVisual(MyPlayer, kind); }
        catch (Exception ex)
        {
            NebulaAPI.Logging.NebulaLogger("TanukiHero").Error(ex.ToString());
            Finish(true);
            return;
        }
        if (AmOwner) transformButton?.StartEffect();
    }
    private void Kill(Player target, ModAbilityButton button)
    {
        if (!Transformed || target.IsDead || target.IsDisconnected || target.PlayerId == MyPlayer.PlayerId) return;
        if (kind == HeroKind.Evil && target.IsImpostor) return;
        if (HeroTransformState.Misfires(kind, target.Role.Role.Category == RoleCategory.CrewmateRole))
            MyPlayer.Suicide(PlayerStates.Misfired, null, KillParameter.NormalKill);
        else
            MyPlayer.MurderPlayer(target, PlayerStates.Dead, null, KillParameter.NormalKill);
        button.StartCoolDown();
    }
    [OnlyMyPlayer]
    private void CheckKill(PlayerCheckKilledEvent ev)
    {
        HeroKind? killer = ev.Killer.Role.Role == SuperHero.MyRole ? HeroKind.Super :
            ev.Killer.Role.Role == EvilHero.MyRole ? HeroKind.Evil : null;
        if (HeroTransformState.BlocksKill(kind, Transformed, ev.Killer.PlayerId == MyPlayer.PlayerId, killer))
            ev.Result = KillResult.ObviousGuard;
    }
    private void Update(GameUpdateEvent ev)
    {
        double now = Now;
        bool eligible = Eligible;
        bool failed = state.IsSpeechTimeout(now, eligible);
        if (state.Update(now, eligible)) Finish(failed);
        if (Transformed) HeroTransformBroadcast.Set(MyPlayer.PlayerId, kind);
        visual?.Update();
        if (!AmOwner) return;
        transformButton?.SetLabel(state.Active ? "tanukiHero.active" : state.Waiting ? "tanukiHero.speak" :
            HeroSpeechHost.ReadyFor(MyPlayer.PlayerId) ? "tanukiHero.transform" : "tanukiHero.disconnected");
    }
    private void LateUpdate(GameLateUpdateEvent ev) => visual?.LateUpdate();
    private void Finish(bool failed = false)
    {
        RestoreName();
        HeroTransformBroadcast.Remove(MyPlayer.PlayerId);
        state.Clear();
        visual?.Dispose(); visual = null;
        if (!AmOwner) return;
        transformButton?.InterruptEffect();
        if (transformButton != null) transformButton.CoolDownTimer = failed ? retryCooldown : normalCooldown;
        transformButton?.StartCoolDown();
    }
    private void OnMeeting(MeetingStartEvent ev)
    {
        if (AmOwner && transformButton != null) transformButton.CoolDownTimer = normalCooldown;
        if (state.Active || state.Waiting) Finish();
    }
    [OnlyMyPlayer]
    private void OnDeath(PlayerDieEvent ev) { Finish(); }
    private void OnGameEnd(GameEndEvent ev) { Finish(); }
    private void RestoreName()
    {
        if (!AmOwner || !nameHidden) return;
        nameHidden = false;
        MyPlayer.RemoveAttributeByTag(HiddenNameTag);
    }
    void IGameOperator.OnReleased()
    {
        RestoreName();
        HeroTransformBroadcast.Remove(MyPlayer.PlayerId);
        state.Clear();
        visual?.Dispose(); visual = null;
    }
}

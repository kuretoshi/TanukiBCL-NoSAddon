using System;
using System.Diagnostics;

using Nebula.Utilities;
using Virial;
using Virial.Assignable;
using Virial.Attributes;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Game;
using Virial.Text;
using UnityEngine;

namespace TanukiBCL.Roles;

public sealed class Talkative : DefinedAllocatableModifierTemplate, DefinedAllocatableModifier
{
    private static readonly FloatConfiguration Duration = NebulaAPI.Configurations.Configuration(
        "options.role.tanukiTalkative.duration", (5f, 120f, 5f), 20f, FloatConfigurationDecorator.Second);
    private static readonly FloatConfiguration Threshold = NebulaAPI.Configurations.Configuration(
        "options.role.tanukiTalkative.threshold", (-60f, -5f, 1f), -35f, VoiceLevelDisplay.Format);
    private static readonly BoolConfiguration DuringMeeting = NebulaAPI.Configurations.Configuration(
        "options.role.tanukiTalkative.duringMeeting", true);
    public static readonly Talkative MyRole = new();
    private Talkative() : base("tanukiTalkative", "TTALK", new Virial.Color(100, 220, 180),
        new IConfiguration[] { Duration, Threshold, DuringMeeting })
    {
        ConfigurationHolder!.Illustration = VoiceRoleIcons.Talkative;
        ConfigurationHolder.AppendConfiguration(MicrophoneTest.Option(() => Threshold));
    }
    Virial.Media.Image? DefinedAssignable.IconImage => VoiceRoleIcons.Talkative;
    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(Player player, int[] arguments) => new Instance(player);

    public sealed class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;
        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo, bool inEndScene)
        {
            if (AmOwner || canSeeAllInfo) name += " [" + NebulaAPI.Language.Translate("role.tanukiTalkative.short") + "]";
        }
        private readonly SilenceCountdown countdown = new();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private SharedVoiceBridge? bridge;
        private TMPro.TextMeshPro? text;
        private bool textInMeeting;
        private GameObject? indicator;
        private SpriteRenderer? indicatorIcon;
        private bool killPending;
        private double nextBridgeAttempt;
        public static string? LastBridgeError { get; private set; }
        public Instance(Player player) : base(player) { }

        void RuntimeAssignable.OnActivated()
        {
            if (!AmOwner) return;
            OpenBridge();
        }

        private void OpenBridge()
        {
            if (bridge != null || clock.Elapsed.TotalSeconds < nextBridgeAttempt) return;
            nextBridgeAttempt = clock.Elapsed.TotalSeconds + 1;
            try
            {
                bridge = new SharedVoiceBridge(MyPlayer.PlayerId);
                LastBridgeError = null;
                System.Console.WriteLine("[TanukiBCL] Talkative voice bridge opened for PID " + Environment.ProcessId + " at " + bridge.DescriptorPath);
            }
            catch (Exception ex)
            {
                if (LastBridgeError != ex.Message) System.Console.WriteLine("[TanukiBCL] Talkative voice bridge initialization failed: " + ex);
                LastBridgeError = ex.Message;
            }
        }

        [Local]
        private void Update(GameUpdateEvent ev)
        {
            // Also initialize from the local update: some role-change paths may
            // activate before ownership is ready. Failed opens retry each second.
            if (!MyPlayer.IsDead && !MyPlayer.IsDisconnected) OpenBridge();
            bool inMeeting = MeetingHud.Instance != null;
            bool eligible = !MyPlayer.IsDead && !MyPlayer.IsDisconnected && !killPending &&
                AmongUsClient.Instance != null && AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started &&
                IntroCutscene.Instance == null && ExileController.Instance == null && (!inMeeting || DuringMeeting);
            if (!eligible) countdown.Pause();
            try
            {
                if (bridge != null && bridge.TryReadSample(out _, out double rms, out bool active) && eligible &&
                    countdown.Sample(rms, active, clock.Elapsed.TotalSeconds, Threshold, Duration))
                {
                    killPending = true;
                    MyPlayer.Suicide(PlayerStates.Suicide, null, inMeeting ? KillParameter.MeetingKill : KillParameter.NormalKill,
                        result => { if (!MyPlayer.IsDead) { killPending = false; countdown.Reset(); } });
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine("[TanukiBCL] Talkative voice bridge read failed: " + ex);
                LastBridgeError = ex.Message; bridge?.Dispose(); bridge = null; countdown.Pause();
            }
            ShowCountdown(inMeeting, eligible);
        }

        private void ShowCountdown(bool inMeeting, bool eligible)
        {
            if (MyPlayer.IsDead) { DestroyText(); return; }
            if (text == null || textInMeeting != inMeeting)
            {
                DestroyText();
                var template = inMeeting ? MeetingHud.Instance?.TitleText : HudManager.Instance?.KillButton?.cooldownTimerText;
                var parent = inMeeting ? MeetingHud.Instance?.transform : HudManager.Instance?.transform;
                if (template == null || parent == null) return;
                indicator = new GameObject("TanukiTalkativeIndicator");
                indicator.layer = template.gameObject.layer;
                indicator.transform.SetParent(parent, false);
                indicator.SetAsUIAspectContent(AspectPosition.EdgeAlignments.RightTop, new Vector3(.65f, 1.8f, -30f));
                indicator.GetComponent<AspectPosition>().updateAlways = true;
                indicatorIcon = indicator.AddComponent<SpriteRenderer>();
                indicatorIcon.sprite = VoiceRoleIcons.TalkativeSprite;
                indicatorIcon.sortingOrder = 20;
                indicator.transform.localScale = Vector3.one * .55f;
                text = UnityEngine.Object.Instantiate(template, indicator.transform);
                text.name = "TanukiTalkativeCountdown";
                text.transform.localPosition = new Vector3(0f, -.8f, -1f);
                text.transform.localScale = Vector3.one;
                text.fontSize = 1.35f;
                text.alignment = TMPro.TextAlignmentOptions.Center;
                text.rectTransform.sizeDelta = new Vector2(2.5f, 1.1f);
                textInMeeting = inMeeting;
            }
            var aspect = indicator!.GetComponent<AspectPosition>();
            aspect.DistanceFromEdge = new Vector3(MyPlayer.TryGetModifier<Bibiri.Instance>(out _) ? 1.5f : .65f, 1.8f, -30f);
            aspect.AdjustPosition();
            text.gameObject.SetActive(true);
            string key = !eligible ? "role.tanukiTalkative.paused" : !countdown.Connected(clock.Elapsed.TotalSeconds)
                ? "role.tanukiTalkative.disconnected" : "role.tanukiTalkative.remaining";
            text.text = Math.Ceiling(countdown.Remaining(Duration)) + "s" + (key == "role.tanukiTalkative.remaining" ? "" : "\n<size=65%>" + NebulaAPI.Language.Translate(key) + "</size>");
            if (indicatorIcon != null) indicatorIcon.color = !eligible || !countdown.Connected(clock.Elapsed.TotalSeconds) ? UnityEngine.Color.gray : countdown.Remaining(Duration) <= 5 ? new UnityEngine.Color(1f, .4f, .4f) : UnityEngine.Color.white;
            text.color = countdown.Remaining(Duration) <= 5 ? UnityEngine.Color.red : UnityEngine.Color.white;
        }

        [Local]
        private void OnMeetingStart(MeetingPreStartEvent ev) { if (!DuringMeeting) countdown.Reset(); }
        [Local]
        private void OnMeetingEnd(MeetingEndEvent ev) { if (!DuringMeeting) countdown.Reset(); }
        private void DestroyText() { if (indicator != null) UnityEngine.Object.Destroy(indicator); indicator = null; indicatorIcon = null; text = null; }
        protected override void OnReleased()
        {
            bridge?.Dispose(); bridge = null; DestroyText(); base.OnReleased();
        }
    }
}

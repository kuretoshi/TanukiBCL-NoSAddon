using System;
using System.Diagnostics;
using Nebula.Modules;
using Nebula.Utilities;
using Virial;
using Virial.Assignable;
using Virial.Attributes;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial.Game;
using Virial.Text;
using UnityEngine;

namespace TanukiBCL.Roles;

public sealed class LimitSpeech : DefinedAllocatableModifierTemplate, DefinedAllocatableModifier
{
    private static readonly BoolConfiguration RandomMinutes = NebulaAPI.Configurations.Configuration("options.role.tanukiLimitSpeech.random", false);
    private static readonly BoolConfiguration ConsumeInMeeting = NebulaAPI.Configurations.Configuration("options.role.tanukiLimitSpeech.consumeInMeeting", true);
    private static readonly FloatConfiguration Minutes = NebulaAPI.Configurations.Configuration("options.role.tanukiLimitSpeech.minutes", (1f, 10f, 1f), 3f);
    private static readonly FloatConfiguration TaskReward = NebulaAPI.Configurations.Configuration("options.role.tanukiLimitSpeech.taskReward", (0f, 120f, 5f), 10f);
    private static readonly FloatConfiguration KillReward = NebulaAPI.Configurations.Configuration("options.role.tanukiLimitSpeech.killReward", (0f, 120f, 5f), 10f);
    private static readonly FloatConfiguration Threshold = NebulaAPI.Configurations.Configuration("options.role.tanukiLimitSpeech.threshold", (-60f, -5f, 1f), -35f, VoiceLevelDisplay.Format);
    private static readonly ValueConfiguration<int> Action = NebulaAPI.Configurations.Configuration("options.role.tanukiLimitSpeech.action", new string[] { "options.role.tanukiLimitSpeech.mute", "options.role.tanukiLimitSpeech.suicide" }, 0);
    public static readonly LimitSpeech MyRole = new();
    private static readonly RemoteProcess<(Player player, bool muted)> MuteRpc = new("TanukiLimitSpeechMuteV1", (data, _) => {
        if (data.player.TryGetModifier<Instance>(out var instance)) instance.SetMuted(data.muted);
    });
    private LimitSpeech() : base("tanukiLimitSpeech", "TLSPEECH", new Virial.Color(235, 195, 105), new IConfiguration[] { RandomMinutes, Minutes, ConsumeInMeeting, Action, Threshold, TaskReward, KillReward })
    {
        ConfigurationHolder!.Illustration = VoiceRoleIcons.LimitSpeech;
        ConfigurationHolder.AppendConfiguration(MicrophoneTest.Option(() => Threshold));
    }
    Virial.Media.Image? DefinedAssignable.IconImage => VoiceRoleIcons.LimitSpeech;
    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(Player player, int[] arguments) => new Instance(player);

    public sealed class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;
        private readonly SpeechBudget budget = new();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private SharedVoiceBridge? bridge;
        private double total;
        private double retryAt;
        private bool expired, killPending, muted;
        private GameObject? indicator;
        private SpriteRenderer? meterFill;
        private static Sprite? meterSprite;
        private TMPro.TextMeshPro? text;
        private bool textInMeeting;
        public Instance(Player player) : base(player) { }
        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo, bool inEndScene)
        {
            if (AmOwner || canSeeAllInfo) name += " [" + NebulaAPI.Language.Translate("role.tanukiLimitSpeech.short") + "]";
        }
        void RuntimeAssignable.OnActivated() { if (AmOwner) Initialize(); }
        private void Initialize()
        {
            if (total <= 0) total = 60 * (RandomMinutes ? UnityEngine.Random.Range(1, 11) : (float)Minutes);
            if (bridge != null || clock.Elapsed.TotalSeconds < retryAt || expired) return;
            retryAt = clock.Elapsed.TotalSeconds + 1;
            try { bridge = new SharedVoiceBridge(MyPlayer.PlayerId); }
            catch (Exception ex) { System.Console.WriteLine("[TanukiBCL] LimitSpeech bridge: " + ex.Message); }
        }
        public void SetMuted(bool value) { muted = value; LimitSpeechBroadcast.Set(MyPlayer.PlayerId, value); }
        [OnlyMyPlayer]
        private void TaskCompleted(PlayerTaskCompleteLocalEvent ev)
        {
            if (MyPlayer.Role.Role.Category == RoleCategory.CrewmateRole) Reward(TaskReward);
        }
        [OnlyMyPlayer]
        private void KilledPlayer(PlayerKillPlayerEvent ev)
        {
            if (ev.Dead.PlayerId != MyPlayer.PlayerId) Reward(KillReward);
        }
        private void Reward(double seconds)
        {
            if (!AmOwner || MyPlayer.IsDead || MyPlayer.IsDisconnected || killPending || !budget.Add(seconds)) return;
            expired = false;
            if (muted) MuteRpc.Invoke((MyPlayer, false));
            Initialize();
            ShowCountdown();
        }
        private void Update(GameUpdateEvent ev)
        {
            if (MyPlayer.IsDisconnected) { LimitSpeechBroadcast.Set(MyPlayer.PlayerId, false); return; }
            LimitSpeechBroadcast.Publish();
            if (!AmOwner) return;
            Initialize();
            bool eligible = !MyPlayer.IsDead && AmongUsClient.Instance != null && AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started && IntroCutscene.Instance == null && ExileController.Instance == null;
            bool consuming = eligible && (MeetingHud.Instance == null || ConsumeInMeeting);
            if (!consuming) budget.Pause();
            try {
                if (bridge != null && bridge.TryReadSample(out _, out double rms, out bool active) && consuming && !expired)
                    expired = budget.Sample(rms, active, clock.Elapsed.TotalSeconds, Threshold, total, bridge.LatestFromHostRelay ? .35 : .15);
            }
            catch (Exception ex) { System.Console.WriteLine("[TanukiBCL] LimitSpeech sample: " + ex.Message); bridge?.Dispose(); bridge = null; budget.Pause(); }
            if (expired && eligible) Expire();
            ShowCountdown();
        }
        private void Expire()
        {
            if (Action.GetValue() == 0) { if (!muted) MuteRpc.Invoke((MyPlayer, true)); return; }
            if (killPending || clock.Elapsed.TotalSeconds < retryAt) return;
            killPending = true;
            MyPlayer.Suicide(PlayerStates.Suicide, null, MeetingHud.Instance != null ? KillParameter.MeetingKill : KillParameter.NormalKill,
                result => { if (result != KillResult.Kill) { killPending = false; retryAt = clock.Elapsed.TotalSeconds + 1; } });
        }
        private void ShowCountdown()
        {
            if (MyPlayer.IsDead) { DestroyIndicator(); return; }
            bool meeting = MeetingHud.Instance != null;
            if (meeting && !ConsumeInMeeting) { DestroyIndicator(); return; }
            if (text == null || textInMeeting != meeting)
            {
                DestroyIndicator();
                var template = meeting ? MeetingHud.Instance?.TitleText : HudManager.Instance?.KillButton?.cooldownTimerText;
                var parent = meeting ? MeetingHud.Instance?.transform : HudManager.Instance?.transform;
                if (template == null || parent == null) return;
                indicator = new GameObject("TanukiLimitSpeechIndicator");
                indicator.transform.SetParent(parent, false);
                indicator.SetAsUIAspectContent(AspectPosition.EdgeAlignments.RightTop, new Vector3(.65f, 2.8f, -30f));
                indicator.GetComponent<AspectPosition>().updateAlways = true;
                indicator.transform.localScale = Vector3.one * .55f;
                var icon = indicator.AddComponent<SpriteRenderer>();
                icon.sprite = VoiceRoleIcons.LimitSpeechSprite;
                icon.sortingOrder = 20;
                if (meterSprite == null)
                {
                    var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    texture.SetPixel(0, 0, UnityEngine.Color.white); texture.Apply();
                    meterSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0, .5f), 1);
                }
                SpriteRenderer Bar(string name, Vector3 position, Vector3 scale, UnityEngine.Color color, int order)
                {
                    var obj = new GameObject(name); obj.transform.SetParent(indicator.transform, false);
                    obj.transform.localPosition = position; obj.transform.localScale = scale;
                    var renderer = obj.AddComponent<SpriteRenderer>(); renderer.sprite = meterSprite;
                    renderer.color = color; renderer.sortingOrder = order; return renderer;
                }
                Bar("Frame", new Vector3(-.115f, -1.52f, -.1f), new Vector3(.23f, 1.44f, 1), UnityEngine.Color.white, 21);
                Bar("Track", new Vector3(-.075f, -1.52f, -.2f), new Vector3(.15f, 1.36f, 1), new UnityEngine.Color(.08f, .12f, .16f), 22);
                meterFill = Bar("Remaining", new Vector3(-.075f, -1.52f, -.3f), new Vector3(.15f, 1.36f, 1), UnityEngine.Color.green, 23);
                text = UnityEngine.Object.Instantiate(template, indicator.transform);
                text.transform.localPosition = new Vector3(0f, -2.75f, -1f);
                text.transform.localScale = Vector3.one;
                text.fontSize = 1.05f; text.alignment = TMPro.TextAlignmentOptions.Center;
                text.rectTransform.sizeDelta = new Vector2(3.3f, 1.5f);
                textInMeeting = meeting;
            }
            double remaining = budget.Remaining(total);
            int seconds = (int)Math.Ceiling(remaining);
            float fraction = total > 0 ? (float)Math.Min(1, remaining / total) : 0;
            var color = fraction <= .2f ? UnityEngine.Color.red : fraction <= .5f ? new UnityEngine.Color(1f, .75f, .2f) : new UnityEngine.Color(.55f, 1f, .7f);
            if (meterFill != null)
            {
                float height = fraction * 1.36f;
                meterFill.transform.localScale = new Vector3(.15f, height, 1);
                meterFill.transform.localPosition = new Vector3(-.075f, -2.2f + height / 2, -.3f);
                meterFill.color = color;
            }
            var aspect = indicator!.GetComponent<AspectPosition>();
            aspect.DistanceFromEdge = new Vector3(MyPlayer.TryGetModifier<Bibiri.Instance>(out _) ? 1.5f : .65f, 2.8f, -30f);
            aspect.AdjustPosition();
            text!.gameObject.SetActive(true);
            string status = expired ? "role.tanukiLimitSpeech.expired" : meeting && !ConsumeInMeeting ? "role.tanukiLimitSpeech.meetingPaused" : !budget.Connected(clock.Elapsed.TotalSeconds) ? "role.tanukiLimitSpeech.disconnected" : "role.tanukiLimitSpeech.remaining";
            int extra = (int)Math.Ceiling(Math.Max(0, remaining - total));
            string overflow = extra > 0 ? "\n<size=70%><nobr>" + NebulaAPI.Language.Translate("role.tanukiLimitSpeech.extra").Replace("{0}", extra.ToString()) + "</nobr></size>" : "";
            text!.text = "<nobr>" + (seconds / 60) + ":" + (seconds % 60).ToString("D2") + "</nobr>" + overflow + "\n<size=55%>" + NebulaAPI.Language.Translate(status) + "</size>";
            text.color = color;
        }
        private void DestroyIndicator() { if (indicator != null) UnityEngine.Object.Destroy(indicator); indicator = null; text = null; meterFill = null; }
        protected override void OnReleased() { LimitSpeechBroadcast.Set(MyPlayer.PlayerId, false); bridge?.Dispose(); DestroyIndicator(); base.OnReleased(); }
    }
}

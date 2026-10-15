using System;
using System.Diagnostics;

using Nebula.Utilities;
using Virial;
using Virial.Assignable;
using Virial.Attributes;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Game;
using Virial.Text;
using UnityEngine;

namespace TanukiBCL.Roles;

public sealed class Bibiri : DefinedAllocatableModifierTemplate, DefinedAllocatableModifier
{
    private static readonly FloatConfiguration Threshold = NebulaAPI.Configurations.Configuration(
        "options.role.tanukiBclBibiri.threshold", (-50f, -5f, 1f), -12f, VoiceLevelDisplay.Format);
    private static readonly BoolConfiguration DuringMeeting = NebulaAPI.Configurations.Configuration(
        "options.role.tanukiBclBibiri.duringMeeting", false);
    public static readonly Bibiri MyRole = new();

    private Bibiri() : base("tanukiBclBibiri", "TBIB", new Virial.Color(196, 164, 112),
        new IConfiguration[] { Threshold, DuringMeeting })
    {
        ConfigurationHolder!.Illustration = VoiceRoleIcons.Bibiri;
        ConfigurationHolder.AppendConfiguration(MicrophoneTest.Option(() => Threshold));
    }
    Virial.Media.Image? DefinedAssignable.IconImage => VoiceRoleIcons.Bibiri;

    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(Player player, int[] arguments)
        => new Instance(player);

    public sealed class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;
        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo, bool inEndScene)
        {
            if (AmOwner || canSeeAllInfo) name += " [" + NebulaAPI.Language.Translate("role.tanukiBclBibiri.short") + "]";
        }
        private SharedVoiceBridge? bridge;
        private bool killPending;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private double nextBridgeAttempt;
        private GameObject? indicator;
        private SpriteRenderer? icon;
        private SpriteRenderer? meterFill;
        private static Sprite? meterSprite;
        private TMPro.TextMeshPro? text;
        private bool indicatorInMeeting;
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
            try { bridge = new SharedVoiceBridge(MyPlayer.PlayerId); LastBridgeError = null; }
            catch (Exception ex) { LastBridgeError = ex.Message; }
        }

        [Local]
        private void Update(GameUpdateEvent ev)
        {
            if (!MyPlayer.IsDead && !MyPlayer.IsDisconnected) OpenBridge();
            bool eligible = !MyPlayer.IsDead && !killPending &&
                AmongUsClient.Instance != null &&
                AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started &&
                (MeetingHud.Instance == null || DuringMeeting) && ExileController.Instance == null;
            try
            {
                if (bridge != null && bridge.Poll(eligible, Threshold, 0))
                {
                    killPending = true;
                    MyPlayer.Suicide(PlayerStates.Suicide, null, MeetingHud.Instance != null ? KillParameter.MeetingKill : KillParameter.NormalKill,
                        result => {
                            if (result == KillResult.Kill) BibiriEcho.Notify(MyPlayer.PlayerId);
                            else killPending = false;
                        });
                }
            }
            catch (Exception ex)
            {
                LastBridgeError = ex.Message;
                bridge?.Dispose();
                bridge = null;
                killPending = false;
            }
            ShowVolume(eligible);
        }

        private void ShowVolume(bool eligible)
        {
            if (MyPlayer.IsDead || MyPlayer.IsDisconnected) { DestroyIndicator(); return; }
            bool meeting = MeetingHud.Instance != null;
            if (meeting && !DuringMeeting) { DestroyIndicator(); return; }
            if (text == null || indicatorInMeeting != meeting)
            {
                DestroyIndicator();
                var template = meeting ? MeetingHud.Instance?.TitleText : HudManager.Instance?.KillButton?.cooldownTimerText;
                var parent = meeting ? MeetingHud.Instance?.transform : HudManager.Instance?.transform;
                if (template == null || parent == null) return;
                indicator = new GameObject("TanukiBibiriVolume");
                indicator.transform.SetParent(parent, false);
                indicator.SetAsUIAspectContent(AspectPosition.EdgeAlignments.RightTop, new Vector3(.65f, 1.8f, -30f));
                indicator.GetComponent<AspectPosition>().updateAlways = true;
                indicator.transform.localScale = Vector3.one * .55f;
                icon = indicator.AddComponent<SpriteRenderer>();
                icon.sprite = VoiceRoleIcons.BibiriSprite;
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
                meterFill = Bar("Voice", new Vector3(-.075f, -2.2f, -.3f), new Vector3(.15f, 0, 1), UnityEngine.Color.green, 23);
                Bar("DeathLine", new Vector3(-.15f, -.84f, -.4f), new Vector3(.3f, .04f, 1), UnityEngine.Color.red, 24);
                text = UnityEngine.Object.Instantiate(template, indicator.transform);
                text.transform.localPosition = new Vector3(0f, -2.65f, -1f);
                text.transform.localScale = Vector3.one;
                text.fontSize = 1.25f;
                text.alignment = TMPro.TextAlignmentOptions.Center;
                text.rectTransform.sizeDelta = new Vector2(3.3f, 1.1f);
                indicatorInMeeting = meeting;
            }
            bool connected = bridge?.Connected ?? false;
            indicator!.GetComponent<AspectPosition>().AdjustPosition();
            text!.gameObject.SetActive(true);
            double percent = connected && bridge!.LatestActive ? bridge.LatestRms / Math.Pow(10, (float)Threshold / 20) * 100 : 0;
            var color = !eligible || !connected ? UnityEngine.Color.gray : percent >= 100 ? UnityEngine.Color.red
                : percent >= 70 ? new UnityEngine.Color(1f, .75f, .2f) : new UnityEngine.Color(.55f, 1f, .7f);
            string status = !eligible ? "paused" : !connected ? "disconnected" : percent >= 100 ? "danger" : "meter";
            text!.text = (connected ? "<nobr>" + Math.Floor(Math.Min(999, percent)) + "%</nobr>" : "—")
                + "\n<size=55%>" + NebulaAPI.Language.Translate("role.tanukiBclBibiri." + status) + "</size>";
            text.color = color;
            if (icon != null) icon.color = color;
            if (meterFill != null)
            {
                float height = (float)Math.Min(1, percent / 100) * 1.36f;
                meterFill.transform.localScale = new Vector3(.15f, height, 1);
                meterFill.transform.localPosition = new Vector3(-.075f, -2.2f + height / 2, -.3f);
                meterFill.color = color;
            }
        }

        private void DestroyIndicator()
        {
            if (indicator != null) UnityEngine.Object.Destroy(indicator);
            indicator = null; icon = null; text = null; meterFill = null;
        }

        protected override void OnReleased()
        {
            bridge?.Dispose();
            bridge = null;
            DestroyIndicator();
            base.OnReleased();
        }
    }
}

using System;
using Nebula.Modules;
using Nebula.Utilities;
using Virial;
using Virial.Assignable;
using Virial.Attributes;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial.Game;
using UnityEngine;

namespace TanukiBCL.Roles;

public sealed class DyingVoiceMessage : DefinedAllocatableModifierTemplate, DefinedAllocatableModifier, IAssignableDocument
{
    private static readonly FloatConfiguration Seconds = NebulaAPI.Configurations.Configuration(
        "options.role.tanukiDyingVoice.seconds", (1f, 30f, 1f), 5f, FloatConfigurationDecorator.Second);
    private static readonly FloatConfiguration Range = NebulaAPI.Configurations.Configuration(
        "options.role.tanukiDyingVoice.range", (.5f, 10f, .5f), 2.5f);
    private static readonly BoolConfiguration ShowIcon = NebulaAPI.Configurations.Configuration(
        "options.role.tanukiDyingVoice.showIcon", true);
    private static readonly BoolConfiguration LoopPlayback = NebulaAPI.Configurations.Configuration(
        "options.role.tanukiDyingVoice.loopPlayback", false);
    public static readonly DyingVoiceMessage MyRole = new();
    private DyingVoiceMessage() : base("tanukiDyingVoice", "TDVM", new Virial.Color(205, 10, 19),
        new IConfiguration[] { Seconds, Range, ShowIcon, LoopPlayback })
    {
        ConfigurationHolder!.Illustration = DyingVoiceVisual.Icon;
        ConfigurationHolder.AddTags(AddonRoleTag.Tag);
    }
    Virial.Media.Image? DefinedAssignable.IconImage => DyingVoiceVisual.Icon;
    bool IAssignableDocument.HasTips => true;
    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(Player player, int[] arguments) => new Instance(player);

    private static readonly RemoteProcess<(Player player, float x, float y)> DeathRpc = new("TanukiDyingVoiceV1",
        (data, _) => {
            if (data.player.TryGetModifier<Instance>(out var instance)) instance.Begin(new Vector2(data.x, data.y));
        });

    public sealed class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;
        // Delegate visibility to NoS's built-in unaware modifier, without
        // activating or assigning the reference instance to the player.
        private readonly RuntimeModifier awarenessReference;
        bool RuntimeAssignable.CanBeAwareAssignment => awarenessReference.CanBeAwareAssignment;
        private bool recorded;
        private GameObject? marker;
        private DyingVoiceRecordingUI? recordingUI;
        private Vector2 location;
        private long started;
        private float duration, range;
        public Instance(Player player) : base(player)
        {
            awarenessReference = ((RuntimeAssignableGenerator<RuntimeModifier>)Nebula.Roles.Modifier.DyingMessage.MyRole)
                .CreateInstance(player, Array.Empty<int>());
        }
        void RuntimeAssignable.OnActivated() { }
        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo, bool inEndScene)
        {
            if (canSeeAllInfo || inEndScene) name += " [" + NebulaAPI.Language.Translate("role.tanukiDyingVoice.short") + "]";
        }

        [OnlyMyPlayer]
        private void OnDeath(PlayerDieEvent ev)
        {
            if (!AmOwner || recorded) return;
            var position = MyPlayer.TruePosition;
            DeathRpc.Invoke((MyPlayer, position.x, position.y));
        }

        internal void Begin(Vector2 position)
        {
            if (recorded || !float.IsFinite(position.x) || !float.IsFinite(position.y)) return;
            recorded = true; location = position;
            started = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            duration = Seconds; range = Range;
            Publish();
        }

        private void Publish() => DyingVoiceBroadcast.Set(MyPlayer.PlayerId, started, duration, range, location.x, location.y, LoopPlayback);

        private void Update(GameUpdateEvent ev)
        {
            if (!recorded) return;
            if (!MyPlayer.IsDead || MyPlayer.IsDisconnected)
            {
                Clear(); recorded = false;
                return;
            }
            Publish();
            bool ready = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - started >= duration * 1000;
            UpdateRecordingUI(ready);
            // Visibility follows the viewer, not the dead message owner.
            bool ghostViewer = PlayerControl.LocalPlayer?.Data?.IsDead == true;
            if (!ShowIcon && !ghostViewer)
            {
                if (marker != null) marker.SetActive(false);
                return;
            }
            if (marker == null)
            {
                marker = new GameObject("TanukiDyingVoiceMessage");
                var image = marker.AddComponent<SpriteRenderer>();
                image.sprite = DyingVoiceVisual.GetSprite();
                marker.transform.localScale = Vector3.one * .4f;
            }
            marker.transform.position = new Vector3(location.x, location.y, location.y / 1000f - .01f);
            marker.SetActive(ready && MeetingHud.Instance == null && ExileController.Instance == null);
        }

        private void UpdateRecordingUI(bool ready)
        {
            if (!AmOwner) return;
            if (ready)
            {
                recordingUI?.Dispose();
                recordingUI = null;
                return;
            }
            recordingUI ??= DyingVoiceRecordingUI.Create();
            float elapsed = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - started) / 1000f;
            recordingUI?.Update(elapsed, duration, MeetingHud.Instance == null && ExileController.Instance == null);
        }

        private void Clear()
        {
            DyingVoiceBroadcast.Remove(MyPlayer.PlayerId);
            if (marker != null) UnityEngine.Object.Destroy(marker);
            recordingUI?.Dispose();
            recordingUI = null;
            marker = null;
        }
        private void OnGameEnd(GameEndEvent ev) { Clear(); recorded = false; }
        protected override void OnReleased() { Clear(); base.OnReleased(); }
    }
}

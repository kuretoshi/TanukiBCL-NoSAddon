using System;
using Nebula.Modules;
using Nebula.Utilities;
using UnityEngine;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Game;

namespace TanukiBCL.Roles;

public sealed class BitCharacter : DefinedAllocatableModifierTemplate, DefinedAllocatableModifier, IAssignableDocument
{
    private static readonly FloatConfiguration Two = Duration("2");
    private static readonly FloatConfiguration Four = Duration("4");
    private static readonly FloatConfiguration Eight = Duration("8");
    private static readonly FloatConfiguration Sixteen = Duration("16");
    private static readonly FloatConfiguration ThirtyTwo = Duration("32");
    private static readonly FloatConfiguration SixtyFour = Duration("64");
    private static FloatConfiguration Duration(string stage) => NebulaAPI.Configurations.Configuration(
        "options.role.tanukiBitCharacter.seconds" + stage, (5f, 180f, 5f), 30f);
    public static readonly BitCharacter MyRole = new();
    private static readonly RemoteProcess<(Player player, int stage)> StageRpc = new("TanukiBitCharacterStageV1", (data, _) => {
        if (data.player.TryGetModifier<Instance>(out var modifier)) modifier.ChangeStage(data.stage);
    });
    private BitCharacter() : base("tanukiBitCharacter", "TBIT", new Virial.Color(155, 210, 235),
        new IConfiguration[] { Two, Four, Eight, Sixteen, ThirtyTwo, SixtyFour })
    {
        ConfigurationHolder!.Illustration = BitCharacterVisual.Icon;
        ConfigurationHolder.AddTags(AddonRoleTag.Tag);
    }
    Virial.Media.Image? DefinedAssignable.IconImage => BitCharacterVisual.Icon;
    bool IAssignableDocument.HasTips => true;
    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(Player player, int[] arguments) => new Instance(player);

    public sealed class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;
        private readonly BitCharacterProgress progress;
        private BitCharacterVisual? visual;
        private int stage;
        private bool visible;
        private float nextTry;
        public Instance(Player player) : base(player) => progress = new((float)Two, (float)Four, (float)Eight, (float)Sixteen, (float)ThirtyTwo, (float)SixtyFour);
        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo, bool inEndScene)
        {
            if ((AmOwner || canSeeAllInfo) && stage < BitCharacterProgress.FinalStage) name += " [" + BitCharacterProgress.Bits[stage] + "bit]";
        }
        void RuntimeAssignable.OnActivated() => ChangeStage(0);
        public void ChangeStage(int next)
        {
            if (next < stage || next < 0 || next > BitCharacterProgress.FinalStage) return;
            if (next == stage && visible) return;
            stage = next;
            visual?.Dispose(); visual = null;
            visible = false;
            Refresh();
        }
        private void Refresh()
        {
            if (MyPlayer.IsDead || MyPlayer.IsDisconnected)
            {
                BitCharacterBroadcast.Remove(MyPlayer.PlayerId);
                visual?.Dispose(); visual = null; visible = false;
                return;
            }
            BitCharacterBroadcast.Set(MyPlayer.PlayerId, stage);
            if (stage == BitCharacterProgress.FinalStage || visible || Time.unscaledTime < nextTry) return;
            nextTry = Time.unscaledTime + 1f;
            try { visual = new BitCharacterVisual(MyPlayer, stage); visible = true; }
            catch (Exception ex) { NebulaAPI.Logging.NebulaLogger("TanukiBit").Error(ex.ToString()); }
        }
        private void Update(GameUpdateEvent ev)
        {
            Refresh();
            visual?.Update();
            bool running = AmOwner && !MyPlayer.IsDead && !MyPlayer.IsDisconnected &&
                AmongUsClient.Instance != null && AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started &&
                IntroCutscene.Instance == null && MeetingHud.Instance == null && ExileController.Instance == null;
            if (progress.Advance(Time.deltaTime, running)) StageRpc.Invoke((MyPlayer, progress.Stage));
        }
        private void LateUpdate(GameLateUpdateEvent ev) => visual?.LateUpdate();
        protected override void OnReleased()
        {
            BitCharacterBroadcast.Remove(MyPlayer.PlayerId);
            visual?.Dispose(); visual = null;
            base.OnReleased();
        }
    }
}

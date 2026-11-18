using System;
using System.Linq;
using System.Collections.Generic;
using Nebula.Modules.Cosmetics;
using Nebula.Utilities;
using UnityEngine;
using Virial;
using Virial.Game;
using Virial.Media;

namespace TanukiBCL.Roles;

public sealed class BitCharacterVisual : IDisposable
{
    public static readonly Image Icon = new WrapSpriteLoader(LoadIcon);
    private static Sprite? icon;
    private readonly HeroCostume costume;
    private readonly PlayerControl vanilla;
    private readonly int bits;
    private BitCharacterNativeVisual? meetingVisual;
    private PoolablePlayer? meetingAvatar;
    private int meetingId;
    private readonly Dictionary<int, (SpriteRenderer renderer, bool hidden)> meetingOriginals = new();
    public BitCharacterVisual(Player player, int stage)
    {
        vanilla = PlayerControl.AllPlayerControls.ToArray().FirstOrDefault(p => p.PlayerId == player.PlayerId && p.Data != null)
            ?? throw new InvalidOperationException("Bit character player is not available.");
        bits = BitCharacterProgress.Bits[stage];
        costume = new HeroCostume(vanilla, "BitCharacter" + bits, true);
    }
    private static Sprite LoadIcon()
    {
        if (icon != null) return icon;
        using var stream = NebulaAPI.GetAddon("tanuki.bcl.roles")?.GetResource("BitCharacter/Icon.png")?.AsStream()
            ?? throw new InvalidOperationException("Bit character icon is missing.");
        using var bytes = new System.IO.MemoryStream(); stream.CopyTo(bytes);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        GraphicsHelper.LoadImage(texture, bytes.ToArray(), false);
        icon = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), texture.width / 1.45f);
        return icon;
    }
    public void Update() { costume.Update(); UpdateMeeting(); }
    private void UpdateMeeting()
    {
        var hud = MeetingHud.Instance;
        if (hud == null) { RestoreMeeting(); return; }
        if (hud.GetInstanceID() == meetingId) { meetingVisual?.Update(true); return; }
        var area = hud.playerStates.ToArray().FirstOrDefault(p => p.PlayerId.Value == vanilla.PlayerId);
        if (area == null || area.PlayerIcon == null) return;
        RestoreMeeting();
        meetingId = hud.GetInstanceID();
        var avatar = area.PlayerIcon;
        var layer = avatar.cosmetics;
        // Preserve NoS's anonymous/camouflaged meeting portrait.
        if (layer.ColorId != vanilla.cosmetics.ColorId) return;
        meetingAvatar = avatar;
        foreach (var body in layer.bodySprites)
        {
            var renderer = body.BodySprite;
            if (renderer == null) continue;
            meetingOriginals[renderer.GetInstanceID()] = (renderer, renderer.forceRenderingOff);
            renderer.forceRenderingOff = true;
        }
        layer.SetSkin(CosmeticsLayer.EMPTY_SKIN_ID, layer.ColorId);
        layer.SetVisor(CosmeticsLayer.EMPTY_VISOR_ID, layer.ColorId);
        layer.SetHat(CosmeticsLayer.EMPTY_HAT_ID, layer.ColorId);
        meetingVisual = new BitCharacterNativeVisual(layer, bits);
        meetingVisual.Update(true);
    }
    private void RestoreMeeting()
    {
        meetingVisual?.Dispose(); meetingVisual = null;
        foreach (var entry in meetingOriginals.Values)
            if (entry.renderer != null) entry.renderer.forceRenderingOff = entry.hidden;
        meetingOriginals.Clear();
        if (meetingAvatar != null && vanilla != null && vanilla.Data != null)
            meetingAvatar.UpdateFromPlayerOutfit(vanilla.CurrentOutfit, PlayerMaterial.MaskType.ComplexUI, false, false);
        meetingAvatar = null; meetingId = 0;
    }
    public void LateUpdate() => costume.LateUpdate();
    public void Dispose() { RestoreMeeting(); costume.Dispose(); }
}

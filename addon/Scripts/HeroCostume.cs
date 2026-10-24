using System;
using System.Collections.Generic;
using Nebula.Modules.Cosmetics;
using UnityEngine;

namespace TanukiBCL.Roles;

// Every peer applies this locally after the transformation RPC. Saved wardrobe
// items and NoS's registered outfit definitions remain unchanged.
public sealed class HeroCostume : IDisposable
{
    private readonly Dictionary<int, (SpriteRenderer renderer, bool hidden)> originals = new();
    private readonly PlayerControl vanilla;
    private readonly CosmicHat hat;
    private readonly Transform? handSlot;
    private readonly PowerTools.SpriteAnimNodeSync? motionNode;
    private readonly bool motionEnabled;
    private bool disposed;
    private float nextCheck;
    private int walkSamples;
    private float walkMinY = float.PositiveInfinity, walkMaxY = float.NegativeInfinity;
    private readonly HashSet<int> walkFrames = new();

    public HeroCostume(PlayerControl vanilla, HeroKind kind)
    {
        this.vanilla = vanilla;
        string id = CosmicHat.IdToProductId("TanukiBCL_" + (kind == HeroKind.Super ? "SuperHero" : "EvilHero"));
        if (!MoreCosmic.AllHats.TryGetValue(id, out var registered) || registered.Main?.GetSprite(0) == null)
            throw new InvalidOperationException("Hero MoreCosmic costume is not loaded: " + id);
        hat = registered;
        handSlot = vanilla.transform.Find("HandSlot");
        motionNode = vanilla.cosmetics.hat.SpriteSyncNode;
        motionEnabled = motionNode != null && motionNode.enabled;
        try { Apply(); Update(); }
        catch { Dispose(); throw; }
    }
    private void Apply()
    {
        var layer = vanilla.cosmetics;
        int color = layer.ColorId;
        layer.SetSkin(CosmeticsLayer.EMPTY_SKIN_ID, color);
        layer.SetVisor(CosmeticsLayer.EMPTY_VISOR_ID, color);
        layer.SetHat(hat.MyHat, color);
        Virial.NebulaAPI.Logging.NebulaLogger("TanukiHero").Message("MoreCosmic costume applied: " + hat.ProductId);
    }
    public void Update()
    {
        if (disposed || vanilla == null || vanilla.cosmetics == null) return;
        // The body is outside CosmeticsLayer, and its active renderer can change.
        // Instance IDs keep repeated IL2CPP wrappers from replacing the snapshot.
        foreach (var sprite in vanilla.cosmetics.bodySprites) Hide(sprite.BodySprite);
        if (handSlot != null)
            foreach (var renderer in handSlot.GetComponentsInChildren<SpriteRenderer>(true)) Hide(renderer);
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + .1f;
        var layer = vanilla.cosmetics;
        if (layer.hat.Hat == null || layer.hat.Hat.ProductId != hat.ProductId) Apply();
    }
    public void LateUpdate()
    {
        if (disposed || vanilla == null || vanilla.MyPhysics == null) return;
        // Even the no-bounce hat node moves vertically through the run cycle.
        // Full-body artwork needs the layer's stationary hat origin instead.
        // Release the native node immediately for idle, ladder and vent states.
        if (!vanilla.MyPhysics.Animations.IsPlayingRunAnimation())
        {
            RestoreMotion();
            return;
        }
        var layer = vanilla.cosmetics;
        if (motionNode == null) return;
        // Disabling only this synchronizer prevents its later callback from
        // overwriting the fixed position; MoreCosmic still selects walk frames.
        motionNode.enabled = false;
        // NoS's normal idle cosmetic node is at y=.575 (also used by its fixed
        // visor). defaultHatVisorPosition is zero in-game, not that idle origin.
        var position = new Vector3(0f, .575f, 0f) + (layer.FlipX ? motionNode.flipOffset : motionNode.normalOffset);
        position.z = layer.hat.transform.localPosition.z;
        layer.hat.transform.localPosition = position;
        layer.hat.transform.localRotation = Quaternion.identity;
        layer.hat.FrontLayer.flipX = layer.FlipX;
        layer.hat.BackLayer.flipX = layer.FlipX;
        walkSamples++;
        walkMinY = Mathf.Min(walkMinY, position.y);
        walkMaxY = Mathf.Max(walkMaxY, position.y);
        var frame = layer.hat.FrontLayer.sprite;
        if (frame != null && frame.texture.width == 2400) walkFrames.Add(frame.GetInstanceID());
    }
    private void RestoreMotion()
    {
        if (motionNode != null) motionNode.enabled = motionEnabled;
    }
    private void Hide(SpriteRenderer renderer)
    {
        if (renderer == null) return;
        int id = renderer.GetInstanceID();
        if (!originals.ContainsKey(id)) originals.Add(id, (renderer, renderer.forceRenderingOff));
        renderer.forceRenderingOff = true;
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        RestoreMotion();
        if (walkSamples > 0)
            Virial.NebulaAPI.Logging.NebulaLogger("TanukiHero").Message($"Walk: {hat.ProductId}, samples={walkSamples}, moveFrames={walkFrames.Count}, attachmentY={walkMinY:F4}..{walkMaxY:F4}");
        foreach (var entry in originals.Values)
            if (entry.renderer != null) entry.renderer.forceRenderingOff = entry.hidden;
        originals.Clear();
        if (vanilla == null || vanilla.Data == null || vanilla.cosmetics == null) return;
        // Read NoS's current outfit so another ability's changes are preserved.
        var outfit = vanilla.CurrentOutfit;
        vanilla.cosmetics.SetHat(outfit.HatId, outfit.ColorId);
        vanilla.cosmetics.SetVisor(outfit.VisorId, outfit.ColorId);
        vanilla.cosmetics.SetSkin(outfit.SkinId, outfit.ColorId);
        Virial.NebulaAPI.Logging.NebulaLogger("TanukiHero").Message("Restored normal costume: " + outfit.HatId + " / " + outfit.SkinId);
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace TanukiBCL.Roles;

// A dedicated seated pose with knees forward and a detached gripping hand. Use the
// exact same four-frame loader and palm-to-native-grip alignment as heroes.
public sealed class BitCharacterZiplineVisual : IDisposable
{
    private static readonly Dictionary<int, Sprite[]> frames = new();
    private static readonly int MainTexture = Shader.PropertyToID("_MainTex");
    private readonly int bits;
    private readonly MaterialPropertyBlock properties = new();
    private SpriteRenderer? hand;
    private SpriteRenderer? renderer;
    private bool wasHidden;
    private float started;
    public BitCharacterZiplineVisual(int bits)
    {
        if (bits is not 2 and not 4 and not 8 and not 16 and not 32) throw new ArgumentOutOfRangeException(nameof(bits));
        this.bits = bits;
    }

    public bool TryApply(PlayerControl player)
    {
        var current = HeroZiplineVisual.FindHand(player);
        var body = player.cosmetics.currentBodySprite.BodySprite;
        if (current == null || !player.cosmetics.Visible || body == null || !body.enabled || !body.gameObject.activeInHierarchy)
        { Dispose(); return false; }
        if (hand == null || hand.GetInstanceID() != current.GetInstanceID())
        {
            Dispose();
            hand = current;
            wasHidden = current.forceRenderingOff;
            started = Time.unscaledTime;
        }
        // Respect a hand which another game effect had already hidden. The
        // ordinary bit body can remain visible without exposing that hand.
        if (wasHidden) return false;
        if (!frames.TryGetValue(bits, out var poses))
            frames[bits] = poses = HeroZiplineVisual.LoadFrames("BitCharacter/Zipline" + bits + ".png", FilterMode.Point);
        if (renderer == null)
        {
            var obj = new GameObject("TanukiBitZiplineBody");
            renderer = obj.AddComponent<SpriteRenderer>();
        }
        renderer.enabled = false;
        // The visible body has its own cosmetic scale (normally .35), which
        // the PlayerControl root does not have. Use the same transform basis
        // as ordinary bit rendering, including live body/scale changes.
        if (renderer.transform.parent != body.transform)
            renderer.transform.SetParent(body.transform, false);
        renderer.transform.localPosition = Vector3.zero;
        renderer.transform.localRotation = Quaternion.identity;
        renderer.transform.localScale = Vector3.one;
        renderer.gameObject.layer = body.gameObject.layer;
        renderer.sprite = poses[HeroCostumeMotion.Frame((Time.unscaledTime - started) / .56f, 4)];
        renderer.sharedMaterial = body.sharedMaterial;
        renderer.color = body.color;
        renderer.flipX = player.cosmetics.FlipX;
        renderer.flipY = body.flipY;
        renderer.maskInteraction = body.maskInteraction;
        renderer.sortingLayerID = body.sortingLayerID;
        renderer.sortingOrder = body.sortingOrder;
        renderer.sortingGroupOrder = body.sortingGroupOrder;
        body.GetPropertyBlock(properties);
        properties.SetTexture(MainTexture, renderer.sprite.texture);
        if (bits == 2)
        {
            properties.SetColor(PlayerMaterial.BodyColor, new Color(2f/3f, 2f/3f, 2f/3f, 1));
            properties.SetColor(PlayerMaterial.BackColor, new Color(1f/3f, 1f/3f, 1f/3f, 1));
            properties.SetColor(PlayerMaterial.VisorColor, new Color(1, 1, 1, 1));
        }
        renderer.SetPropertyBlock(properties);
        HeroZiplineVisual.AlignGrip(renderer.transform, renderer, current);
        renderer.enabled = true;
        current.forceRenderingOff = true;
        return true;
    }

    public void Dispose()
    {
        if (renderer != null)
        {
            renderer.enabled = false;
            UnityEngine.Object.Destroy(renderer.gameObject);
            renderer = null;
        }
        if (hand != null) hand.forceRenderingOff = wasHidden;
        hand = null;
    }
}

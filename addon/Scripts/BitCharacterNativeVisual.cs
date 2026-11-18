using System;
using System.Collections.Generic;
using UnityEngine;

namespace TanukiBCL.Roles;

// Follow the real body sprite, including its animation pivot, geometry and
// baked floor shadow. Lower stages hold native stride/contact poses while
// movement, player color/opacity and collision remain live.
public sealed class BitCharacterNativeVisual : IDisposable
{
    private static readonly Dictionary<(int texture, int bits), Texture2D> atlases = new();
    private static int consumers;
    private static readonly int MainTexture = Shader.PropertyToID("_MainTex");
    private readonly PlayerControl? player;
    private readonly CosmeticsLayer cosmetics;
    private readonly int bits;
    private readonly SpriteRenderer renderer;
    private readonly MaterialPropertyBlock properties = new();
    private bool disposed;
    private Sprite? heldWalk;
    private long walkTick = long.MinValue;
    private int walkBody;

    public BitCharacterNativeVisual(PlayerControl player, int bits) : this(player.cosmetics, bits)
    {
        this.player = player;
    }

    public BitCharacterNativeVisual(CosmeticsLayer cosmetics, int bits)
    {
        if (bits is not 2 and not 4 and not 8 and not 16 and not 32) throw new ArgumentOutOfRangeException(nameof(bits));
        this.cosmetics = cosmetics;
        this.bits = bits;
        var obj = new GameObject("TanukiBitNativeBody");
        renderer = obj.AddComponent<SpriteRenderer>();
        renderer.enabled = false;
        consumers++;
    }

    private static Texture2D PixelAtlas(Texture2D source, int bits)
    {
        var key = (source.GetInstanceID(), bits);
        if (atlases.TryGetValue(key, out var cached) && cached != null) return cached;
        int cell = bits == 2 ? 16 : bits == 4 ? 12 : bits == 8 ? 8 : bits == 16 ? 6 : 3;
        int width = (source.width + cell - 1) / cell, height = (source.height + cell - 1) / cell;
        var target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32,
            source.isDataSRGB ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
        var previous = RenderTexture.active;
        var filter = source.filterMode;
        bool srgb = GL.sRGBWrite;
        Texture2D? texture = null;
        try
        {
            // UVs and sprite mesh/pivot are kept exactly as the native sprite.
            // Cache one small mask atlas per stage, shared across all players,
            // without reading or cloning the full atlas on every animation frame.
            source.filterMode = FilterMode.Point;
            GL.sRGBWrite = source.isDataSRGB && QualitySettings.activeColorSpace == ColorSpace.Linear;
            Graphics.Blit(source, target);
            RenderTexture.active = target;
            texture = new Texture2D(width, height, TextureFormat.RGBA32, false, !source.isDataSRGB) {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
            };
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            if (bits == 2)
            {
                var pixels = texture.GetPixels32();
                for (int i = 0; i < pixels.Length; i++) pixels[i] = TwoBitMask(pixels[i]);
                texture.SetPixels32(pixels);
            }
            texture.Apply(false, true);
            atlases[key] = texture;
            return texture;
        }
        catch
        {
            if (texture != null) UnityEngine.Object.Destroy(texture);
            throw;
        }
        finally
        {
            source.filterMode = filter;
            GL.sRGBWrite = srgb;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
        }
    }

    private static Color32 TwoBitMask(Color32 color)
    {
        byte r = color.r, g = color.g, b = color.b;
        if (Math.Max(r, Math.Max(g, b)) < 48) return new Color32(0, 0, 0, color.a);
        if (g >= r && g >= b) return new Color32(0, 255, 0, color.a);
        return r >= b ? new Color32(255, 0, 0, color.a) : new Color32(0, 0, 255, color.a);
    }

    private void ResetWalk() { heldWalk = null; walkTick = long.MinValue; walkBody = 0; }

    private Sprite SelectSprite(SpriteRenderer body)
    {
        var animations = player?.MyPhysics?.Animations;
        if (bits > 8 || animations == null || !animations.IsPlayingRunAnimation())
        {
            ResetWalk();
            return body.sprite;
        }
        long tick = BitCharacterMotion.WalkTick(animations.Animator.NormalizedTime);
        if (tick == long.MinValue) { ResetWalk(); return body.sprite; }
        if (heldWalk == null || walkTick != tick || walkBody != body.GetInstanceID() || heldWalk.texture != body.sprite.texture)
        {
            heldWalk = body.sprite;
            walkTick = tick;
            walkBody = body.GetInstanceID();
        }
        return heldWalk!;
    }

    public void Update(bool active)
    {
        if (disposed || renderer == null) return;
        renderer.enabled = false;
        if (!active || (player != null && (player.Data == null || player.Data.IsDead))) { ResetWalk(); return; }
        var layer = player != null ? player.cosmetics : cosmetics;
        var body = layer?.currentBodySprite?.BodySprite;
        if (body == null || body.sprite == null || !layer!.Visible || !body.enabled || !body.gameObject.activeInHierarchy) { ResetWalk(); return; }
        if (renderer.transform.parent != body.transform)
        {
            renderer.transform.SetParent(body.transform, false);
            renderer.transform.localPosition = Vector3.zero;
            renderer.transform.localRotation = Quaternion.identity;
            renderer.transform.localScale = Vector3.one;
        }
        renderer.gameObject.layer = body.gameObject.layer;
        renderer.sprite = SelectSprite(body);
        renderer.sharedMaterial = body.sharedMaterial;
        renderer.color = body.color;
        renderer.flipX = body.flipX;
        renderer.flipY = body.flipY;
        renderer.maskInteraction = body.maskInteraction;
        renderer.sortingLayerID = body.sortingLayerID;
        renderer.sortingOrder = body.sortingOrder;
        renderer.sortingGroupOrder = body.sortingGroupOrder;
        body.GetPropertyBlock(properties);
        properties.SetTexture(MainTexture, PixelAtlas(renderer.sprite.texture, bits));
        if (bits == 2)
        {
            // Four grayscale levels via the native adaptive-mask shader.
            properties.SetColor(PlayerMaterial.BodyColor, new Color(2f / 3f, 2f / 3f, 2f / 3f, 1));
            properties.SetColor(PlayerMaterial.BackColor, new Color(1f / 3f, 1f / 3f, 1f / 3f, 1));
            properties.SetColor(PlayerMaterial.VisorColor, new Color(1, 1, 1, 1));
        }
        renderer.SetPropertyBlock(properties);
        renderer.enabled = true;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (renderer != null) { renderer.enabled = false; UnityEngine.Object.Destroy(renderer.gameObject); }
        if (--consumers != 0) return;
        foreach (var texture in atlases.Values) if (texture != null) UnityEngine.Object.Destroy(texture);
        atlases.Clear();
    }
}

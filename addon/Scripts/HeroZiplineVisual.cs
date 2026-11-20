using System;
using System.IO;
using Nebula.Utilities;
using UnityEngine;

namespace TanukiBCL.Roles;

// MoreCosmic has ladder/vent images but no zipline image slot. Follow the
// actual per-player pooled hand so cancellation, meetings and both directions
// end the pose with the base game's ride, rather than an estimated timer.
public sealed class HeroZiplineVisual : IDisposable
{
    internal const float GripPixelX = 220f, GripPixelY = 55f;
    private static readonly Sprite[]?[] sprites = new Sprite[2][];
    private readonly HeroKind kind;
    private SpriteRenderer? hand;
    private bool wasHidden;
    private float started;
    public bool Active => hand != null;

    public HeroZiplineVisual(HeroKind kind) { this.kind = kind; }

    private static Sprite[] Load(HeroKind kind)
    {
        if (sprites[(int)kind] != null) return sprites[(int)kind]!;
        string name = kind == HeroKind.Super ? "SuperZipline.png" : "EvilZipline.png";
        return sprites[(int)kind] = LoadFrames("Heroes/" + name, FilterMode.Bilinear);
    }

    internal static Sprite[] LoadFrames(string resource, FilterMode filter)
    {
        using var stream = Virial.NebulaAPI.GetAddon("tanuki.bcl.roles")?.GetResource(resource)?.AsStream()
            ?? throw new InvalidOperationException("Zipline sprite is missing: " + resource);
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = filter };
        GraphicsHelper.LoadImage(texture, bytes.ToArray(), false);
        if (texture.width != 1200 || texture.height != 375)
        {
            UnityEngine.Object.Destroy(texture);
            throw new InvalidOperationException("Hero zipline strip must contain four 300x375 poses.");
        }
        var frames = new Sprite[4];
        for (int i = 0; i < frames.Length; i++)
            frames[i] = Sprite.Create(texture, new Rect(i * 300, 0, 300, 375), new Vector2(.53f, .575f), 112.875f);
        return frames;
    }

    internal static void AlignGrip(Transform root, SpriteRenderer front, SpriteRenderer nativeHand)
    {
        float x = (GripPixelX - 300f * .53f) / 112.875f;
        float y = (375f * .425f - GripPixelY) / 112.875f;
        var grip = front.transform.TransformPoint(new Vector3(front.flipX ? -x : x, front.flipY ? -y : y, 0f));
        var delta = nativeHand.transform.position - grip;
        root.position += new Vector3(delta.x, delta.y, 0f);
    }

    internal static SpriteRenderer? FindHand(PlayerControl player)
    {
        var ship = ShipStatus.Instance == null ? null : ShipStatus.Instance.TryCast<FungleShipStatus>();
        if (player.Data == null || player.Data.IsDead || ship?.Zipline == null ||
            !ship.Zipline.playerIdHands.TryGetValue(player.PlayerId, out var pooled) || pooled == null ||
            pooled.handRenderer == null || !pooled.handRenderer.enabled || !pooled.handRenderer.gameObject.activeInHierarchy)
            return null;
        return pooled.handRenderer;
    }

    public bool TryApply(PlayerControl player)
    {
        var current = FindHand(player);
        if (current == null) { Dispose(); return false; }
        if (hand == null || hand.GetInstanceID() != current.GetInstanceID())
        {
            Dispose();
            hand = current; wasHidden = current.forceRenderingOff; started = Time.unscaledTime;
        }
        var frames = Load(kind);
        var layer = player.cosmetics;
        var front = layer.hat.FrontLayer;
        front.sprite = frames[HeroCostumeMotion.Frame((Time.unscaledTime - started) / .56f, 4)];
        front.flipX = layer.FlipX;
        layer.hat.BackLayer.sprite = null!;
        var node = layer.hat.SpriteSyncNode;
        var position = new Vector3(0f, .575f, 0f);
        if (node != null) position += layer.FlipX ? node.flipOffset : node.normalOffset;
        position.z = layer.hat.transform.localPosition.z;
        layer.hat.transform.localPosition = position;
        layer.hat.transform.localRotation = Quaternion.identity;
        // All four seated poses have a separate palm at pixel (220,55).
        // Bind that point to the game's moving grip, preserving depth. Rebase
        // first on every update so repeated late callbacks cannot accumulate.
        AlignGrip(layer.hat.transform, front, current);
        current.forceRenderingOff = true;
        return true;
    }

    public void Dispose()
    {
        if (hand != null) hand.forceRenderingOff = wasHidden;
        hand = null;
    }
}

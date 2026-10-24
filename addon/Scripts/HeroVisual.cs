using System;
using System.Linq;
using Nebula.Utilities;
using UnityEngine;
using Virial;
using Virial.Game;
using Virial.Media;

namespace TanukiBCL.Roles;

// Full costume replacement with separate light/dark transformation effects.
public sealed class HeroVisual : IDisposable
{
    public static readonly Image SuperIcon = new WrapSpriteLoader(() => GetSprite(HeroKind.Super, 0));
    public static readonly Image EvilIcon = new WrapSpriteLoader(() => GetSprite(HeroKind.Evil, 0));
    public static readonly Image SuperTransformIcon = new WrapSpriteLoader(() => GetTransformIcon(HeroKind.Super));
    public static readonly Image EvilTransformIcon = new WrapSpriteLoader(() => GetTransformIcon(HeroKind.Evil));
    private static readonly Sprite?[] transformIcons = new Sprite?[2];
    private static readonly Sprite?[,] sprites = new Sprite?[2, 5];
    private readonly PlayerControl? vanilla;
    private readonly GameObject? root;
    private readonly SpriteRenderer? body, aura, burst;
    private readonly HeroCostume? costume;
    private readonly HeroKind kind;
    private readonly Player player;
    private readonly float started;

    public HeroVisual(Player player, HeroKind kind)
    {
        this.kind = kind; this.player = player; started = Time.unscaledTime;
        var local = PlayerControl.LocalPlayer;
        vanilla = local != null && local.PlayerId == player.PlayerId ? local :
            PlayerControl.AllPlayerControls.ToArray().FirstOrDefault(p => p.PlayerId == player.PlayerId && p.Data != null);
        body = vanilla?.cosmetics?.currentBodySprite?.BodySprite;
        NebulaAPI.Logging.NebulaLogger("TanukiHero").Message($"Preparing {kind} costume for player {player.PlayerId}: body={body != null}");
        if (body == null) throw new InvalidOperationException("Hero player body is unavailable.");
        costume = new HeroCostume(vanilla!, kind);
        root = new GameObject("TanukiHeroTransformation");
        root.layer = body.gameObject.layer;
        root.transform.SetParent(body.transform, false);
        // Player sprites have their own pixel density and pivot. Match their
        // visible body instead of inheriting a one-unit canvas at player scale.
        var bounds = body.sprite.bounds;
        root.transform.localPosition = bounds.center;
        root.transform.localScale = new Vector3(bounds.size.y, bounds.size.y, 1f);
        aura = Layer("Aura", 3, .08f, -2);
        burst = Layer("TransformFlash", 3, -.06f, 2);
    }
    private SpriteRenderer Layer(string name, int part, float z, int order)
    {
        // NoS uses an additional sorting-group order for world renderers.
        // A raw SpriteRenderer retains order zero and can disappear behind the map.
        var renderer = UnityHelper.CreateSpriteRenderer(name, root!.transform, new Vector3(0, 0, z));
        renderer.sprite = GetSprite(kind, part);
        renderer.sortingLayerID = body!.sortingLayerID;
        renderer.sortingOrder = body.sortingOrder + order;
        renderer.sortingGroupOrder = Mathf.Max(10, body.sortingGroupOrder) + order;
        renderer.maskInteraction = body.maskInteraction;
        return renderer;
    }
    public void Update()
    {
        if (root == null || body == null || vanilla == null) return;
        costume?.Update();
        root.SetActive(body.enabled && body.gameObject.activeInHierarchy && body.color.a > .01f &&
            !player.IsDead && !player.IsInvisible && !player.IsDived && !vanilla.inVent);
        if (!traced)
        {
            traced = true;
            System.Console.WriteLine($"[TanukiHero] Visual: active={root.activeInHierarchy}, bodyOrder={body.sortingOrder}/{body.sortingGroupOrder}, nativeHat={vanilla.cosmetics.hat.Hat?.ProductId}");
            if (root.activeInHierarchy)
                HeroTransformSound.Play(root, vanilla.GetTruePosition(), kind);
        }
        float elapsed = Time.unscaledTime - started;
        float alpha = body.color.a;
        aura!.transform.localScale = Vector3.one * (1.42f + Mathf.Sin(elapsed * 5f) * .06f);
        aura.transform.localRotation = Quaternion.Euler(0, 0, elapsed * (kind == HeroKind.Super ? 24f : -36f));
        aura.color = new UnityEngine.Color(1, 1, 1, alpha * (.68f + Mathf.Sin(elapsed * 8f) * .12f));
        burst!.transform.localScale = Vector3.one * (1f + Mathf.Clamp01(elapsed / .6f) * 1.5f);
        burst.color = new UnityEngine.Color(1, 1, 1, alpha * Mathf.Clamp01(1 - elapsed / .6f));
    }
    private bool traced;
    public void LateUpdate() => costume?.LateUpdate();
    public void Dispose()
    {
        costume?.Dispose();
        if (root != null) UnityEngine.Object.Destroy(root);
    }
    public static Sprite GetSprite(HeroKind kind, int part)
    {
        if (sprites[(int)kind, part] != null) return sprites[(int)kind, part]!;
        const int size = 256;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++) pixels[y * size + x] = HeroArtwork.Pixel((x + .5f) / 2f, (y + .5f) / 2f, kind, part);
        texture.SetPixels32(pixels); texture.Apply(false, true);
        var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), part == 0 || part == 4 ? 180f : 256f);
        sprites[(int)kind, part] = sprite;
        return sprite;
    }
    private static Sprite GetTransformIcon(HeroKind kind)
    {
        if (transformIcons[(int)kind] != null) return transformIcons[(int)kind]!;
        string name = kind == HeroKind.Super ? "SuperTransform.png" : "EvilTransform.png";
        using var stream = NebulaAPI.GetAddon("tanuki.bcl.roles")?.GetResource("Heroes/" + name)?.AsStream()
            ?? throw new InvalidOperationException("Hero transform icon is missing: " + name);
        using var bytes = new System.IO.MemoryStream();
        stream.CopyTo(bytes);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        GraphicsHelper.LoadImage(texture, bytes.ToArray(), false);
        var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 180f);
        transformIcons[(int)kind] = sprite;
        return sprite;
    }
}

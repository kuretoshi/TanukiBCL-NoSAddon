using System;
using UnityEngine;

namespace TanukiBCL.Roles;

// Full-body hats replace the native body sprite, including its floor shadow.
// Keep the replacement shadow on the cosmetics root so walking cannot lift it.
public sealed class BitCharacterShadow : IDisposable
{
    private static Sprite? sprite;
    private readonly PlayerControl player;
    private readonly SpriteRenderer renderer;

    public BitCharacterShadow(PlayerControl player)
    {
        this.player = player;
        var obj = new GameObject("TanukiBitGroundShadow");
        obj.transform.SetParent(player.cosmetics.hat.transform.parent, false);
        obj.transform.localPosition = new Vector3(-.05f, -.72f, .05f);
        renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = GetSprite();
        renderer.enabled = false;
    }

    private static Sprite GetSprite()
    {
        if (sprite != null) return sprite;
        const int width = 64, height = 12;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float dx = (x + .5f) / width * 2 - 1, dy = (y + .5f) / height * 2 - 1;
                float edge = Mathf.Clamp01((1 - dx * dx - dy * dy) * 8);
                pixels[y * width + x] = new Color32(0, 0, 0, (byte)(edge * 255));
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(.5f, .5f), width / 1.2f);
        return sprite;
    }

    public void Update(bool active)
    {
        if (renderer == null) return;
        renderer.enabled = false;
        if (!active || player == null || player.Data == null || player.Data.IsDead || player.inVent) return;
        var layer = player.cosmetics;
        var animations = player.MyPhysics?.Animations;
        var body = layer?.currentBodySprite?.BodySprite;
        if (body == null || animations == null || !layer!.Visible || !body.enabled || !body.gameObject.activeInHierarchy) return;
        if (animations.IsPlayingAnyLadderAnimation() || animations.IsPlayingEnterVentAnimation() ||
            animations.Animator.GetCurrentAnimation() == animations.group.ExitVentAnim) return;
        renderer.gameObject.layer = body.gameObject.layer;
        renderer.sortingLayerID = body.sortingLayerID;
        // Keep the native body's sorting group; a lower order could put the
        // shadow underneath the map floor. Local Z places it behind the feet.
        renderer.sortingOrder = body.sortingOrder;
        renderer.color = new Color(0, 0, 0, body.color.a * .28f);
        renderer.enabled = true;
    }

    public void Dispose() { if (renderer != null) UnityEngine.Object.Destroy(renderer.gameObject); }
}

using System;
using System.IO;
using Nebula.Utilities;
using UnityEngine;
using Virial.Media;

namespace TanukiBCL.Roles;

public sealed class WallEarVisual : IDisposable
{
    public static readonly Image Icon = VoiceRoleIcons.WallEar;
    private static Sprite? sprite;
    private readonly GameObject root;
    private readonly SpriteRenderer image;
    public WallEarVisual(WallEarRound.Ear ear, Action click)
    {
        root = new GameObject("TanukiWallEar");
        root.layer = LayerMask.NameToLayer("Objects");
        root.transform.position = new Vector3(ear.X, ear.Y + .2f, ear.Y / 1000f - .01f);
        root.transform.localScale = Vector3.one * .45f;
        image = root.AddComponent<SpriteRenderer>();
        image.sprite = GetSprite();
        var collider = root.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(1.3f, 1.4f);
        collider.isTrigger = true;
        var button = root.SetUpButton(true);
        button.OnClick.AddListener(click);
    }
    public void Update(bool visible, bool nearby, bool active)
    {
        if (root == null) return;
        root.SetActive(visible);
        image.color = active ? new Color(.3f, 1, .7f) : nearby ? Color.white : new Color(1, 1, 1, .45f);
    }
    public void Dispose() { if (root != null) UnityEngine.Object.Destroy(root); }
    private static Sprite GetSprite()
    {
        if (sprite != null) return sprite;
        using var stream = Virial.NebulaAPI.GetAddon("tanuki.bcl.roles")?.GetResource("WallEar.png")?.AsStream()
            ?? throw new FileNotFoundException("Missing wall ear sprite in the addon.");
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        GraphicsHelper.LoadImage(texture, bytes.ToArray(), true);
        sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f),
            Mathf.Max(texture.width, texture.height));
        return sprite;
    }
}

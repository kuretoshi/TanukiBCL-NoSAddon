using System;
using System.IO;
using Nebula.Utilities;
using UnityEngine;
using Virial.Media;

namespace TanukiBCL.Roles;

public sealed class ParrotVisual : IDisposable
{
    public static readonly Image Icon = VoiceRoleIcons.ParrotTrainer;
    private static Sprite? sprite;
    private readonly GameObject root;
    private readonly SpriteRenderer image;
    public ParrotVisual(ParrotRound.Bird bird)
    {
        root = new GameObject("TanukiParrot"); root.layer = LayerMask.NameToLayer("Objects");
        root.transform.position = new Vector3(bird.X, bird.Y + .25f, bird.Y / 1000f - .01f);
        root.transform.localScale = Vector3.one * .5f;
        image = root.AddComponent<SpriteRenderer>(); image.sprite = GetSprite();
    }
    public void Update(bool visible, bool recording, bool talking)
    {
        if (root == null) return;
        root.SetActive(visible);
        image.color = recording ? new Color(1f, .65f, .65f) : talking ? new Color(1f, 1f, .5f) : Color.white;
    }
    public void Dispose() { if (root != null) UnityEngine.Object.Destroy(root); }
    private static Sprite GetSprite()
    {
        if (sprite != null) return sprite;
        using var stream = Virial.NebulaAPI.GetAddon("tanuki.bcl.roles")?.GetResource("Parrot.png")?.AsStream()
            ?? throw new FileNotFoundException("Missing parrot sprite in the addon.");
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        GraphicsHelper.LoadImage(texture, bytes.ToArray(), true);
        sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f),
            Mathf.Max(texture.width, texture.height));
        return sprite;
    }
}

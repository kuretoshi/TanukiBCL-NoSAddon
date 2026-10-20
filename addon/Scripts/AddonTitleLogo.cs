using System.IO;
using Nebula.Utilities;
using TMPro;
using UnityEngine;

namespace TanukiBCL.Roles;

public static class AddonTitleLogo
{
    private static Sprite? sprite;
    public static Sprite GetSprite()
    {
        if (sprite) return sprite!;
        using var stream = Virial.NebulaAPI.GetAddon("tanuki.bcl.roles")?.GetResource("TitleLogo.png")?.AsStream()
            ?? throw new FileNotFoundException("Missing title logo in the addon.");
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        GraphicsHelper.LoadImage(texture, bytes.ToArray(), true);
        sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), texture.width / 1.75f);
        return sprite;
    }

    internal static void KeepBelowVersion(Transform title, TextMeshPro reference)
    {
        var logo = title.Find("Logo")?.GetComponent<SpriteRenderer>();
        var referenceRenderer = reference.GetComponent<Renderer>();
        var aspect = title.GetComponent<AspectPosition>();
        if (logo == null || referenceRenderer == null || aspect == null) return;
        reference.ForceMeshUpdate();
        float overlap = logo.bounds.max.y - (referenceRenderer.bounds.min.y - .15f);
        if (overlap <= 0) return;
        var offset = aspect.DistanceFromEdge;
        offset.y += overlap;
        aspect.DistanceFromEdge = offset;
        aspect.AdjustPosition();
    }
}

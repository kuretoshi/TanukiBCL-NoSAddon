using System.IO;
using Nebula.Utilities;
using UnityEngine;
using Virial;
using Virial.Configuration;
using Virial.Media;
using Virial.Text;

namespace TanukiBCL.Roles;

public static class AddonRoleTag
{
    private static Sprite? sprite;
    public static readonly ConfigurationTag Tag = new(new WrapSpriteLoader(GetSprite), () =>
        NebulaAPI.GUI.LocalizedText(GUIAlignment.Left, NebulaAPI.GUI.GetAttribute(AttributeAsset.OverlayContent), "tanuki.roleTag.description"));

    private static Sprite GetSprite()
    {
        if (sprite) return sprite!;
        using var stream = NebulaAPI.GetAddon("tanuki.bcl.roles")?.GetResource("DiscordContact.png")?.AsStream()
            ?? throw new FileNotFoundException("Missing Discord contact icon in the addon.");
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        var art = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        GraphicsHelper.LoadImage(art, bytes.ToArray(), false);
        const int size = 128;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = BadgePixel(art, (x + .5f) / size - .5f, (y + .5f) / size - .5f);
        UnityEngine.Object.Destroy(art);
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        // Native configuration tags are 0.42 units before NoS applies its scale.
        sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size / .42f);
        return sprite;
    }

    private static UnityEngine.Color BadgePixel(Texture2D art, float x, float y)
    {
        // Match the two layers of the existing Discord button, including its
        // blue circle, dark rim and the 0.34 / 0.46 artwork-to-background ratio.
        float radius = new Vector2(x * 96, y * 96).magnitude;
        UnityEngine.Color background = radius > 47 ? new Color32(0, 0, 0, 0)
            : radius > 42 ? new Color32(47, 55, 112, 255) : new Color32(88, 101, 242, 255);
        float u = x * (.46f / .34f) + .5f, v = y * (.46f / .34f) + .5f;
        if (u < 0 || u > 1 || v < 0 || v > 1) return background;
        var foreground = art.GetPixelBilinear(u, v);
        var result = UnityEngine.Color.Lerp(background, foreground, foreground.a);
        result.a = foreground.a + background.a * (1 - foreground.a);
        return result;
    }
}

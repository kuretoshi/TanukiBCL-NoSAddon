using Nebula.Utilities;
using UnityEngine;
using Virial.Media;

namespace TanukiBCL.Roles;

// Handheld interview microphone, shared by the role and its action buttons.
public static class InterviewerVisual
{
    private static Sprite? sprite;
    public static readonly Image Icon = new WrapSpriteLoader(() => GetSprite());

    public static Sprite GetSprite()
    {
        if (sprite != null) return sprite;
        const int size = 256;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        var ink = new Color32(24, 28, 35, 255);
        var light = new Color32(238, 249, 252, 255);
        var blue = new Color32(80, 190, 225, 255);
        for (int py = 0; py < size; py++)
        for (int px = 0; px < size; px++)
        {
            float x = (px + .5f) / 2, y = (py + .5f) / 2;
            bool Box(float l, float b, float r, float t) => x >= l && x <= r && y >= b && y <= t;
            bool Disc(float cx, float cy, float r) => (x-cx)*(x-cx)+(y-cy)*(y-cy) <= r*r;
            Color32 color = new(0, 0, 0, 0);
            // White silhouette keeps the microphone legible on dark UI backgrounds.
            if (Box(49, 10, 79, 83) || Disc(64, 92, 29)) color = light;
            if (Box(53, 14, 75, 82) || Disc(64, 92, 25)) color = ink;
            if (Box(58, 19, 70, 70)) color = new Color32(96, 110, 120, 255);
            if (Disc(64, 93, 20)) color = new Color32(161, 180, 189, 255);
            if (Disc(64, 93, 20) && (Box(43, 87, 85, 90) || Box(43, 96, 85, 99))) color = ink;
            // Blue microphone flag and speech mark distinguish it from a recorder.
            if (Box(39, 49, 89, 77)) color = light;
            if (Box(43, 53, 85, 73)) color = ink;
            if (Box(47, 57, 81, 69)) color = blue;
            if (Box(54, 61, 74, 65) || Box(54, 58, 59, 64)) color = light;
            pixels[py * size + px] = color;
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 200f);
        return sprite;
    }
}

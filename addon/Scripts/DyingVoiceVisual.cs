using Nebula.Utilities;
using UnityEngine;
using Virial.Media;

namespace TanukiBCL.Roles;

// Original tape-recorder artwork, shared by the role icon and the placed message.
public static class DyingVoiceVisual
{
    private static Sprite? sprite;
    public static readonly Image Icon = new WrapSpriteLoader(() => GetSprite());
    public static Sprite GetSprite()
    {
        if (sprite != null) return sprite;
        const int size = 256;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var pixels = new Color32[size * size];
        var ink = new Color32(24, 28, 35, 255);
        var light = new Color32(228, 231, 228, 255);
        for (int py = 0; py < size; py++)
        for (int px = 0; px < size; px++)
        {
            float x = (px + .5f) / 2, y = (py + .5f) / 2;
            Color32 color = new(0, 0, 0, 0);
            bool Box(float left, float bottom, float right, float top) => x >= left && x <= right && y >= bottom && y <= top;
            bool Disc(float cx, float cy, float r) => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r;
            // Handle, white outer border, and charcoal recorder housing.
            if (Box(34, 97, 94, 116)) color = light;
            if (Box(38, 96, 90, 112)) color = ink;
            if (Box(44, 96, 84, 106)) color = new Color32(0, 0, 0, 0);
            if (Box(12, 12, 116, 101)) color = light;
            if (Box(16, 16, 112, 97)) color = ink;
            if (Box(20, 20, 108, 93)) color = new Color32(79, 88, 96, 255);
            if (Box(22, 83, 106, 90)) color = new Color32(153, 165, 170, 255);
            // Cassette window with two visible tape reels.
            if (Box(26, 43, 102, 81)) color = ink;
            if (Box(30, 47, 98, 77)) color = new Color32(203, 195, 166, 255);
            if (Box(43, 52, 85, 71)) color = ink;
            if (Disc(43, 61, 12) || Disc(85, 61, 12)) color = light;
            if (Disc(43, 61, 7) || Disc(85, 61, 7)) color = ink;
            if (Disc(43, 61, 2) || Disc(85, 61, 2)) color = light;
            if (Box(53, 52, 75, 56)) color = new Color32(170, 157, 126, 255);
            // Speaker slots and playback/stop/record buttons.
            if (Box(27, 26, 57, 28) || Box(27, 32, 57, 34) || Box(27, 38, 57, 40)) color = ink;
            if (Box(64, 26, 75, 38) || Box(80, 26, 91, 38)) color = light;
            if (Box(67, 29, 72, 35) || Box(83, 29, 88, 35)) color = ink;
            if (Disc(100, 32, 7)) color = ink;
            if (Disc(100, 32, 5)) color = new Color32(205, 10, 19, 255);
            pixels[py * size + px] = color;
        }
        texture.SetPixels32(pixels); texture.Apply(false, true);
        sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 200f);
        return sprite;
    }
}

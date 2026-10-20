using Nebula.Utilities;
using UnityEngine;
using Virial.Media;

namespace TanukiBCL.Roles;

public static class VoiceJammingerIcon
{
    public static readonly Image Icon = new WrapSpriteLoader(GetSprite);
    private static Sprite? sprite;
    public static Sprite GetSprite()
    {
        if (sprite) return sprite!;
        const int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++) pixels[y * size + x] = Pixel(x, y);
        texture.SetPixels32(pixels); texture.Apply(false, true);
        sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 85f);
        return sprite;
    }
    private static Color32 Pixel(int x, int y)
    {
        var ink = new Color32(25, 25, 36, 255);
        var white = new Color32(245, 249, 255, 255);
        // A lightning strike interrupts the radio waves, rather than a normal radio.
        if (Bolt(x, y)) return new Color32(255, 62, 73, 255);
        if (BoltEdge(x, y) < 3f) return ink;
        float body = RoundedBox(x - 48f, y - 35f, 31f, 27f, 6f);
        if (body <= 0f)
        {
            if (body > -4f) return ink;
            if (Distance(x - 32f, y - 49f) < 5f) return ink;
            if (x >= 45 && x <= 68 && y >= 21 && y <= 49 && y % 9 < 4) return ink;
            return white;
        }
        float antenna = Segment(x, y, 47, 62, 62, 92);
        if (antenna < 4f) return white;
        if (antenna < 7f) return ink;
        float ring = Mathf.Abs(Distance(x - 62f, y - 91f) - 20f);
        float outerRing = Mathf.Abs(Distance(x - 62f, y - 91f) - 32f);
        bool brokenWave = y > 75 && (x < 53 || x > 71) && !(x > 71 && y > 95 && y < 104);
        if (brokenWave && Mathf.Min(ring, outerRing) < 3f) return white;
        if (brokenWave && Mathf.Min(ring, outerRing) < 6f) return ink;
        return new Color32(0, 0, 0, 0);
    }
    private static readonly Vector2[] Lightning = {
        new(102, 120), new(73, 81), new(88, 81), new(64, 37),
        new(111, 91), new(94, 91)
    };
    private static bool Bolt(float x, float y)
    {
        bool inside = false;
        for (int i = 0, j = Lightning.Length - 1; i < Lightning.Length; j = i++)
        {
            var a = Lightning[i]; var b = Lightning[j];
            if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x)
                inside = !inside;
        }
        return inside;
    }
    private static float BoltEdge(float x, float y)
    {
        float distance = 128f;
        for (int i = 0, j = Lightning.Length - 1; i < Lightning.Length; j = i++)
            distance = Mathf.Min(distance, Segment(x, y, Lightning[i].x, Lightning[i].y, Lightning[j].x, Lightning[j].y));
        return distance;
    }
    private static float RoundedBox(float x, float y, float width, float height, float radius)
    {
        float dx = Mathf.Abs(x) - width + radius, dy = Mathf.Abs(y) - height + radius;
        return Distance(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)) + Mathf.Min(Mathf.Max(dx, dy), 0f) - radius;
    }
    private static float Segment(float x, float y, float ax, float ay, float bx, float by)
    {
        float dx = bx - ax, dy = by - ay;
        float amount = Mathf.Clamp01(((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy));
        return Distance(x - ax - amount * dx, y - ay - amount * dy);
    }
    private static float Distance(float x, float y) => Mathf.Sqrt(x * x + y * y);
}

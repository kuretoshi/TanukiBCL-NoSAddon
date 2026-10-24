using UnityEngine;

namespace TanukiBCL.Roles;

// Pixel coordinates also drive the standalone preview, keeping it true to the game asset.
public static class HeroArtwork
{
    private static readonly Color32 Ink = new(23, 27, 40, 255);
    private static readonly Color32 Gold = new(255, 217, 67, 255);
    private static readonly Color32 White = new(244, 250, 255, 255);
    private static readonly Color32 Empty = new(0, 0, 0, 0);
    public static Color32 Pixel(float x, float y, HeroKind kind, int part)
    {
        if (part == 3) return Aura(x, y, kind);
        if (part == 4) return Emblem(x, y, false, true);
        var suit = kind == HeroKind.Super ? new Color32(35, 128, 236, 255) : new Color32(55, 39, 82, 255);
        var cape = kind == HeroKind.Super ? new Color32(222, 37, 66, 255) : new Color32(91, 29, 139, 255);
        bool isCape = y >= 15 && y <= 95 && x >= 12 + (y - 15) * .23f && x <= 84 - (y - 15) * .26f;
        if (part == 1) return isCape ? cape : Empty;
        var color = part == 0 && isCape ? cape : Empty;
        if (part == 0) color = Body(x, y, suit, color);
        if (Box(x, y, 39, 77, 110, 94)) color = Ink;
        if (Box(x, y, 43, 81, 106, 90)) color = kind == HeroKind.Super ? suit : new Color32(160, 41, 96, 255);
        if (Box(x, y, 55, 83, 76, 88) || Box(x, y, 83, 83, 102, 88)) color = White;
        if (Box(x, y, 33, 32, 90, 39)) color = kind == HeroKind.Super ? Gold : new Color32(191, 81, 221, 255);
        var emblem = Emblem(x, y, kind == HeroKind.Evil, false);
        return emblem.a > 0 ? emblem : color;
    }
    private static Color32 Body(float x, float y, Color32 suit, Color32 previous)
    {
        bool torso = Box(x, y, 29, 25, 94, 78) || Disc(x, y, 61, 78, 33);
        if (torso) previous = Ink;
        bool inside = Box(x, y, 34, 29, 89, 78) || Disc(x, y, 61, 78, 28);
        if (inside) previous = suit;
        if (Box(x, y, 32, 11, 53, 33) || Box(x, y, 72, 11, 93, 33)) previous = Ink;
        if (Box(x, y, 37, 16, 48, 31) || Box(x, y, 77, 16, 88, 31)) previous = suit;
        return previous;
    }
    private static Color32 Emblem(float x, float y, bool evil, bool large)
    {
        float cx = 64, cy = large ? 64 : 55;
        float scale = large ? 2.4f : 1f;
        float dx = (x - cx) / scale, dy = (y - cy) / scale;
        if (Mathf.Abs(dx) + Mathf.Abs(dy) > 18) return Empty;
        if (Mathf.Abs(dx) + Mathf.Abs(dy) > 14) return Ink;
        if (evil && Mathf.Abs(dx - dy * .5f) < 3f) return White;
        if (!evil && Mathf.Abs(dx + dy * .5f) < 3f) return White;
        return evil ? new Color32(214, 65, 129, 255) : Gold;
    }
    private static Color32 Aura(float x, float y, HeroKind kind)
    {
        float dx = (x - 64) / 60f, dy = (y - 64) / 60f;
        float radius = Mathf.Sqrt(dx * dx + dy * dy);
        if (radius < .38f || radius > 1f) return Empty;
        float angle = Mathf.Atan2(dy, dx);
        float rays = Mathf.Pow(Mathf.Max(0, Mathf.Sin(angle * 9 + radius * 7)), 3);
        float ring = Mathf.Clamp01(1 - Mathf.Abs(radius - .70f) * 8);
        float alpha = Mathf.Clamp01((1 - radius) * (ring * 1.8f + rays * 2.5f));
        byte opacity = (byte)(alpha * 210);
        if (kind == HeroKind.Super) return new Color32(255, (byte)(225 + ring * 25), (byte)(115 + ring * 130), opacity);
        return ring > .45f ? new Color32(153, 62, 210, opacity) : new Color32(30, 13, 48, opacity);
    }
    private static bool Box(float x, float y, float left, float bottom, float right, float top) =>
        x >= left && x <= right && y >= bottom && y <= top;
    private static bool Disc(float x, float y, float cx, float cy, float radius) =>
        (x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius;
}

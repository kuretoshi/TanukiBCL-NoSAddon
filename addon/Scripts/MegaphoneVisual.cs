using UnityEngine;
using Nebula.Utilities;
using Virial.Media;

namespace TanukiBCL.Roles;

// Original layered vector art; the icon and in-game model share this design.
public sealed class MegaphoneVisual
{
    public static readonly Image Icon = new WrapSpriteLoader(() => SpriteArt(true));
    public static readonly Image PortableIcon = new WrapSpriteLoader(() => SpriteArt(true, true));
    public static Sprite PortableSprite => SpriteArt(false, true);
    private static Sprite? iconSprite, gunSprite, portableIconSprite, portableGunSprite;
    private readonly GameObject root = new("TanukiMegaphone");
    private readonly Material material;
    private readonly LineRenderer beam, sheath, core, sight, muzzle, halo;
    private readonly LineRenderer[] cells = new LineRenderer[6];
    private readonly LineRenderer[] filaments = new LineRenderer[2];
    private readonly Vector3[] ringPoints = new Vector3[33];
    private readonly Vector3[] beamPoints = new Vector3[2];
    private readonly Vector3[] wavePoints = new Vector3[17];
    private const int FlowRingCount = 9;
    private readonly LineRenderer[] ringFront = new LineRenderer[FlowRingCount];
    private readonly LineRenderer[] ringBack = new LineRenderer[FlowRingCount];
    private readonly Vector3[] flowPoints = new Vector3[17];
    private bool wasFiring;
    private float firingStartedAt;

    private sealed class Plate
    {
        public readonly Color Color;
        public readonly Vector2[] Points;
        public Plate(Color color, params float[] xy)
        {
            Color = color; Points = new Vector2[xy.Length / 2];
            for (int i = 0; i < Points.Length; i++) Points[i] = new(xy[i * 2], xy[i * 2 + 1]);
        }
    }
    private static readonly Plate[] plates = new Plate[]
    {
        new(new Color(.04f,.06f,.10f), -.18f,-.12f, .17f,-.16f, .07f,-.62f, -.08f,-.67f, -.19f,-.59f),
        new(new Color(.24f,.29f,.37f), -.11f,-.18f, .09f,-.21f, .02f,-.56f, -.07f,-.60f, -.12f,-.56f),
        new(new Color(.04f,.06f,.10f), -.25f,-.21f, .05f,-.21f, .57f,-.43f, .79f,-.43f, .79f,.43f, .57f,.43f, .05f,.21f, -.25f,.21f),
        new(new Color(.27f,.34f,.44f), -.20f,-.16f, .06f,-.16f, .58f,-.37f, .71f,-.37f, .71f,.37f, .58f,.37f, .06f,.16f, -.20f,.16f),
        new(new Color(.48f,.57f,.68f), -.17f,.09f, .08f,.09f, .58f,.30f, .68f,.30f, .68f,.37f, .58f,.37f, .06f,.16f, -.17f,.16f),
        new(new Color(.12f,.18f,.26f), .06f,-.10f, .54f,-.28f, .67f,-.28f, .67f,.18f, .54f,.18f, .06f,.06f),
        new(new Color(.02f,.06f,.12f), -.19f,-.11f, .15f,-.11f, .15f,.11f, -.19f,.11f),
        new(new Color(.04f,.66f,.80f), -.17f,-.055f, .13f,-.055f, .13f,.055f, -.17f,.055f),
        new(new Color(.70f,1f,1f), -.14f,-.018f, .11f,-.018f, .11f,.018f, -.14f,.018f),
        new(new Color(1f,.51f,.09f), .57f,-.40f, .66f,-.40f, .66f,.40f, .57f,.40f),
        new(new Color(.04f,.08f,.14f), .67f,-.42f, .79f,-.42f, .79f,.42f, .67f,.42f),
        new(new Color(.16f,.78f,.93f), .70f,-.36f, .75f,-.36f, .75f,.36f, .70f,.36f),
        new(new Color(.67f,.98f,1f), .735f,-.31f, .76f,-.31f, .76f,.31f, .735f,.31f),
        new(new Color(1f,.62f,.17f), .20f,.16f, .45f,.26f, .45f,.30f, .20f,.20f),
        new(new Color(1f,.62f,.17f), .20f,-.20f, .45f,-.30f, .45f,-.26f, .20f,-.16f),
    };
    private static bool Inside(Vector2 p, Vector2[] polygon)
    {
        bool hit = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            if ((polygon[i].y > p.y) != (polygon[j].y > p.y) && p.x < (polygon[j].x - polygon[i].x) * (p.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x) hit = !hit;
        return hit;
    }
    private static Sprite SpriteArt(bool icon, bool portable = false)
    {
        var cached = portable ? (icon ? portableIconSprite : portableGunSprite) : (icon ? iconSprite : gunSprite);
        if (cached != null) return cached;
        const int size = 256;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            pixels[y * size + x] = PixelColor(x, y, size, icon, portable);
        }
        if (icon)
        {
            // Black outer shadow + white rim + dark inner edge, readable on any map.
            var outlined = (Color[])pixels.Clone();
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                outlined[y * size + x] = OutlinePixel(pixels, size, x, y);
            }
            pixels = outlined;
        }
        texture.SetPixels(pixels);
        texture.Apply();
        cached = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f,.5f), icon ? 175f : size / 1.5f);
        if (portable) { if (icon) portableIconSprite = cached; else portableGunSprite = cached; }
        else { if (icon) iconSprite = cached; else gunSprite = cached; }
        return cached;
    }
    private static Color PixelColor(int x, int y, int size, bool icon, bool portable)
    {
        var p = new Vector2((x + .5f) / size * 1.5f - .3f, (y + .5f) / size * 1.5f - .75f);
        if (icon)
        {
            // Hero angle for the HUD icon; the world-space weapon stays level.
            // Leave room for the outline so the rear corner cannot be clipped.
            var offset = (p - new Vector2(.40f, -.10f)) / .86f;
            float a = -12f * Mathf.Deg2Rad;
            p = new Vector2(offset.x * Mathf.Cos(a) - offset.y * Mathf.Sin(a),
                offset.x * Mathf.Sin(a) + offset.y * Mathf.Cos(a)) + new Vector2(.40f, -.10f);
        }
        Color color = Color.clear;
        foreach (var plate in plates) if (Inside(p, plate.Points)) color = plate.Color;
        if (Mathf.Abs(p.x + .21f) < .013f && Mathf.Abs(p.y) < .12f) color = new Color(.73f,.82f,.9f);
        if (p.x > -.105f && p.x < .035f && p.y < -.28f && p.y > -.54f && Mathf.Repeat(-p.y, .08f) < .017f) color = new Color(.07f,.10f,.15f);
        if (p.x > .27f && p.x < .49f && Mathf.Abs(p.y) < .11f && Mathf.Repeat(p.x, .07f) < .026f) color = new Color(.04f,.10f,.17f);
        if (portable)
        {
            // Same armored shell, mint trim and a three-cell battery badge.
            if (color.a > 0 && color.r > .9f && color.g > .4f && color.g < .7f) color = new Color(.24f, .92f, .64f);
            if (p.x > .24f && p.x < .49f && Mathf.Abs(p.y) < .07f)
                color = Mathf.Repeat(p.x - .24f, .085f) < .057f ? new Color(.4f, 1f, .72f) : new Color(.04f, .12f, .14f);
        }
        if (icon && p.x > .85f)
        {
            float r = (p - new Vector2(.72f, 0)).magnitude;
            if (Mathf.Abs(r - .25f) < .014f || Mathf.Abs(r - .40f) < .014f) color = new Color(.20f,.92f,1f);
        }
        return color;
    }
    private static Color OutlinePixel(Color[] pixels, int size, int x, int y)
    {
                if (pixels[y * size + x].a > 0) return pixels[y * size + x];
                int nearest = 122;
                for (int dy = -11; dy <= 11; dy++) for (int dx = -11; dx <= 11; dx++)
                {
                    int distance = dx * dx + dy * dy;
                    int sx = x + dx, sy = y + dy;
                    if (distance < nearest && sx >= 0 && sy >= 0 && sx < size && sy < size && pixels[sy * size + sx].a > 0)
                        nearest = distance;
                }
                if (nearest <= 121) return nearest <= 9 || nearest > 64
                    ? new Color(.02f,.03f,.05f,.95f) : new Color(.94f,.98f,1f);
        return pixels[y * size + x];
    }
    public MegaphoneVisual()
    {
        material = new Material(Shader.Find("Sprites/Default"));
        var gun = new GameObject("ArmoredMegaphone");
        gun.transform.SetParent(root.transform, false);
        gun.transform.localPosition = new Vector3(.45f, 0, 0);
        var sprite = gun.AddComponent<SpriteRenderer>();
        sprite.sprite = SpriteArt(false); sprite.sortingOrder = 15;
        beam = Line("BlueAura", new Color(.15f,.35f,1f,.4f), .05f, 10);
        sheath = Line("CyanSheath", new Color(.05f,.86f,1f,.7f), .05f, 11);
        core = Line("WhiteHotCore", Color.white, .02f, 12);
        sight = Line("ChargeSight", new Color(.1f,.85f,1f,.3f), .008f, 10);
        muzzle = Line("MuzzleCorona", new Color(.4f,1f,1f), .035f, 16);
        halo = Line("ChargeHalo", new Color(.1f,.7f,1f,.4f), .025f, 14);
        muzzle.positionCount = halo.positionCount = 33;
        // Reuse a fixed pool: no particle objects are created during firing.
        for (int i = 0; i < FlowRingCount; i++)
        {
            ringBack[i] = Line("FlowRingRear", Color.cyan, .02f, 9);
            ringFront[i] = Line("FlowRingFront", Color.cyan, .02f, 14);
            ringBack[i].positionCount = ringFront[i].positionCount = 17;
            ringBack[i].enabled = ringFront[i].enabled = false;
        }
        for (int i = 0; i < 2; i++) { filaments[i] = Line("EnergyFilament", Color.white, .01f, 13); filaments[i].positionCount = 17; }
        for (int i = 0; i < 6; i++)
        {
            cells[i] = Line("ChargeCell", Color.cyan, .03f, 16);
            cells[i].SetPositions(new Vector3[] { new(.20f + i * .048f, -.055f), new(.20f + i * .048f, .055f) });
        }
    }
    private LineRenderer Line(string name, Color color, float width, int order)
    {
        var child = new GameObject(name); child.transform.SetParent(root.transform, false);
        var line = child.AddComponent<LineRenderer>(); line.sharedMaterial = material;
        line.useWorldSpace = false; line.positionCount = 2;
        line.startColor = line.endColor = color; line.startWidth = line.endWidth = width; line.sortingOrder = order;
        return line;
    }
    private void Ring(LineRenderer line, float radius, float squash)
    {
        for (int i = 0; i < ringPoints.Length; i++)
        {
            float theta = i / 32f * Mathf.PI * 2;
            ringPoints[i] = new Vector3(.75f + Mathf.Cos(theta) * radius * squash, Mathf.Sin(theta) * radius, 0);
        }
        line.SetPositions(ringPoints);
    }
    public void Draw(float x, float y, float angle, float width, bool firing, float energy = 0)
    {
        energy = Mathf.Clamp01(energy);
        float t = Time.time, pulse = .5f + .5f * Mathf.Sin(t * (6f + energy * 10f));
        if (firing && !wasFiring) firingStartedAt = t;
        wasFiring = firing;
        FlowRings(firing, t - firingStartedAt, width);
        root.transform.position = new Vector3(x, y, -.1f);
        root.transform.eulerAngles = new Vector3(0, 0, angle * Mathf.Rad2Deg);
        beam.startWidth = beam.endWidth = width; sheath.startWidth = sheath.endWidth = width * .72f;
        core.startWidth = core.endWidth = width * .30f;
        beam.startColor = beam.endColor = new Color(.15f,.35f,1f, firing ? .38f + pulse * .12f : .045f + energy * .055f);
        sheath.startColor = sheath.endColor = new Color(.05f,.86f,1f, firing ? .68f : .04f + energy * .10f);
        beamPoints[0] = new Vector3(.75f, 0); beamPoints[1] = new Vector3(firing ? 150f : 8f, 0);
        beam.SetPositions(beamPoints); sheath.SetPositions(beamPoints); core.SetPositions(beamPoints); sight.SetPositions(beamPoints);
        core.enabled = firing; sight.enabled = !firing;
        Ring(muzzle, firing ? Mathf.Max(.32f, width * .48f) : .31f + pulse * .025f * energy, .16f);
        Ring(halo, .37f + energy * .15f + pulse * .025f, .20f);
        halo.startColor = halo.endColor = new Color(.1f,.65f,1f, firing ? .5f : .08f + energy * .4f);
        muzzle.startColor = muzzle.endColor = new Color(.55f,1f,1f, .4f + energy * .6f);
        for (int i = 0; i < cells.Length; i++) cells[i].startColor = cells[i].endColor = energy * 6 >= i + 1 || firing ? new Color(.4f,1f,1f,.85f + pulse * .15f) : new Color(.05f,.20f,.28f);
        for (int side = 0; side < filaments.Length; side++)
        {
            filaments[side].enabled = firing;
            if (!firing) continue;
            for (int i = 0; i < wavePoints.Length; i++) wavePoints[i] = new Vector3(.75f + i * .75f, (side == 0 ? -1 : 1) * width * (.37f + .06f * Mathf.Sin(i * 1.8f - t * 22f)));
            filaments[side].startWidth = filaments[side].endWidth = Mathf.Min(.035f, width * .045f);
            filaments[side].startColor = filaments[side].endColor = new Color(.55f,.95f,1f,.45f);
            filaments[side].SetPositions(wavePoints);
        }
    }
    private void FlowRings(bool firing, float elapsed, float width)
    {
        const float interval = .22f;
        const float lifetime = FlowRingCount * interval;
        for (int i = 0; i < FlowRingCount; i++)
        {
            float age = elapsed - i * interval;
            bool visible = firing && age >= 0;
            ringFront[i].enabled = ringBack[i].enabled = visible;
            if (!visible) continue;
            age = Mathf.Repeat(age, lifetime);
            float progress = age / lifetime;
            float opacity = Mathf.Clamp01(age / .08f) * (1f - progress) * (1f - progress);
            float center = .75f + age * 13f;
            float radius = width * .5f + Mathf.Min(.06f, width * .2f);
            float stroke = Mathf.Clamp(width * .035f, .003f, .045f);
            ringFront[i].startWidth = ringFront[i].endWidth = stroke;
            ringBack[i].startWidth = ringBack[i].endWidth = stroke;
            ringFront[i].startColor = ringFront[i].endColor = new Color(.55f,1f,1f, opacity * .9f);
            ringBack[i].startColor = ringBack[i].endColor = new Color(.1f,.7f,1f, opacity * .45f);
            FlowArc(ringBack[i], center, radius, Mathf.PI * .5f);
            FlowArc(ringFront[i], center, radius, -Mathf.PI * .5f);
        }
    }
    private void FlowArc(LineRenderer line, float center, float radius, float start)
    {
        for (int i = 0; i < flowPoints.Length; i++)
        {
            float theta = start + i / 16f * Mathf.PI;
            // A slight shear gives the traveling ring a tilted, three-dimensional look.
            flowPoints[i] = new Vector3(center + radius * (Mathf.Cos(theta) * .30f + Mathf.Sin(theta) * .12f), Mathf.Sin(theta) * radius);
        }
        line.SetPositions(flowPoints);
    }
    public void Dispose()
    {
        if (root) Object.Destroy(root);
        if (material) Object.Destroy(material);
    }
}

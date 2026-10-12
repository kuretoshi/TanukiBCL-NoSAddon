using System.IO;
using HarmonyLib;
using Nebula.Utilities;
using TMPro;
using UnityEngine;
using Virial.Attributes;
using Virial.Runtime;

namespace TanukiBCL.Roles;

[NebulaPreprocess(PreprocessPhase.PostRoles)]
public static class DiscordContactButton
{
    public const string ContactUrl = "https://discord.gg/j7grHxTh4H";
    private const string ObjectName = "TanukiDiscordContact";
    private static Sprite? icon;
    private static Sprite? circle;
    private static Sprite? hintBackground;

    private static Sprite Circle()
    {
        if (circle) return circle!;
        const int size = 96;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float radius = new Vector2(x - 47.5f, y - 47.5f).magnitude;
                pixels[y * size + x] = radius > 47f ? new Color32(0, 0, 0, 0)
                    : radius > 42f ? new Color32(47, 55, 112, 255)
                    : new Color32(88, 101, 242, 255);
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        circle = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size / .46f);
        return circle;
    }

    public static void Preprocess(NebulaPreprocessor preprocessor)
    {
        new Harmony("tanuki.bcl.roles.discordContact").Patch(
            AccessTools.Method(typeof(MainMenuManager), nameof(MainMenuManager.Start)),
            postfix: new HarmonyMethod(typeof(DiscordContactButton), nameof(AddButton)) { priority = Priority.Last });
    }

    private static Sprite LoadIcon()
    {
        if (icon) return icon!;
        using var stream = Virial.NebulaAPI.GetAddon("tanuki.bcl.roles")?.GetResource("DiscordContact.png")?.AsStream()
            ?? throw new System.IO.FileNotFoundException("Missing Discord contact icon in the addon.");
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        GraphicsHelper.LoadImage(texture, bytes.ToArray(), true);
        icon = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), Mathf.Max(texture.width, texture.height) / 0.34f);
        return icon;
    }

    public static void AddButton(MainMenuManager __instance)
    {
        if (__instance.transform.Find(ObjectName)) return;
        var sprite = LoadIcon();
        var gameObject = new GameObject(ObjectName);
        gameObject.layer = __instance.gameObject.layer;
        gameObject.transform.SetParent(__instance.transform, false);
        var renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = Circle();
        var iconObject = new GameObject("TanukiIcon");
        iconObject.layer = gameObject.layer;
        iconObject.transform.SetParent(gameObject.transform, false);
        iconObject.transform.localPosition = new Vector3(0f, 0f, -.1f);
        var iconRenderer = iconObject.AddComponent<SpriteRenderer>();
        iconRenderer.sprite = sprite;
        var aspect = gameObject.AddComponent<AspectPosition>();
        aspect.Alignment = AspectPosition.EdgeAlignments.RightBottom;
        aspect.DistanceFromEdge = new Vector3(1.38f, 0.24f, -6f);
        aspect.AdjustPosition();
        var button = gameObject.SetUpButton(true, renderer, new Virial.Color(1f, 1f, 1f), new Virial.Color(1f, 1f, 1f));
        gameObject.AddComponent<CircleCollider2D>().radius = 0.25f;
        button.OnClick.AddListener((System.Action)(() => Application.OpenURL(ContactUrl)));

        var helpObject = new GameObject("ContactHint");
        helpObject.layer = gameObject.layer;
        helpObject.transform.SetParent(gameObject.transform, false);
        helpObject.transform.localPosition = new Vector3(-1.0f, 0.5f, -1f);
        var help = helpObject.AddComponent<TextMeshPro>();
        help.font = TMP_Settings.defaultFontAsset;
        help.text = "タヌキのアドオンの公式Discordサーバーはこちら。";
        help.fontSize = 1.4f;
        help.alignment = TextAlignmentOptions.Right;
        help.enableWordWrapping = false;
        help.rectTransform.sizeDelta = new Vector2(4.4f, 0.45f);
        if (!hintBackground)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, new Color(.12f, .12f, .12f, .94f));
            texture.Apply(false, true);
            hintBackground = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1f);
        }
        var backdrop = new GameObject("Background");
        backdrop.layer = gameObject.layer;
        backdrop.transform.SetParent(helpObject.transform, false);
        backdrop.transform.localPosition = new Vector3(0f, 0f, .1f);
        backdrop.transform.localScale = new Vector3(4.55f, .35f, 1f);
        backdrop.AddComponent<SpriteRenderer>().sprite = hintBackground;
        helpObject.SetActive(false);
        button.OnMouseOver.AddListener((System.Action)(() => { helpObject.SetActive(true); iconObject.transform.localScale = new Vector3(1.08f, 1.08f, 1f); }));
        button.OnMouseOut.AddListener((System.Action)(() => { helpObject.SetActive(false); iconObject.transform.localScale = Vector3.one; }));
    }
}

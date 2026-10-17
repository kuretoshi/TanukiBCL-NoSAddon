using System.IO;
using System;
using System.Collections.Generic;
using HarmonyLib;
using Nebula;
using Nebula.Modules;
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
        var button = gameObject.SetUpButton(true, new SpriteRenderer[] { renderer, iconRenderer });
        var collider = gameObject.AddComponent<CircleCollider2D>();
        collider.radius = 0.25f;
        button.Colliders = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Collider2D>(new Collider2D[] { collider });
        button.ClickMask = null;
        button.OnClick.AddListener((System.Action)(() => Application.OpenURL(ContactUrl)));
        button.OnMouseOver.AddListener((Action)(() => NebulaManager.Instance.SetHelpWidget(button,
            "タヌキのアドオンの公式Discordサーバーはこちら。")));
        button.OnMouseOut.AddListener((Action)(() => NebulaManager.Instance.HideHelpWidgetIf(button)));
        AddTitle(__instance);
        AlignButtons(__instance, gameObject, renderer);
        // Other add-ons may add their social buttons in later Start postfixes.
        NebulaManager.Instance.StartDelayAction(.5f, () => AlignButtons(__instance, gameObject, renderer));
        NebulaManager.Instance.StartDelayAction(2f, () => AlignButtons(__instance, gameObject, renderer));
    }

    private static void AlignButtons(MainMenuManager menu, GameObject own, SpriteRenderer ownRenderer)
    {
        if (!menu || !own) return;
        var reference = menu.transform.Find("DiscordButton")?.GetComponent<SpriteRenderer>();
        if (reference == null) return;
        var row = new List<SpriteRenderer> { reference };
        foreach (var candidate in menu.GetComponentsInChildren<PassiveButton>())
        {
            if (candidate.gameObject == own || candidate.gameObject == reference.gameObject) continue;
            var image = candidate.GetComponent<SpriteRenderer>();
            if (image == null || !image.enabled || Math.Abs(image.transform.position.y - reference.transform.position.y) > .1f) continue;
            float left = reference.transform.position.x - image.transform.position.x;
            if (left > 0 && left < 1.6f) row.Add(image);
        }
        row.Sort((a, b) => b.transform.position.x.CompareTo(a.transform.position.x));
        float diameter = reference.bounds.size.x;
        float spacing = row.Count > 1 ? row[0].transform.position.x - row[1].transform.position.x : diameter * 1.3f;
        spacing = Mathf.Max(diameter * 1.15f, spacing);
        float scale = diameter / ownRenderer.sprite.bounds.size.x;
        own.transform.localScale = Vector3.one * scale;
        var aspect = own.GetComponent<AspectPosition>();
        var referenceAspect = reference.GetComponent<AspectPosition>();
        if (referenceAspect != null)
        {
            aspect.parentCam = referenceAspect.parentCam;
            aspect.DistanceFromEdge = new Vector3(referenceAspect.DistanceFromEdge.x +
                reference.transform.position.x - row[row.Count - 1].transform.position.x + spacing,
                referenceAspect.DistanceFromEdge.y, referenceAspect.DistanceFromEdge.z);
            aspect.updateAlways = true;
            aspect.AdjustPosition();
        }
    }

    private static void AddTitle(MainMenuManager menu)
    {
        if (menu.transform.Find("TanukiAddonTitle")) return;
        var obj = new GameObject("TanukiAddonTitle");
        obj.layer = menu.gameObject.layer;
        obj.transform.SetParent(menu.transform, false);
        var name = new GameObject("Name");
        name.layer = obj.layer;
        name.transform.SetParent(obj.transform, false);
        name.transform.localPosition = new Vector3(0, .17f, 0);
        var title = name.AddComponent<TextMeshPro>();
        title.font = TMP_Settings.defaultFontAsset;
        title.text = "<b>タヌキ丼</b>";
        title.fontSize = 2.4f;
        title.alignment = TextAlignmentOptions.Center;
        title.enableWordWrapping = false;
        title.rectTransform.sizeDelta = new Vector2(2.1f, .8f);
        obj.SetAsUIAspectContent(AspectPosition.EdgeAlignments.RightTop, new Vector3(1f, 2.6f, -6f));
        obj.GetComponent<AspectPosition>().updateAlways = true;
        var versionObject = new GameObject("Version");
        versionObject.layer = obj.layer;
        versionObject.transform.SetParent(obj.transform, false);
        versionObject.transform.localPosition = new Vector3(0, -.17f, 0);
        var version = versionObject.AddComponent<TextMeshPro>();
        version.font = title.font;
        version.fontStyle = FontStyles.Bold;
        version.fontSize = 2f;
        version.text = "v" + (NebulaAddon.GetAddon("tanuki.bcl.roles")?.Version ?? "");
        version.alignment = TextAlignmentOptions.Center;
        version.enableWordWrapping = false;
        version.rectTransform.sizeDelta = new Vector2(2.1f, .4f);
        MatchVersionStyle(menu, version);
        NebulaManager.Instance.StartDelayAction(.5f, () => MatchVersionStyle(menu, version));
        NebulaManager.Instance.StartDelayAction(2f, () => MatchVersionStyle(menu, version));
    }

    private static void MatchVersionStyle(MainMenuManager menu, TextMeshPro version)
    {
        if (!menu || !version) return;
        TextMeshPro? nearest = null;
        float distance = float.MaxValue;
        foreach (var text in menu.GetComponentsInChildren<TextMeshPro>(true))
        {
            if (text == version || text.transform.IsChildOf(version.transform.parent)) continue;
            string value = System.Text.RegularExpressions.Regex.Replace(text.text ?? "", "<[^>]+>", "").Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(value, @"^v?\d+\.\d+\.\d+[a-z]?$")) continue;
            var delta = text.transform.position - version.transform.position;
            if (delta.y <= 0 || Math.Abs(delta.x) > 1.5f || delta.sqrMagnitude >= distance) continue;
            nearest = text; distance = delta.sqrMagnitude;
        }
        if (nearest == null) return;
        version.font = nearest.font;
        version.fontSharedMaterial = nearest.fontSharedMaterial;
        version.fontStyle = nearest.fontStyle;
        version.fontWeight = nearest.fontWeight;
        version.fontSize = nearest.fontSize * Math.Abs(nearest.transform.lossyScale.y) /
            Math.Max(.001f, Math.Abs(version.transform.lossyScale.y));
        version.characterSpacing = nearest.characterSpacing;
        version.color = nearest.color;
    }
}

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
        icon = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), Mathf.Max(texture.width, texture.height) / 0.46f);
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
        renderer.sprite = sprite;
        var aspect = gameObject.AddComponent<AspectPosition>();
        aspect.Alignment = AspectPosition.EdgeAlignments.RightBottom;
        aspect.DistanceFromEdge = new Vector3(1.38f, 0.24f, -6f);
        aspect.AdjustPosition();
        var button = gameObject.SetUpButton(true, renderer);
        gameObject.AddComponent<CircleCollider2D>().radius = 0.25f;
        button.OnClick.AddListener((System.Action)(() => Application.OpenURL(ContactUrl)));

        var helpObject = new GameObject("ContactHint");
        helpObject.layer = gameObject.layer;
        helpObject.transform.SetParent(gameObject.transform, false);
        helpObject.transform.localPosition = new Vector3(-1.0f, 0.5f, -1f);
        var help = helpObject.AddComponent<TextMeshPro>();
        help.font = TMP_Settings.defaultFontAsset;
        help.text = "タヌキ丼：Discordで問い合わせ";
        help.fontSize = 1.4f;
        help.alignment = TextAlignmentOptions.Right;
        help.enableWordWrapping = false;
        help.rectTransform.sizeDelta = new Vector2(3.0f, 0.45f);
        helpObject.SetActive(false);
        button.OnMouseOver.AddListener((System.Action)(() => helpObject.SetActive(true)));
        button.OnMouseOut.AddListener((System.Action)(() => helpObject.SetActive(false)));
    }
}

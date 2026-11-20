using System;
using System.Collections.Generic;
using System.Globalization;
using Nebula.Utilities;
using TMPro;
using UnityEngine;
using Virial;
using Virial.Media;
using Virial.Text;
using Color = UnityEngine.Color;

namespace TanukiBCL.Roles;

// One solid collider per word; the rendered stone letters use equal-width cells.
public sealed class VoiceStoneVisual : IDisposable
{
    public static readonly Image Icon = VoiceRoleIcons.VoiceStone;
    private readonly GameObject root;
    private readonly BoxCollider2D collider;
    private readonly double expires;
    private readonly List<Material> materials = new();
    private Collider2D? localCollider;
    private bool? collisionIgnored;
    private bool exitGrace;
    private bool disposed;
    public bool Expired => disposed;

    public VoiceStoneVisual(string word, float x, float y, float characterWidth, float seconds)
    {
        var template = PlayerControl.LocalPlayer?.cosmetics?.nameText;
        if (template == null || template.font == null) throw new InvalidOperationException("Stone text template unavailable.");
        root = new GameObject("TanukiVoiceStone");
        root.layer = LayerMask.NameToLayer("Ship");
        root.transform.position = new Vector3(x, y, y / 1000f - .01f);
        expires = Time.unscaledTimeAsDouble + seconds;
        collider = root.AddComponent<BoxCollider2D>();
        collider.enabled = false;
        collider.isTrigger = false;
        collider.size = new Vector2(VoiceStoneRules.Width(word, characterWidth), .5f);
        collider.offset = new Vector2(0, .15f);
        try
        {
            BuildLetters(template, word, characterWidth);
            collider.enabled = true;
            BindLocalCollider();
            VoiceStoneImpactSound.Play(root, new Vector2(x, y));
        }
        catch { Dispose(); throw; }
    }
    private void BuildLetters(TextMeshPro template, string word, float width)
    {
        var elements = StringInfo.GetTextElementEnumerator(word);
        int index = 0, count = VoiceStoneRules.Count(word);
        while (elements.MoveNext())
        {
            string letter = elements.GetTextElement();
            float x = (index++ - (count - 1) / 2f) * width;
            Letter(template, letter, width, new Vector3(x + .045f, .205f, .02f), new Color(.22f, .25f, .29f));
            Letter(template, letter, width, new Vector3(x, .25f, -.01f), new Color(.69f, .73f, .77f));
        }
    }
    private void Letter(TextMeshPro template, string value, float width, Vector3 position, Color color)
    {
        var text = UnityEngine.Object.Instantiate(template, root.transform);
        for (int i = text.transform.childCount - 1; i >= 0; i--)
            UnityEngine.Object.Destroy(text.transform.GetChild(i).gameObject);
        text.name = "StoneLetter";
        text.transform.localPosition = position;
        text.transform.localRotation = Quaternion.identity;
        text.transform.localScale = Vector3.one;
        var material = new Material(template.fontSharedMaterial);
        materials.Add(material);
        text.fontSharedMaterial = material;
        text.text = value;
        text.richText = false;
        text.color = color;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.rectTransform.pivot = new Vector2(.5f, .5f);
        text.rectTransform.sizeDelta = new Vector2(1f, 1f);
        text.fontSize = 5f;
        text.enableAutoSizing = false;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.outlineColor = new Color32(24, 29, 35, 255);
        text.outlineWidth = .18f;
        text.enabled = true;
        text.gameObject.SetActive(true);
        text.ForceMeshUpdate();
        var bounds = text.textBounds;
        text.transform.localScale = new Vector3(width * .9f / Math.Max(.01f, bounds.size.x), .65f / Math.Max(.01f, bounds.size.y), 1f);
    }
    private void BindLocalCollider()
    {
        var local = PlayerControl.LocalPlayer;
        if (local == null || local.Collider == null) return;
        localCollider = local.Collider;
        collisionIgnored = null;
        // Everyone overlapping a newly created word can leave it once. On each
        // client only the locally controlled body needs this collision exception.
        var bounds = collider.bounds;
        bounds.Expand(.2f);
        exitGrace = bounds.Intersects(localCollider.bounds);
        UpdateCollision();
    }
    private void UpdateCollision()
    {
        var local = PlayerControl.LocalPlayer;
        if (local == null || localCollider == null) return;
        var bounds = collider.bounds;
        bounds.Expand(.15f);
        if (exitGrace && !bounds.Intersects(localCollider.bounds)) exitGrace = false;
        bool ignore = exitGrace || local.Data?.IsDead == true;
        if (collisionIgnored == ignore) return;
        Physics2D.IgnoreCollision(collider, localCollider, ignore);
        collisionIgnored = ignore;
    }
    public void Update()
    {
        if (disposed) return;
        if (Time.unscaledTimeAsDouble >= expires) { Dispose(); return; }
        if (localCollider == null) BindLocalCollider(); else UpdateCollision();
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        collider.enabled = false;
        UnityEngine.Object.Destroy(root);
        foreach (var material in materials) UnityEngine.Object.Destroy(material);
        materials.Clear();
    }
}

using System;
using Nebula;
using Nebula.Utilities;
using TMPro;
using UnityEngine;
using Virial;

namespace TanukiBCL.Roles;

internal sealed class VoiceJammingMeetingButton : IDisposable
{
    private readonly GameObject root;
    private readonly SpriteRenderer image;
    private readonly TextMeshPro label;
    private bool usable;
    public VoiceJammingMeetingButton(Transform parent, Action click)
    {
        root = new GameObject("TanukiVoiceJammingButton");
        root.layer = LayerMask.NameToLayer("UI");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = new Vector3(-3.35f, 2.4f, -30f);
        root.transform.localScale = Vector3.one * .45f;
        image = root.AddComponent<SpriteRenderer>();
        image.sprite = VoiceJammingerIcon.GetSprite();
        root.AddComponent<BoxCollider2D>().size = new Vector2(1.8f, 1.8f);
        var button = root.SetUpButton(true);
        button.OnClick.AddListener(() => { if (usable) click(); });
        button.OnMouseOver.AddListener(() => { if (usable) image.color = new UnityEngine.Color(1f, .6f, .8f); });
        button.OnMouseOut.AddListener(() => image.color = usable ? UnityEngine.Color.white : UnityEngine.Color.gray);
        label = UnityEngine.Object.Instantiate(VanillaAsset.StandardTextPrefab, root.transform);
        label.gameObject.SetActive(true);
        label.transform.localPosition = new Vector3(0, -.9f, -1);
        label.rectTransform.sizeDelta = new Vector2(3.4f, .6f);
        label.fontSize = 1.4f;
        label.alignment = TextAlignmentOptions.Center;
    }
    public void Update(bool visible, double seconds, int remaining)
    {
        if (root == null) return;
        root.SetActive(visible);
        usable = visible && remaining != 0;
        image.color = usable ? UnityEngine.Color.white : UnityEngine.Color.gray;
        bool active = seconds > 0;
        string key = remaining == 0 ? "empty" : active ? "reroll" : "pitch";
        label.text = NebulaAPI.Language.Translate("role.tanukiVoiceJamminger." + key);
        if (remaining > 0) label.text += " (" + remaining + ")";
        if (active) label.text += "\n" + Math.Ceiling(seconds) + "s";
    }
    public void Dispose() { if (root != null) UnityEngine.Object.Destroy(root); }
}

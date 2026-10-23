using System;
using Nebula;
using Nebula.Utilities;
using TMPro;
using UnityEngine;
using Virial;

namespace TanukiBCL.Roles;

internal sealed class InterviewerMeetingButton : IDisposable
{
    private readonly GameObject root;
    private readonly SpriteRenderer image;
    private readonly TextMeshPro label;
    public InterviewerMeetingButton(Transform parent, Action click)
    {
        root = new GameObject("TanukiInterviewerPlayback");
        root.layer = LayerMask.NameToLayer("UI");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = new Vector3(-3.35f, 2.4f, -30);
        image = root.AddComponent<SpriteRenderer>();
        image.sprite = InterviewerVisual.GetSprite();
        image.transform.localScale = Vector3.one * .45f;
        var collider = root.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(1.8f, 1.6f);
        collider.offset = new Vector2(0, -.25f);
        var button = root.SetUpButton(true);
        button.OnClick.AddListener(click);
        button.OnMouseOver.AddListener(() => image.color = new UnityEngine.Color(.6f, 1, 1));
        button.OnMouseOut.AddListener(() => image.color = UnityEngine.Color.white);
        label = UnityEngine.Object.Instantiate(VanillaAsset.StandardTextPrefab, root.transform);
        label.gameObject.SetActive(true);
        label.transform.localScale = Vector3.one;
        label.transform.localPosition = new Vector3(0, -.85f, -1);
        label.rectTransform.sizeDelta = new Vector2(3, .5f);
        label.fontSize = 1.4f;
        label.alignment = TextAlignmentOptions.Center;
    }
    public void Update(bool visible, bool recorded, bool playing, int remaining)
    {
        if (root == null) return;
        root.SetActive(visible);
        image.color = recorded && !playing && remaining != 0 ? UnityEngine.Color.white : UnityEngine.Color.gray;
        string key = playing ? "playing" : !recorded ? "empty" : remaining == 0 ? "exhausted" : "play";
        label.text = NebulaAPI.Language.Translate("role.tanukiInterviewer." + key);
        if (recorded && !playing && remaining > 0) label.text += " (" + remaining + ")";
    }
    public void Dispose() { if (root != null) UnityEngine.Object.Destroy(root); }
}

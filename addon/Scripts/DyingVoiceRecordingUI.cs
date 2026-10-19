using System;
using System.Collections.Generic;
using Nebula;
using TMPro;
using UnityEngine;
using Virial;

namespace TanukiBCL.Roles;

// Copy only the task's artwork: task scripts, buttons, sounds and completion
// callbacks must never run while a dead player leaves a voice recording.
internal sealed class DyingVoiceRecordingUI : IDisposable
{
    private readonly GameObject root;
    private readonly Dictionary<int, Transform> visuals = new();
    private Transform? leftWheel, rightWheel;
    private Transform? leftTape, rightTape;
    private TextMeshPro? remainingText, totalText;
    private Vector3 leftRotation, rightRotation;
    private Vector3 leftTapeScale, rightTapeScale;

    private DyingVoiceRecordingUI(RewindTapeMinigame prefab, Transform parent)
    {
        root = new GameObject("TanukiDyingVoiceRecording");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = new Vector3(0, 0, -60);
        CopyVisual(prefab.transform, root.transform);
        root.transform.GetChild(0).gameObject.SetActive(true);
        leftWheel = Find(prefab.LeftWheel.transform);
        rightWheel = Find(prefab.RightWheel.transform);
        leftRotation = leftWheel?.localEulerAngles ?? Vector3.zero;
        rightRotation = rightWheel?.localEulerAngles ?? Vector3.zero;
        leftTape = Find(prefab.LeftTape.transform);
        rightTape = Find(prefab.RightTape.transform);
        leftTapeScale = leftTape?.localScale ?? Vector3.one;
        rightTapeScale = rightTape?.localScale ?? Vector3.one;
        remainingText = Find(prefab.CurrentText.transform)?.GetComponent<TextMeshPro>();
        totalText = Find(prefab.TargetText.transform)?.GetComponent<TextMeshPro>();
        HideControls(prefab);
        AddRecordingLabel();
    }

    internal static DyingVoiceRecordingUI? Create(Transform? parent = null)
    {
        parent ??= Camera.main?.transform;
        var ship = VanillaAsset.MapAsset[4];
        if (parent == null || ship == null) return null;
        foreach (var task in ship.LongTasks)
        {
            var prefab = task.MinigamePrefab.TryCast<RewindTapeMinigame>();
            if (prefab != null) return new DyingVoiceRecordingUI(prefab, parent);
        }
        return null;
    }

    private Transform? Find(Transform original) => visuals.TryGetValue(original.GetInstanceID(), out var copy) ? copy : null;

    private void HideControls(RewindTapeMinigame prefab)
    {
        Hide(prefab.RewindButton.transform);
        Hide(prefab.FastFwdButton.transform);
        Hide(prefab.PlayButton.transform);
        Hide(prefab.PauseButton.transform);
        Hide(prefab.RewindGlyph.transform);
        Hide(prefab.FastFwdGlyph.transform);
        Hide(prefab.PlayGlyph.transform);
        Hide(prefab.PauseGlyph.transform);
        foreach (var button in prefab.GetComponentsInChildren<PassiveButton>(true))
        {
            if (button.name == "CloseButton") Hide(button.transform);
        }
    }

    private void Hide(Transform original)
    {
        var copy = Find(original);
        if (copy != null) copy.gameObject.SetActive(false);
    }

    private void AddRecordingLabel()
    {
        var label = UnityEngine.Object.Instantiate(VanillaAsset.StandardTextPrefab, root.transform);
        label.gameObject.SetActive(true);
        label.transform.localPosition = new Vector3(0, -2.1f, -2);
        label.transform.localScale = Vector3.one;
        label.fontSize = 2.5f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new UnityEngine.Color(1f, .2f, .2f);
        label.rectTransform.sizeDelta = new Vector2(8, .6f);
        label.text = NebulaAPI.Language.Translate("role.tanukiDyingVoice.recordingLabel");
    }

    private void CopyVisual(Transform original, Transform parent)
    {
        var copy = new GameObject(original.name);
        copy.layer = LayerMask.NameToLayer("UI");
        copy.transform.SetParent(parent, false);
        copy.transform.localPosition = original.localPosition;
        copy.transform.localRotation = original.localRotation;
        copy.transform.localScale = original.localScale;
        CopySprite(original, copy);
        CopyText(original, copy);
        visuals[original.GetInstanceID()] = copy.transform;
        for (int i = 0; i < original.childCount; i++) CopyVisual(original.GetChild(i), copy.transform);
        copy.SetActive(original.gameObject.activeSelf);
    }

    private static void CopySprite(Transform original, GameObject copy)
    {
        var source = original.GetComponent<SpriteRenderer>();
        if (source == null) return;
        var target = copy.AddComponent<SpriteRenderer>();
        target.sprite = source.sprite;
        target.color = source.color;
        target.sharedMaterial = source.sharedMaterial;
        target.sortingOrder = source.sortingOrder;
        target.sortingLayerID = source.sortingLayerID;
        target.flipX = source.flipX;
        target.flipY = source.flipY;
        target.enabled = source.enabled;
    }

    private static void CopyText(Transform original, GameObject copy)
    {
        var source = original.GetComponent<TextMeshPro>();
        if (source == null) return;
        var target = copy.AddComponent<TextMeshPro>();
        target.font = source.font;
        target.fontSharedMaterial = source.fontSharedMaterial;
        target.fontSize = source.fontSize;
        target.fontStyle = source.fontStyle;
        target.alignment = source.alignment;
        target.color = source.color;
        target.text = source.text;
        target.rectTransform.sizeDelta = source.rectTransform.sizeDelta;
        target.rectTransform.pivot = source.rectTransform.pivot;
    }

    internal void Update(float elapsed, float seconds, bool visible)
    {
        if (root == null) return;
        root.SetActive(visible);
        if (leftWheel != null) leftWheel.localEulerAngles = leftRotation + new Vector3(0, 0, -elapsed * 160f);
        if (rightWheel != null) rightWheel.localEulerAngles = rightRotation + new Vector3(0, 0, -elapsed * 160f);
        UpdateTape(elapsed, seconds);
        if (remainingText != null) remainingText.text = TimeText(Math.Max(0, seconds - elapsed));
        if (totalText != null) totalText.text = TimeText(seconds);
    }

    private void UpdateTape(float elapsed, float seconds)
    {
        float progress = Mathf.Clamp01(elapsed / Mathf.Max(seconds, .001f));
        // Tape length is proportional to the wound area, rather than its radius.
        // Keep the hubs and reel wheels at their original size.
        const float coreArea = .65f * .65f;
        float left = Mathf.Sqrt(Mathf.Lerp(1f, coreArea, progress));
        float right = Mathf.Sqrt(Mathf.Lerp(coreArea, 1f, progress));
        if (leftTape != null) leftTape.localScale = TapeScale(leftTapeScale, left);
        if (rightTape != null) rightTape.localScale = TapeScale(rightTapeScale, right);
    }

    private static Vector3 TapeScale(Vector3 original, float amount) =>
        new Vector3(original.x * amount, original.y * amount, original.z);

    private static string TimeText(float seconds)
    {
        int value = (int)Math.Ceiling(seconds);
        return (value / 60).ToString("00") + ":" + (value % 60).ToString("00");
    }

    public void Dispose() { if (root != null) UnityEngine.Object.Destroy(root); }
}

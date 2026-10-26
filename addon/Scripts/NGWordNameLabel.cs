using System;
using System.Linq;
using TMPro;
using UnityEngine;

namespace TanukiBCL.Roles;

// Keep the native name and role text untouched. A child follows the name's
// visibility, so world labels do not reveal players through walls or vents.
public sealed class NGWordNameLabel : IDisposable
{
    private TextMeshPro? label, source;
    private bool inMeeting;

    public void Update(byte playerId, string word, bool visible)
    {
        if (!visible) { Dispose(); return; }
        bool meeting = HeroSpeechHost.IsPresent(MeetingHud.Instance);
        if (label != null && meeting != inMeeting) Dispose();
        if (label == null) Create(playerId, meeting);
        if (label == null || source == null) return;
        label.text = "NG：「" + word + "」";
        label.color = new Color(1f, .65f, .3f, source.color.a);
        label.gameObject.SetActive(source.enabled && source.gameObject.activeInHierarchy && source.color.a > .01f && source.text.Length > 0);
    }

    private void Create(byte playerId, bool meeting)
    {
        source = FindName(playerId, meeting);
        if (source == null || source.font == null) return;
        // A fresh TextMeshPro can fail in LoadFontAsset before its renderer is
        // initialized in IL2CPP. Clone the working native name component instead.
        var copy = UnityEngine.Object.Instantiate(source, source.transform);
        copy.gameObject.SetActive(false);
        try { Configure(copy, meeting); label = copy; }
        catch { UnityEngine.Object.Destroy(copy.gameObject); throw; }
        inMeeting = meeting;
    }

    private void Configure(TextMeshPro copy, bool meeting)
    {
        copy.name = "TanukiNGWordName";
        // Cloning a name also clones its role line; only keep the new word.
        for (int i = copy.transform.childCount - 1; i >= 0; i--)
            UnityEngine.Object.Destroy(copy.transform.GetChild(i).gameObject);
        copy.text = "";
        copy.transform.localScale = Vector3.one;
        copy.transform.localRotation = Quaternion.identity;
        // Meeting labels stay inside the voting plate, above the player name.
        // World labels also leave room for NoS's role line above the name.
        copy.transform.localPosition = new Vector3(0, meeting ? .205f : .40f, -.01f);
        copy.enableAutoSizing = false;
        copy.fontSize = meeting ? 1.05f : 1.45f;
        copy.fontSizeMax = copy.fontSize;
        copy.fontSizeMin = meeting ? .65f : 1.0f;
        copy.enableAutoSizing = true;
        copy.alignment = TextAlignmentOptions.Center;
        copy.enableWordWrapping = false;
        copy.overflowMode = TextOverflowModes.Ellipsis;
        copy.richText = false;
        copy.rectTransform.pivot = new Vector2(.5f, .5f);
        copy.rectTransform.sizeDelta = new Vector2(meeting ? source!.rectTransform.sizeDelta.x : 4f, .20f);
    }

    private static TextMeshPro? FindName(byte playerId, bool meeting)
    {
        if (meeting)
            return MeetingHud.Instance?.playerStates.FirstOrDefault(p => p.PlayerId.Value == playerId)?.NameText;
        var player = PlayerControl.AllPlayerControls.ToArray().FirstOrDefault(p => p.PlayerId == playerId);
        return player?.cosmetics?.nameText;
    }

    public void Dispose()
    {
        if (label != null) UnityEngine.Object.Destroy(label.gameObject);
        label = null;
        source = null;
    }
}

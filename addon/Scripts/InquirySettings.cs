using System;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using Nebula;
using Nebula.Behavior;
using Nebula.Modules;
using Nebula.Modules.GUIWidget;
using UnityEngine;
using Virial;
using Virial.Attributes;
using Virial.Media;
using Virial.Runtime;
using Virial.Text;

namespace TanukiBCL.Roles;

[NebulaPreprocess(PreprocessPhase.PostRoles)]
public static class InquirySettings
{
    private static bool sending;
    private static string T(string key) => NebulaAPI.Language.Translate("tanuki.inquiry." + key);
    public static void Preprocess(NebulaPreprocessor preprocessor)
    {
        new Harmony("tanuki.bcl.roles.inquiry").Patch(AccessTools.Method(typeof(OptionsMenuBehaviour), nameof(OptionsMenuBehaviour.Start)),
            postfix: new HarmonyMethod(typeof(InquirySettings), nameof(AddButton)) { priority = Priority.Last });
    }
    private static void AddButton(OptionsMenuBehaviour __instance)
    {
        var tab = __instance.transform.Find("NebulaTab");
        if (tab == null || tab.Find("TanukiInquiryButton") != null) return;
        foreach (var content in tab.GetComponentsInChildren<MetaScreen>(true))
        {
            content.transform.localScale = new Vector3(1f, .88f, 1f);
            content.transform.localPosition += new Vector3(0f, .20f, 0f);
        }
        var screen = MetaScreen.GenerateScreen(new Vector2(5.6f, .7f), tab, new Vector3(0f, -2.12f, -20f), false, false, false);
        screen.gameObject.name = "TanukiInquiryButton";
        var buttonAttribute = new TextAttribute(NebulaAPI.GUI.GetAttribute(AttributeAsset.OverlayBold))
            { Size = new(2.5f, .27f), Alignment = Virial.Text.TextAlignment.Center, FontSize = new(1.8f, 1.4f, 1.8f) };
        screen.SetWidget(NebulaAPI.GUI.RawButton(GUIAlignment.Center, buttonAttribute, T("open"), _ => Open(__instance.transform),
            onMouseOver: _ => NebulaManager.Instance.SetHelpWidget(null, NebulaAPI.GUI.RawText(GUIAlignment.Center, NebulaAPI.GUI.GetAttribute(AttributeAsset.OverlayContent), T("hint"))),
            onMouseOut: _ => NebulaManager.Instance.SetHelpWidget(null, (GUIWidget?)null), margin: .16f), new Virial.Compat.Vector2(.5f, .5f), out _);
        BindButtons(screen.gameObject);
    }
    private static void BindButtons(GameObject root)
    {
        foreach (var button in root.GetComponentsInChildren<PassiveButton>(true))
        {
            button.gameObject.layer = LayerMask.NameToLayer("UI");
            button.Colliders = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Collider2D>(button.GetComponents<Collider2D>().ToArray());
            button.ClickMask = null;
        }
    }
    private static GUIWidget Label(string value, float height = .28f) => NebulaAPI.GUI.RawText(GUIAlignment.Center,
        new TextAttribute(NebulaAPI.GUI.GetAttribute(AttributeAsset.OverlayContent))
            { Size = new(6.6f, height), Wrapping = true, Alignment = Virial.Text.TextAlignment.Center }, value);
    private static TextAttribute FormButtonAttribute(float width = 3f) => new TextAttribute(NebulaAPI.GUI.GetAttribute(AttributeAsset.OverlayBold))
        { Size = new(width, .3f), Wrapping = false, Alignment = Virial.Text.TextAlignment.Center, FontSize = new(1.5f, 1.3f, 1.5f) };
    private static void Open(Transform parent)
    {
        if (sending) return;
        var logs = InquiryReport.Collect(System.IO.Path.GetDirectoryName(Application.dataPath) ?? Environment.CurrentDirectory);
        var window = MetaScreen.GenerateWindow(new Vector2(7f, 4.9f), null, Vector3.zero, true, true);
        string subject = "", body = "";
        bool consent = false;
        var consentRow = NebulaAPI.GUI.HorizontalHolder(GUIAlignment.Center,
            new NoSGUICheckbox(GUIAlignment.Center, false) { OnValueChanged = value => consent = value },
            NebulaAPI.GUI.RawText(GUIAlignment.Center,
                new TextAttribute(NebulaAPI.GUI.GetAttribute(AttributeAsset.OverlayContent))
                    { Size = new(4.2f, .38f), Alignment = Virial.Text.TextAlignment.Center }, T("consent")));
        var title = new GUITextField(GUIAlignment.Center, new(6.4f, .35f)) { IsSharpField = false, WithMaskMaterial = false, MaxLines = 1, HintText = T("subject"), LostFocusAction = value => subject = value };
        var detail = new GUITextField(GUIAlignment.Center, new(6.4f, 1.45f)) { IsSharpField = false, WithMaskMaterial = false, MaxLines = 8, FontSize = 1.5f, HintText = T("body"), LostFocusAction = value => body = value };
        window.SetWidget(NebulaAPI.GUI.VerticalHolder(GUIAlignment.Center,
            Label(T("open")), title, detail, Label(T("disclosure"), .65f),
            NebulaAPI.GUI.RawButton(GUIAlignment.Center, FormButtonAttribute(), T("preview"), _ => {
                TextField.LoseAllFocus();
                string combined = "";
                foreach (var log in logs) combined += "=== " + log.name + " ===\n" + log.text + "\n";
                Preview(window.transform, combined, 0);
            }, margin: .18f),
            consentRow,
            NebulaAPI.GUI.RawButton(GUIAlignment.Center, FormButtonAttribute(), T("send"), _ => {
                TextField.LoseAllFocus();
                if (sending) return;
                if (!consent || subject.Trim().Length < 1 || subject.Trim().Length > 100 || body.Trim().Length < 1 || body.Trim().Length > 1800) { Notice(window.transform, T("invalid")); return; }
                sending = true;
                var task = InquiryReport.Send(subject.Trim(), body.Trim(), logs);
                Watch(task, window);
            }, margin: .18f),
            NebulaAPI.GUI.RealtimeText(GUIAlignment.Center, new TextAttribute(NebulaAPI.GUI.GetAttribute(AttributeAsset.OverlayContent)) { Size = new(6.6f, .3f), Alignment = Virial.Text.TextAlignment.Center }, () => sending ? T("sending") : "", 55)), new Virial.Compat.Vector2(.5f, 1f), out _);
        BindButtons(window.gameObject);
        foreach (var button in window.GetComponentsInChildren<PassiveButton>(true))
        {
            var parentText = button.transform.parent?.GetComponent<TMPro.TextMeshPro>();
            if (parentText == null || parentText.text != "□") continue;
            var hitbox = button.GetComponent<BoxCollider2D>();
            hitbox.size = new Vector2(4.6f, .4f);
            hitbox.offset = new Vector2(2.05f, 0f);
        }
    }
    private static void Preview(Transform parent, string content, int page)
    {
        const int pageSize = 900;
        int count = Math.Max(1, (content.Length + pageSize - 1) / pageSize);
        int start = page * pageSize;
        var preview = MetaScreen.GenerateWindow(new Vector2(7f, 4.5f), null, Vector3.zero, true, true);
        preview.SetWidget(NebulaAPI.GUI.VerticalHolder(GUIAlignment.Center,
            Label((page + 1) + " / " + count),
            new GUITextField(GUIAlignment.Center, new(6.5f, 3.2f)) { DefaultText = content.Substring(start, Math.Min(pageSize, content.Length - start)), MaxLines = 100, FontSize = 1.2f, WithMaskMaterial = false, TextPredicate = _ => false },
            NebulaAPI.GUI.HorizontalHolder(GUIAlignment.Center,
                NebulaAPI.GUI.RawButton(GUIAlignment.Center, FormButtonAttribute(.65f), "◀", _ => { if (page > 0) { preview.CloseScreen(); Preview(parent, content, page - 1); } }, margin: .18f),
                NebulaAPI.GUI.RawButton(GUIAlignment.Center, FormButtonAttribute(.65f), "▶", _ => { if (page + 1 < count) { preview.CloseScreen(); Preview(parent, content, page + 1); } }, margin: .18f))), out _);
        BindButtons(preview.gameObject);
    }
    private static void Watch(Task<string> task, MetaScreen window)
    {
        NebulaManager.Instance.StartDelayAction(.2f, () => {
            if (!task.IsCompleted) { Watch(task, window); return; }
            sending = false;
            if (!window) return;
            if (task.IsFaulted || task.IsCanceled) { Notice(window.transform, T("failed")); return; }
            string url = task.Result;
            window.CloseScreen();
            ShowCompletion(url);
        });
    }
    public static void ShowCompletion(string url)
    {
            var complete = MetaScreen.GenerateWindow(new Vector2(4.8f, 1.6f), null, Vector3.zero, true, true);
            var message = NebulaAPI.GUI.RawText(GUIAlignment.Center,
                new TextAttribute(NebulaAPI.GUI.GetAttribute(AttributeAsset.OverlayContent))
                    { Size = new(4.4f, .8f), Wrapping = true, Alignment = Virial.Text.TextAlignment.Center }, T("sent"));
            complete.SetWidget(NebulaAPI.GUI.VerticalHolder(GUIAlignment.Center, message,
                NebulaAPI.GUI.RawButton(GUIAlignment.Center, FormButtonAttribute(), T("viewIssue"), _ => Application.OpenURL(url), margin: .18f)),
                new Virial.Compat.Vector2(.5f, .5f), out _);
            BindButtons(complete.gameObject);
    }
    private static void Notice(Transform parent, string message)
    {
        var notice = MetaScreen.GenerateWindow(new Vector2(6.8f, 1.7f), null, Vector3.zero, true, true);
        notice.SetWidget(Label(message, 1.2f), out _);
    }
}

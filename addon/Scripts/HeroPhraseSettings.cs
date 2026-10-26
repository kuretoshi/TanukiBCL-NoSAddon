using System;
using Nebula.Behavior;
using Nebula.Modules;
using Nebula.Modules.GUIWidget;
using Nebula.Utilities;
using UnityEngine;
using Virial;
using Virial.Media;
using Virial.Text;

namespace TanukiBCL.Roles;

public sealed class HeroPhraseSettings
{
    private static readonly DataSaver Preferences = new("TanukiHeroPreferences");
    private static readonly HeroPhraseSettings Super = new("tanukiSuperHero"), Evil = new("tanukiEvilHero");
    private readonly StringDataEntry word, reading;
    private readonly string role;
    private static string T(string key) => NebulaAPI.Language.Translate("tanuki.heroPhrase." + key);
    private HeroPhraseSettings(string role)
    {
        this.role = role;
        word = new StringDataEntry(role + ".word", Preferences, "変身");
        reading = new StringDataEntry(role + ".reading", Preferences, "");
    }
    public static string[] WordsFor(HeroKind kind) => (kind == HeroKind.Super ? Super : Evil).Words;
    private string[] Words => HeroTransformPhrase.TryCreate(word.Value, reading.Value, out var values) ? values : new[] { "変身", "へんしん" };
    private static TextAttribute Attribute(float width, float height = .3f) => new(NebulaAPI.GUI.GetAttribute(AttributeAsset.OptionsTitleHalf))
        // These dialogs are outside the options list. Its masked font also
        // makes GUIButton use the list's stencil, clipping labels and frames.
        { Font = NebulaAPI.GUI.GetFont(FontAsset.Gothic), Size = new(width, height), Wrapping = false,
            Alignment = Virial.Text.TextAlignment.Center, FontSize = new(1.5f, 1.2f, 1.5f) };
    private string RoleName => NebulaAPI.Language.Translate("role." + role + ".name");
    public static void OpenMenu()
    {
        // Anchor to the UI camera, not the world or a registered modal. NoS
        // assigns each modal a deep local Z, which must not be nested twice.
        var uiParent = UnityHelper.FindCamera(LayerMask.NameToLayer("UI"))?.transform;
        if (uiParent == null) return;
        var menu = MetaScreen.GenerateWindow(new Vector2(6.2f, 2.4f), uiParent, Vector3.zero, true, true);
        menu.SetWidget(NebulaAPI.GUI.VerticalHolder(GUIAlignment.Center,
            NebulaAPI.GUI.RawText(GUIAlignment.Center, Attribute(5.6f), T("personal")),
            Super.Choice(menu, uiParent), Evil.Choice(menu, uiParent)), new Virial.Compat.Vector2(.5f, .5f), out _);
    }
    private GUIWidget Choice(MetaScreen menu, Transform uiParent) => NebulaAPI.GUI.RawButton(GUIAlignment.Center, Attribute(5.5f),
        RoleName + ": " + Words[0], _ => { menu.CloseScreen(); Open(uiParent); }, margin: .16f);
    private void Open(Transform uiParent)
    {
        string draftWord = word.Value, draftReading = reading.Value, error = "";
        var window = MetaScreen.GenerateWindow(new Vector2(6.2f, 4.2f), uiParent, Vector3.zero, true, true);
        var wordField = Field(draftWord, value => draftWord = value);
        var readingField = Field(draftReading, value => draftReading = value);
        window.SetWidget(NebulaAPI.GUI.VerticalHolder(GUIAlignment.Center,
            NebulaAPI.GUI.RawText(GUIAlignment.Center, Attribute(5.6f), RoleName),
            NebulaAPI.GUI.RawText(GUIAlignment.Center, Attribute(5.6f), T("word")), wordField,
            NebulaAPI.GUI.RawText(GUIAlignment.Center, Attribute(5.6f), T("reading")), readingField,
            NebulaAPI.GUI.RawText(GUIAlignment.Center, new TextAttribute(Attribute(5.6f, .8f)) { Wrapping = true }, T("hint")),
            NebulaAPI.GUI.HorizontalHolder(GUIAlignment.Center,
                NebulaAPI.GUI.RawButton(GUIAlignment.Center, Attribute(2f), T("save"), _ => {
                    TextField.LoseAllFocus();
                    if (!HeroTransformPhrase.TryCreate(draftWord, draftReading, out var values)) { error = T("invalid"); return; }
                    word.SetValueWithoutSave(values[0]); reading.SetValueWithoutSave(values.Length > 1 ? values[1] : "");
                    Preferences.TrySave(); window.CloseScreen();
                }),
                NebulaAPI.GUI.RawButton(GUIAlignment.Center, Attribute(2f), T("cancel"), _ => window.CloseScreen())),
            NebulaAPI.GUI.RealtimeText(GUIAlignment.Center, Attribute(5.6f, .35f), () => error, 20)),
            new Virial.Compat.Vector2(.5f, .5f), out _);
    }
    private static GUITextField Field(string initial, Action<string> changed) => new(GUIAlignment.Center, new(5.3f, .4f))
        { DefaultText = initial, MaxLines = 1, FontSize = 1.7f, IsSharpField = false, WithMaskMaterial = false, LostFocusAction = changed };
}

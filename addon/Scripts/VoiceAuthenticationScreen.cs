using System;
using System.IO;
using Nebula.Modules;
using Nebula.Utilities;
using TMPro;
using UnityEngine;

namespace TanukiBCL.Roles;

// The faceplate is decorative artwork. Words, LEDs and the meter are live;
// the meter uses real RMS input and never invents microphone activity.
public sealed class VoiceAuthenticationScreen : IDisposable
{
    private MetaScreen? window;
    private static Sprite? panelSprite, whiteSprite, lampSprite;
    private readonly VoiceAuthenticationProgress progress;
    private readonly Func<bool> ready;
    private readonly TextMeshPro word, counter, status;
    private readonly TextMeshPro[] labels = new TextMeshPro[5];
    private readonly SpriteRenderer[] lamps = new SpriteRenderer[5], bars = new SpriteRenderer[16];
    private readonly float[] heights = { 12,18,27,38,48,39,57,45,33,48,29,23,18,12,8,8 };
    private long consumed, lastVoice;
    private int epoch, completed;
    private float nextDraw, acceptedUntil, level;
    private static readonly Color Mint = new(.44f, .87f, .71f), Amber = new(1f, .86f, .47f);
    public bool IsOpen => window != null && window.gameObject.activeInHierarchy;
    private static Vector3 Position(float x, float y, float z = -.1f) => new((x - 700f) / 200f, (450f - y) / 200f, z);
    public VoiceAuthenticationScreen(VoiceAuthenticationProgress progress, Func<bool> ready, Action close)
    {
        this.progress = progress; this.ready = ready;
        completed = progress.Completed; epoch = AddonLinkVoice.Epoch;
        var parent = UnityHelper.FindCamera(LayerMask.NameToLayer("UI"))?.transform;
        window = MetaScreen.GenerateBlankWindow(new(7f, 4.5f), parent, Vector3.zero, new(-3.6f, 2.25f), true);
        window.gameObject.name = "TanukiVoiceAuthenticationScreen";
        var face = Child("Faceplate", 700, 450, .05f).AddComponent<SpriteRenderer>();
        face.sprite = Panel();
        Label("Title", 665, 119, 900, 72, 43, VoiceAuthenticationTask.Text("title"));
        Label("ProgressLabel", 1195, 246, 190, 45, 23, VoiceAuthenticationTask.Text("progress"));
        Label("Prompt", 663, 281, 680, 58, 25, VoiceAuthenticationTask.Text("prompt"), new(.6f,.77f,.75f));
        word = Label("CurrentWord", 663, 381, 650, 125, 78, "", Amber);
        counter = Label("Counter", 663, 475, 650, 45, 25, "", new(.72f,.82f,.78f));
        status = Label("Status", 663, 538, 690, 58, 24, "", Mint);
        Label("InputLabel", 670, 626, 600, 34, 21, VoiceAuthenticationTask.Text("micInput"), new(.22f,.36f,.35f));
        Label("Saved", 663, 812, 690, 45, 23, VoiceAuthenticationTask.Text("saved"), new(.22f,.35f,.33f));
        Label("CloseLabel", 1180, 812, 240, 45, 29, VoiceAuthenticationTask.Text("closeButton"));
        for (int i = 0; i < labels.Length; i++)
        {
            int y = 320 + i * 81;
            labels[i] = Label("Word" + i, 1220, y, 133, 48, 23, "", new(.13f,.25f,.22f));
            lamps[i] = Rect("Lamp" + i, 1135, y, 24, 24);
            lamps[i].sprite = Lamp();
        }
        for (int i = 0; i < bars.Length; i++)
            bars[i] = Rect("Meter" + i, 367.5f + i * 39, 710 - heights[i] * .5f, 25, heights[i]);
        var closeCollider = Child("CloseButton", 1180, 812, -.15f).AddComponent<BoxCollider2D>();
        closeCollider.size = new(1.3f, .34f); closeCollider.isTrigger = true;
        closeCollider.gameObject.SetUpButton().OnClick.AddListener(close);
        Update();
    }
    private GameObject Child(string name, float x, float y, float z = -.1f)
    {
        var obj = new GameObject(name); obj.layer = window!.gameObject.layer;
        obj.transform.SetParent(window.transform, false); obj.transform.localPosition = Position(x,y,z);
        return obj;
    }
    private TextMeshPro Label(string name, float x, float y, float width, float height, float pixels, string value, Color? color = null)
    {
        var text = Child(name,x,y).AddComponent<TextMeshPro>();
        var source = PlayerControl.LocalPlayer.cosmetics.nameText;
        text.font = source.font; text.fontSharedMaterial = source.fontSharedMaterial;
        text.rectTransform.sizeDelta = new(width/200f,height/200f);
        text.rectTransform.pivot = new(.5f,.5f);
        text.fontSize = pixels/20f; text.fontSizeMax = pixels/20f; text.fontSizeMin = pixels/30f;
        text.enableAutoSizing = true; text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Ellipsis; text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Bold; text.color = color ?? new Color(.93f,.96f,.87f);
        text.richText = false; text.text = value;
        return text;
    }
    private SpriteRenderer Rect(string name, float x, float y, float width, float height)
    {
        var obj = Child(name,x,y,-.08f);
        var renderer = obj.AddComponent<SpriteRenderer>(); renderer.sprite = White();
        obj.transform.localScale = new(width/200f,height/200f,1f);
        return renderer;
    }
    public void Update()
    {
        if (!IsOpen || Time.unscaledTime < nextDraw) return;
        nextDraw = Time.unscaledTime + 1f/20f;
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (epoch != AddonLinkVoice.Epoch) { epoch = AddonLinkVoice.Epoch; consumed = 0; level = 0; }
        bool connected = ready();
        if (AddonLinkVoice.TryRead(PlayerControl.LocalPlayer.PlayerId, ref consumed, out double rms, out bool active, now))
        {
            lastVoice = now;
            level = active ? Mathf.Clamp01((float)Math.Sqrt(rms) * 2.5f) : 0f;
        }
        if (!connected || now - lastVoice > 300) level *= .65f;
        if (completed != progress.Completed) { completed = progress.Completed; acceptedUntil = Time.unscaledTime + .45f; }
        word.text = progress.Finished ? VoiceAuthenticationTask.Text("accepted") : progress.Words[progress.Completed].Display;
        counter.text = Math.Min(completed + 1, progress.Entries.Count).ToString("00") + " / " + progress.Entries.Count.ToString("00");
        status.text = !connected ? VoiceAuthenticationTask.Text("notReady") : Time.unscaledTime < acceptedUntil ?
            VoiceAuthenticationTask.Text("accepted") : level > .05f ? VoiceAuthenticationTask.Text("listening") : VoiceAuthenticationTask.Text("waiting");
        status.color = connected ? Mint : Amber;
        for (int i = 0; i < labels.Length; i++)
        {
            bool used = i < progress.Entries.Count;
            labels[i].text = used ? progress.Words[i].Display : "—";
            labels[i].color = used ? new(.13f,.25f,.22f) : new(.47f,.55f,.49f);
            lamps[i].color = i < completed ? Mint : used && i == completed ? Amber : new(.25f,.35f,.32f);
        }
        int lit = (int)Math.Ceiling(level * bars.Length);
        for (int i = 0; i < bars.Length; i++)
            bars[i].color = i < lit ? (i >= 13 ? Amber : Mint) : new(.26f,.38f,.36f);
    }
    private static Sprite Panel()
    {
        if (panelSprite != null) return panelSprite;
        using var stream = Virial.NebulaAPI.GetAddon("tanuki.bcl.roles")?.GetResource("VoiceAuthenticationPanel.png")?.AsStream()
            ?? throw new FileNotFoundException("Missing voice authentication panel");
        using var data = new MemoryStream(); stream.CopyTo(data);
        var texture = new Texture2D(2,2,TextureFormat.RGBA32,false) { filterMode = FilterMode.Bilinear };
        GraphicsHelper.LoadImage(texture,data.ToArray(),false);
        return panelSprite = Sprite.Create(texture,new(0,0,texture.width,texture.height),new(.5f,.5f),200f);
    }
    private static Sprite White()
    {
        if (whiteSprite != null) return whiteSprite;
        var texture = new Texture2D(1,1,TextureFormat.RGBA32,false);
        texture.SetPixel(0,0,Color.white); texture.Apply(false,true);
        return whiteSprite = Sprite.Create(texture,new(0,0,1,1),new(.5f,.5f),1f);
    }
    private static Sprite Lamp()
    {
        if (lampSprite != null) return lampSprite;
        var texture = new Texture2D(32,32,TextureFormat.RGBA32,false) { filterMode = FilterMode.Bilinear };
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float dx = x - 15.5f, dy = y - 15.5f;
                texture.SetPixel(x,y,new Color(1f,1f,1f,Mathf.Clamp01(15.5f - Mathf.Sqrt(dx*dx+dy*dy))));
            }
        texture.Apply(false,true);
        return lampSprite = Sprite.Create(texture,new(0,0,32,32),new(.5f,.5f),32f);
    }
    public void Dispose() { if (window != null) window.CloseScreen(); window = null; }
}

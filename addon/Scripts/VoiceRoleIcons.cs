using System.IO;
using UnityEngine;
using Nebula.Utilities;
using Virial.Media;

namespace TanukiBCL.Roles;

// Distinct PNG artwork, shared by role lists and each modifier's HUD indicator.
public static class VoiceRoleIcons
{
    public static readonly Image Bibiri = new WrapSpriteLoader(() => Get(0));
    public static readonly Image Talkative = new WrapSpriteLoader(() => Get(1));
    public static readonly Image LimitSpeech = new WrapSpriteLoader(() => Get(2));
    public static readonly Image NGWord = new WrapSpriteLoader(() => Get(3));
    public static readonly Image SuperHero = new WrapSpriteLoader(() => Get(4));
    public static readonly Image EvilHero = new WrapSpriteLoader(() => Get(5));
    public static readonly Image Interviewer = new WrapSpriteLoader(() => Get(6));
    public static readonly Image VoiceJamminger = new WrapSpriteLoader(() => Get(7));
    public static readonly Image VoiceStone = new WrapSpriteLoader(() => Get(8));
    public static readonly Image DyingVoiceMessage = new WrapSpriteLoader(() => Get(9));
    public static readonly Image MegaphoneLaser = new WrapSpriteLoader(() => Get(10));
    public static readonly Image PortableMegaphone = new WrapSpriteLoader(() => Get(11));
    public static readonly Image WallEar = new WrapSpriteLoader(() => Get(12));
    public static readonly Image ParrotTrainer = new WrapSpriteLoader(() => Get(13));
    public static Sprite InterviewerSprite => Get(6);
    public static Sprite VoiceJammingerSprite => Get(7);
    public static Sprite DyingVoiceSprite => Get(9);
    public static Sprite BibiriSprite => Get(0);
    public static Sprite TalkativeSprite => Get(1);
    public static Sprite LimitSpeechSprite => Get(2);
    public static Sprite SuperHeroSprite => Get(4);
    public static Sprite EvilHeroSprite => Get(5);
    private static readonly string[] Names = { "Bibiri", "Talkative", "LimitSpeech", "NGWord", "SuperHero", "EvilHero",
        "Interviewer", "VoiceJamminger", "VoiceStone", "DyingVoiceMessage",
        "MegaphoneLaser", "PortableMegaphone", "WallEar", "ParrotTrainer" };
    private static readonly Sprite?[] Cache = new Sprite?[Names.Length];
    private static readonly float[] CanvasWidths = {
        256f / 175f, 256f / 175f, 256f / 175f, 256f / 175f, 256f / 175f, 256f / 175f,
        256f / 200f, 128f / 85f, 96f / 75f, 256f / 200f,
        256f / 175f, 256f / 175f, 1f, 1f
    };

    private static Sprite Get(int index)
    {
        if (Cache[index] != null) return Cache[index]!;
        string resource = "RoleIcons/" + Names[index] + ".png";
        using var stream = Virial.NebulaAPI.GetAddon("tanuki.bcl.roles")?.GetResource(resource)?.AsStream()
            ?? throw new FileNotFoundException("Missing role icon: " + resource);
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
        { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        GraphicsHelper.LoadImage(texture, bytes.ToArray(), false);
        // Preserve each previous icon canvas so HUD buttons and map markers retain their size.
        Cache[index] = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
            new Vector2(.5f, .5f), texture.width / CanvasWidths[index]);
        return Cache[index]!;
    }
}

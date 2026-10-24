using AmongUs.Data;
using UnityEngine;

namespace TanukiBCL.Roles;

internal static class HeroTransformSound
{
    private static readonly AudioClip?[] clips = new AudioClip?[2];

    public static void Play(GameObject parent, Vector2 position, HeroKind kind)
    {
        if (!Constants.ShouldPlaySfx() || PlayerControl.LocalPlayer == null) return;
        float distance = Vector2.Distance(PlayerControl.LocalPlayer.GetTruePosition(), position);
        float volume = Mathf.Clamp01(1f - Mathf.Max(0f, distance - 2f) / 8f) * DataManager.Settings.Audio.SfxVolume;
        if (volume <= 0f) return;
        var clip = clips[(int)kind];
        if (clip == null)
        {
            string name = kind == HeroKind.Super ? "super-hero-transform" : "evil-hero-transform";
            using var stream = Virial.NebulaAPI.GetAddon("tanuki.bcl.roles")?.GetResource("Audio/" + name + ".pcm")?.AsStream();
            if (stream == null) return;
            // The packaged sound uses the same 44.1 kHz stereo PCM format as the charge sound.
            var samples = MegaphoneChargeSound.Read(stream);
            clip = AudioClip.Create("TanukiHeroTransformation-" + kind, samples.Length / 2, 2, 44100, false);
            clip.SetData(samples, 0);
            clips[(int)kind] = clip;
        }
        var source = parent.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = volume;
        source.clip = clip;
        source.Play();
    }
}

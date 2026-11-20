using AmongUs.Data;
using UnityEngine;

namespace TanukiBCL.Roles;

internal static class VoiceStoneImpactSound
{
    private static AudioClip? clip;
    public static void Play(GameObject parent, Vector2 position)
    {
        if (!Constants.ShouldPlaySfx() || PlayerControl.LocalPlayer == null) return;
        float distance = Vector2.Distance(PlayerControl.LocalPlayer.GetTruePosition(), position);
        float volume = Mathf.Clamp01(1f - Mathf.Max(0f, distance - 1f) / 7f) * DataManager.Settings.Audio.SfxVolume;
        if (volume <= 0f) return;
        if (clip == null)
        {
            var samples = VoiceStoneImpactSynth.Create();
            clip = AudioClip.Create("TanukiVoiceStoneImpact", samples.Length, 1, VoiceStoneImpactSynth.Rate, false);
            clip.SetData(samples, 0);
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

using AmongUs.Data;
using UnityEngine;

namespace TanukiBCL.Roles;

internal static class SonarmanSound
{
    private static AudioClip? clip;
    public static AudioSource Create(GameObject parent)
    {
        if (clip == null)
        {
            var samples = SonarmanPingSynth.Create();
            clip = AudioClip.Create("TanukiSonarmanPing",samples.Length,1,SonarmanPingSynth.Rate,false);
            clip.SetData(samples,0);
        }
        var source = parent.AddComponent<AudioSource>();
        source.playOnAwake = false; source.loop = false; source.spatialBlend = 0f;
        source.clip = clip;
        return source;
    }
    public static void Ping(AudioSource source)
    {
        if (source == null || !Constants.ShouldPlaySfx()) return;
        source.volume = Mathf.Clamp01(DataManager.Settings.Audio.SfxVolume)*.45f;
        if (source.volume > 0) source.Play();
    }
}

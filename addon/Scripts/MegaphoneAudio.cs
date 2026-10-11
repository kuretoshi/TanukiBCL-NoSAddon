using System;
using AmongUs.Data;
using UnityEngine;

namespace TanukiBCL.Roles;

internal sealed class MegaphoneAudio : IDisposable
{
    private static readonly AudioClip?[] clips = new AudioClip?[5];
    private readonly GameObject root;
    private readonly AudioSource loop;
    private readonly AudioSource transient;
    private readonly float gain;
    private readonly float x, y;
    private bool firing;

    private static AudioClip Clip(int kind)
    {
        if (clips[kind] != null) return clips[kind]!;
        var data = MegaphoneSynth.Create(kind);
        var clip = AudioClip.Create("TanukiOriginalMegaphone" + kind, data.Length, 1, MegaphoneSynth.Rate, false);
        clip.SetData(data, 0);
        clips[kind] = clip;
        return clip;
    }

    public MegaphoneAudio(float x, float y, float volume)
    {
        this.x = x; this.y = y; gain = volume;
        root = new GameObject("TanukiMegaphoneAudio");
        loop = root.AddComponent<AudioSource>();
        transient = root.AddComponent<AudioSource>();
        loop.playOnAwake = transient.playOnAwake = false;
        loop.loop = true;
        loop.clip = Clip(1);
        Tick(0);
        transient.PlayOneShot(Clip(0));
        loop.Play();
    }

    public void Tick(float energy)
    {
        float distance = PlayerControl.LocalPlayer == null ? 100f : Vector2.Distance(PlayerControl.LocalPlayer.GetTruePosition(), new Vector2(x, y));
        float volume = Constants.ShouldPlaySfx() ? gain * Mathf.Clamp01(1f - Mathf.Max(0f, distance - 2f) / 18f) * DataManager.Settings.Audio.SfxVolume : 0f;
        transient.volume = volume;
        loop.volume = volume * (firing ? .3f : .24f + .16f * energy);
        loop.pitch = firing ? .85f + .25f * energy : .7f + .65f * energy;
    }

    public void Fire(float energy)
    {
        if (firing) return;
        firing = true;
        loop.Stop();
        transient.Stop();
        Tick(energy);
        transient.PlayOneShot(Clip(2));
    }

    public void Dispose()
    {
        loop.Stop(); transient.Stop();
        UnityEngine.Object.Destroy(root);
    }
}

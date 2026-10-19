using System;
using AmongUs.Data;
using UnityEngine;

namespace TanukiBCL.Roles;

internal sealed class MegaphoneAudio : IDisposable
{
    private static readonly AudioClip?[] clips = new AudioClip?[5];
    private readonly GameObject root;
    private readonly AudioSource charge;
    private readonly AudioSource transient;
    private readonly float gain;
    private readonly float x, y;
    private bool firing;

    private static AudioClip ChargeClip()
    {
        if (clips[1] != null) return clips[1]!;
        using var stream = Virial.NebulaAPI.GetAddon("tanuki.bcl.roles")?.GetResource("Audio/megaphone-charge.pcm")?.AsStream()
            ?? throw new System.IO.FileNotFoundException("Missing megaphone charge sound.");
        var samples = MegaphoneChargeSound.Read(stream);
        var clip = AudioClip.Create("TanukiMegaphoneCharge", samples.Length / MegaphoneChargeSound.Channels,
            MegaphoneChargeSound.Channels, MegaphoneChargeSound.Rate, false);
        clip.SetData(samples, 0);
        clips[1] = clip;
        return clip;
    }

    private static AudioClip Clip(int kind)
    {
        if (clips[kind] != null) return clips[kind]!;
        var data = MegaphoneSynth.Create(kind);
        var clip = AudioClip.Create("TanukiOriginalMegaphone" + kind, data.Length, 1, MegaphoneSynth.Rate, false);
        clip.SetData(data, 0);
        clips[kind] = clip;
        return clip;
    }

    public MegaphoneAudio(float x, float y, float volume, float chargeSeconds)
    {
        this.x = x; this.y = y; gain = volume;
        root = new GameObject("TanukiMegaphoneAudio");
        charge = root.AddComponent<AudioSource>();
        transient = root.AddComponent<AudioSource>();
        charge.playOnAwake = transient.playOnAwake = false;
        charge.loop = false;
        charge.clip = ChargeClip();
        charge.pitch = MegaphoneChargeSound.PlaybackPitch(charge.clip.length, chargeSeconds);
        Tick(0);
        charge.Play();
    }

    public void Tick(float energy)
    {
        float distance = PlayerControl.LocalPlayer == null ? 100f : Vector2.Distance(PlayerControl.LocalPlayer.GetTruePosition(), new Vector2(x, y));
        float volume = Constants.ShouldPlaySfx() ? gain * Mathf.Clamp01(1f - Mathf.Max(0f, distance - 2f) / 18f) * DataManager.Settings.Audio.SfxVolume : 0f;
        transient.volume = volume;
        charge.volume = volume;
    }

    public void Fire(float energy)
    {
        if (firing) return;
        firing = true;
        charge.Stop();
        transient.Stop();
        Tick(energy);
        transient.PlayOneShot(Clip(2));
    }

    public void Dispose()
    {
        charge.Stop(); transient.Stop();
        UnityEngine.Object.Destroy(root);
    }
}

using System;
using System.IO;

namespace TanukiBCL.Roles;

public static class MegaphoneChargeSound
{
    public const int Rate = 44100;
    public const int Channels = 2;
    public static float[] Read(Stream stream)
    {
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        byte[] raw = bytes.ToArray();
        if (raw.Length == 0 || raw.Length % (Channels * 2) != 0 || raw.Length > Rate * Channels * 2 * 30)
            throw new InvalidDataException("Invalid megaphone charge PCM data.");
        var samples = new float[raw.Length / 2];
        for (int i = 0; i < samples.Length; i++)
            samples[i] = unchecked((short)(raw[i * 2] | (raw[i * 2 + 1] << 8))) / 32768f;
        return samples;
    }
    public static float PlaybackPitch(float duration, float chargeSeconds) => Math.Clamp(duration / Math.Max(.5f, chargeSeconds), .1f, 3f);
}

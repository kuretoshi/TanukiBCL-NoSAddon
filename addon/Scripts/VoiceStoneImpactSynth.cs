using System;

namespace TanukiBCL.Roles;

// Original, short stone impact. Generated once, not once per frame or stone.
public static class VoiceStoneImpactSynth
{
    public const int Rate = 22050;
    public static float[] Create()
    {
        var samples = new float[(int)(Rate * .32)];
        uint random = 19193;
        double low = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            double t = (double)i / Rate;
            random = unchecked(random * 1664525 + 1013904223);
            double noise = random / (double)uint.MaxValue * 2 - 1;
            low += .14 * (noise - low);
            double thud = Math.Sin(2 * Math.PI * (90 * t + 1.2 * (1 - Math.Exp(-t * 60)))) * Math.Exp(-t * 22);
            double grit = (.8 * low + .12 * noise) * Math.Exp(-t * 45);
            double tap = t > .065 ? .25 * Math.Sin(2 * Math.PI * 160 * (t - .065)) * Math.Exp(-(t - .065) * 50) : 0;
            double fade = Math.Min(1, t / .0015) * Math.Min(1, (.32 - t) / .025);
            samples[i] = (float)(.8 * Math.Tanh(1.6 * (thud + grit + tap)) * fade);
        }
        samples[0] = samples[samples.Length - 1] = 0;
        return samples;
    }
}

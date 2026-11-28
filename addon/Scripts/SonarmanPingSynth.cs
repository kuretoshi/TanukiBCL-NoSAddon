using System;

namespace TanukiBCL.Roles;

// Short, decaying sonar ping with two quiet echoes. No external sound files,
// models, recordings or network playback are involved.
public static class SonarmanPingSynth
{
    public const int Rate = 24000;
    public const double Seconds = .48;
    public static float[] Create()
    {
        var samples = new float[(int)(Rate*Seconds)];
        for (int i = 0; i < samples.Length; i++)
        {
            double t = (double)i/Rate;
            double wave = Tone(t) + .22*Tone(t-.105) + .08*Tone(t-.21);
            double fade = Math.Clamp((Seconds-t)/.02,0,1);
            samples[i] = (float)(wave*fade*.46);
        }
        samples[^1] = 0;
        return samples;
    }
    private static double Tone(double t)
    {
        if (t < 0) return 0;
        double phase = 2*Math.PI*(1100*t-95*t*t);
        double envelope = Math.Min(1,t/.006)*Math.Exp(-10*t);
        return envelope*(Math.Sin(phase)+.12*Math.Sin(phase*2.01));
    }
}

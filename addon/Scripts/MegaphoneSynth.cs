using System;

namespace TanukiBCL.Roles;

// Original procedural sounds; shared by the game and the WAV preview exporter.
public static class MegaphoneSynth
{
    public const int Rate = 44100;
    private static double Tone(double frequency, double t) => Math.Sin(2 * Math.PI * frequency * t);
    public static float[] Create(int kind)
    {
        double duration = kind == 0 ? 1.1 : kind == 1 || kind == 4 ? 1 : kind == 2 ? 5 : .3;
        var samples = new float[(int)(Rate * duration)];
        uint noise = 73191;
        double lowNoise = 0, previousNoise = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            double t = (double)i / Rate, u = t / duration;
            noise = unchecked(noise * 1664525 + 1013904223);
            double n = noise / (double)uint.MaxValue * 2 - 1;
            lowNoise += .035 * (n - lowNoise);
            double hiss = n - previousNoise;
            previousNoise = n;
            double s;
            if (kind == 1 || kind == 4)
            {
                // Integer frequencies keep both loops seamless. Harmonic drive
                // gives the amplifier a rough industrial sound, rather than a beep.
                double hum = Tone(55, t) + .45 * Tone(110, t) + .25 * Tone(165, t);
                double motor = Tone(220, t) * (.6 + .4 * Tone(8, t)) + .22 * Tone(440, t);
                s = kind == 1 ? (.3 * Math.Tanh(1.5 * (Tone(150, t) + .45 * Tone(300, t)))
                    + .75 * lowNoise + .045 * n + .025 * Tone(710, t)) * (.9 + .1 * Tone(7, t))
                    : .43 * Math.Tanh(2.0 * hum) + .12 * Tone(330, t) * (1 + .3 * Tone(12, t));
                if (kind == 1) s *= Math.Min(1, t / .008) * Math.Min(1, (duration - t) / .008);
            }
            else if (kind == 2)
            {
                double blast = Math.Sin(2 * Math.PI * (43 * t + 117 * .035 * (1 - Math.Exp(-t / .035))));
                s = 1.15 * blast * Math.Exp(-t * 8) + 2.8 * lowNoise * Math.Exp(-t * 9)
                    + .48 * hiss * Math.Exp(-t * 30)
                    + .27 * Tone(940, t) * Math.Exp(-t * 12)
                    + .18 * Math.Sin(2 * Math.PI * (1600 * t - 620 * t * t)) * Math.Exp(-t * 6);
                // Metallic chamber reflections after the initial cannon impact.
                for (int echo = 1; echo <= 4; echo++)
                {
                    double delay = t - .105 * echo;
                    if (delay > 0) s += .18 / echo * (Tone(173, delay) + .5 * Tone(347, delay)) * Math.Exp(-delay * 4.5);
                }
                // Reference the sustained-beam structure of Nova, using newly
                // synthesized pressure noise and driven bass throughout the shot.
                double sustain = .5 * Math.Tanh(2 * (Tone(82, t) + .35 * Tone(164, t)))
                    + 1.8 * lowNoise + .16 * n + .045 * Tone(650, t) * (1 + .4 * Tone(13, t));
                s += sustain * Math.Min(1, t / .06) * (.92 + .08 * Tone(9, t));
                s = .88 * Math.Tanh(s * 1.6) * Math.Min(1, t / .002) * Math.Min(1, (duration - t) / .07);
            }
            else if (kind == 0)
            {
                double lockClick = .65 * hiss * Math.Exp(-t * 7) + .3 * Tone(155, t) * Math.Exp(-t * 16);
                double turbine = Math.Sin(2 * Math.PI * (70 * t + 180 * t * t)) + .12 * Tone(730, t);
                s = .65 * Math.Tanh(lockClick + .22 * turbine * Math.Sin(Math.PI * u))
                    * Math.Min(1, t / .003) * Math.Min(1, (duration - t) / .035);
            }
            else
            {
                double phase = 800 * t - 950 * t * t;
                s = (.35 * Math.Sin(2 * Math.PI * phase) + .08 * Math.Sin(4 * Math.PI * phase))
                    * Math.Sin(Math.PI * u) * Math.Sin(Math.PI * u);
            }
            samples[i] = (float)Math.Max(-.9, Math.Min(.9, s));
        }
        return samples;
    }
}

using System;

namespace TanukiBCL.Roles;

public sealed class MegaphoneCharge
{
    public double Energy { get; private set; }
    private double lastTime = double.NaN;
    private double previousLevel;

    public void Reset() { Energy = 0; lastTime = double.NaN; previousLevel = 0; }

    public void Sample(double rms, bool active, double now, double quietDb, double loudDb, double fullChargeSeconds, double maxSampleInterval = .15)
    {
        double level = active && double.IsFinite(rms) && rms > 0 && rms <= 1 && loudDb > quietDb
            ? Math.Clamp((20 * Math.Log10(rms) - quietDb) / (loudDb - quietDb), 0, 1) : 0;
        double delta = now - lastTime;
        // A missing input/connection never earns energy. Silence keeps existing energy.
        if (double.IsFinite(delta) && delta > 0 && delta <= Math.Clamp(maxSampleInterval, .15, .35) && fullChargeSeconds > 0)
            Energy = Math.Clamp(Energy + Math.Min(previousLevel, level) * delta / fullChargeSeconds, 0, 1);
        lastTime = now;
        previousLevel = level;
    }

    public double Width(double minimum, double maximum) => minimum + Energy * (Math.Max(minimum, maximum) - minimum);

    public static double Aim(double radians, int mode)
    {
        if (mode == 1) return Math.Cos(radians) >= 0 ? 0 : Math.PI;
        if (mode == 2) return Math.Round(radians / (Math.PI / 2)) * (Math.PI / 2);
        return radians;
    }

    public static bool Hits(double dx, double dy, double radians, double fullWidth)
    {
        double forward = dx * Math.Cos(radians) + dy * Math.Sin(radians);
        double side = -dx * Math.Sin(radians) + dy * Math.Cos(radians);
        // Deliberately no wall test or length cap, matching Nova's forward-beam logic.
        return forward > 0 && Math.Abs(side) <= fullWidth / 2;
    }
}

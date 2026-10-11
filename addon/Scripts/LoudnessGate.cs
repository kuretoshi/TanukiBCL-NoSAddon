using System;

namespace TanukiBCL.Roles;

// This class has no NoS dependencies, so the timing rules can be tested separately.
public sealed class LoudnessGate
{
    private long sequence = -1;
    private double lastTime = double.NaN;
    private double heldSeconds;
    private bool wasLoud;

    public void Reset()
    {
        lastTime = double.NaN;
        heldSeconds = 0;
        wasLoud = false;
        // Keep the sequence watermark when pausing during meetings.
    }

    public bool Sample(long sampleSequence, double rms, bool active, double now,
        double thresholdDb, double durationSeconds)
    {
        if (sampleSequence <= sequence) return false;
        sequence = sampleSequence;
        if (!double.IsFinite(rms) || rms < 0 || rms > 1 || !active)
        {
            Reset();
            return false;
        }
        double delta = now - lastTime;
        bool loud = rms > 0 && 20 * Math.Log10(rms) >= thresholdDb;
        if (!loud || !wasLoud || !double.IsFinite(delta) || delta < 0 || delta > 0.15)
            heldSeconds = 0;
        else
            heldSeconds += delta;
        lastTime = now;
        wasLoud = loud;
        return loud && heldSeconds >= durationSeconds;
    }
}

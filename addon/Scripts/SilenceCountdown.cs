using System;

namespace TanukiBCL.Roles;

// Count only consecutive, fresh BCL heartbeat intervals. Muting is silence;
// loss of the connection freezes the countdown instead of killing the player.
public sealed class SilenceCountdown
{
    public double SilentSeconds { get; private set; }
    private double lastSample = double.NaN;
    // UI liveness tolerates short scheduling stalls. This does not authorize
    // counting a missing interval as silence; Sample still requires <=150ms.
    public bool Connected(double now) => double.IsFinite(lastSample) && now - lastSample >= 0 && now - lastSample <= 1.0;
    public void Pause() => lastSample = double.NaN;
    public void Reset() { SilentSeconds = 0; Pause(); }
    public double Remaining(double duration) => Math.Max(0, duration - SilentSeconds);

    public bool Sample(double rms, bool active, double now, double thresholdDb, double duration)
    {
        if (!double.IsFinite(rms) || rms < 0 || rms > 1 || !double.IsFinite(now) || duration <= 0) return false;
        double delta = now - lastSample;
        bool speaking = active && rms > 0 && 20 * Math.Log10(rms) >= thresholdDb;
        if (speaking) SilentSeconds = 0;
        else if (double.IsFinite(delta) && delta > 0 && delta <= .15) SilentSeconds += delta;
        lastSample = now;
        return SilentSeconds + 1e-9 >= duration;
    }
}

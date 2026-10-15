using System;
namespace TanukiBCL.Roles;

public sealed class SpeechBudget
{
    public double UsedSeconds { get; private set; }
    public double AddedSeconds { get; private set; }
    public bool Add(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds <= 0 || !double.IsFinite(AddedSeconds + seconds)) return false;
        AddedSeconds += seconds;
        Pause();
        return true;
    }
    private double previous = double.NaN;
    private bool wasSpeaking;
    public void Pause() { previous = double.NaN; wasSpeaking = false; }
    public bool Connected(double now) => double.IsFinite(previous) && now >= previous && now - previous <= 1;
    public double Remaining(double total) => Math.Max(0, total + AddedSeconds - UsedSeconds);
    public bool Sample(double rms, bool active, double now, double thresholdDb, double total, double maxGap = .15)
    {
        if (!double.IsFinite(rms) || rms < 0 || rms > 1 || !double.IsFinite(now) || total <= 0) return false;
        bool speaking = active && rms > 0 && 20 * Math.Log10(rms) >= thresholdDb;
        double elapsed = now - previous;
        if (speaking && wasSpeaking && elapsed > 0 && elapsed <= maxGap) UsedSeconds = Math.Min(total + AddedSeconds, UsedSeconds + elapsed);
        previous = now; wasSpeaking = speaking;
        return Remaining(total) <= 1e-9;
    }
}

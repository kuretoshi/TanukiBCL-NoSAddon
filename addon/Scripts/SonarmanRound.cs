using System;
using System.Collections.Generic;

namespace TanukiBCL.Roles;

public readonly record struct SonarmanSignal(byte Source, double X, double Y, double Time);
public readonly record struct SonarmanMark(double X, double Y);

// No audio routing lives here: pulses contain only an observed speech position.
public sealed class SonarmanRound
{
    private readonly Dictionary<byte, SonarmanSignal> pending = new();
    private readonly List<SonarmanMark> marks = new();
    private int sequence;
    private double nextSweep, cooldown;
    public bool Running { get; private set; }
    public double Started { get; private set; }
    public double Ends { get; private set; }
    public double ReadyAt { get; private set; }
    public double Period { get; private set; }
    public IReadOnlyList<SonarmanMark> Marks => marks;
    public bool Active(double now) => Running && now < Ends;
    public double Remaining(double now) => Running ? Math.Max(0,Ends-now) : 0;
    public double Phase(double now) => !Running || Period <= 0 ? 0 : (Math.Max(0,now-Started) % Period) / Period;
    public bool Activate(int id, double now, double duration, double recharge, double period)
    {
        if (id <= sequence || Running || now < ReadyAt || !double.IsFinite(now) ||
            !double.IsFinite(duration) || duration < 1 || duration > 120 ||
            !double.IsFinite(recharge) || recharge < 0 || recharge > 180 ||
            !double.IsFinite(period) || period < .5 || period > 10) return false;
        sequence = id; Started = now; Ends = now + duration; cooldown = recharge;
        Period = period; nextSweep = now + period; Running = true;
        pending.Clear(); marks.Clear();
        return true;
    }
    public void Observe(SonarmanSignal signal, byte self, double x, double y, double range, double now)
    {
        if (!Active(now) || signal.Source == self || signal.Time < Started || signal.Time > now ||
            !double.IsFinite(signal.X) || !double.IsFinite(signal.Y) || !double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(range) || range <= 0) return;
        double dx = signal.X-x, dy = signal.Y-y;
        if (dx*dx+dy*dy > range*range || (pending.TryGetValue(signal.Source,out var old) && old.Time > signal.Time)) return;
        pending[signal.Source] = signal;
    }
    public bool Sweep(double now, double x, double y, double range)
    {
        if (!Active(now) || now < nextSweep || !double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(range) || range <= 0) return false;
        marks.Clear();
        foreach (var signal in pending.Values)
        {
            if (signal.Time < now-Period) continue;
            double dx = signal.X-x, dy = signal.Y-y, distance = Math.Sqrt(dx*dx+dy*dy);
            if (distance < .01 || distance > range) continue;
            marks.Add(new(dx/distance,dy/distance));
        }
        pending.Clear();
        nextSweep = Started + (Math.Floor((now-Started)/Period)+1)*Period;
        return true;
    }
    public bool Stop(double now)
    {
        if (!Running) return false;
        ReadyAt = Math.Min(now,Ends) + cooldown;
        Running = false; pending.Clear(); marks.Clear();
        return true;
    }
    public void Clear() { Running = false; ReadyAt = 0; pending.Clear(); marks.Clear(); }
}

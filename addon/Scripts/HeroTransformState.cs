using System;

namespace TanukiBCL.Roles;

public enum HeroKind { Super, Evil }

// Monotonic state and rules shared by both roles, without game dependencies.
public sealed class HeroTransformState
{
    public bool Waiting { get; private set; }
    public bool Active { get; private set; }
    public double Deadline { get; private set; }
    public double Duration { get; private set; }
    private int sequence;

    public bool Arm(double now, double seconds)
    {
        if (Waiting || Active || !ValidTime(now, seconds)) return false;
        Waiting = true; Duration = seconds; Deadline = now + seconds;
        return true;
    }
    public bool Transform(int message, double now, double seconds)
    {
        if (message <= sequence || Active || !ValidTime(now, seconds)) return false;
        sequence = message;
        Waiting = false; Active = true; Duration = seconds; Deadline = now + seconds;
        return true;
    }
    public bool Update(double now, bool eligible)
    {
        if ((!Waiting && !Active) || (eligible && double.IsFinite(now) && now < Deadline)) return false;
        Clear();
        return true;
    }
    public double Remaining(double now) => Waiting || Active ? Math.Max(0, Deadline - now) : 0;
    public bool CanHearTrigger(double now) => Waiting && double.IsFinite(now) && now < Deadline;
    public bool IsSpeechTimeout(double now, bool eligible) => Waiting && eligible && double.IsFinite(now) && now >= Deadline;
    public void Clear() { Waiting = false; Active = false; Duration = 0; Deadline = 0; }
    private static bool ValidTime(double now, double seconds) => double.IsFinite(now) && now >= 0 &&
        double.IsFinite(seconds) && seconds >= 1 && seconds <= 120;
    public static bool BlocksKill(HeroKind defender, bool active, bool selfKill, HeroKind? killer) =>
        active && !selfKill && (!killer.HasValue || killer.Value == defender);
    public static bool Misfires(HeroKind kind, bool targetIsCrewmate) => kind == HeroKind.Super && targetIsCrewmate;
}

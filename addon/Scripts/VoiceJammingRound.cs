using System;

namespace TanukiBCL.Roles;

// Meeting lifetimes and replay rejection do not depend on Unity or wall clocks.
public sealed class VoiceJammingRound
{
    public byte? PendingTarget { get; private set; }
    public byte? ActiveTarget { get; private set; }
    public bool InMeeting { get; private set; }
    public int Used { get; private set; }
    public int Seed { get; private set; }
    public long Priority { get; private set; }
    private int lastMark, lastPitch;
    private double pitchUntil;

    public bool Mark(int sequence, byte target)
    {
        if (InMeeting || sequence <= lastMark) return false;
        lastMark = sequence;
        PendingTarget = target;
        return true;
    }
    public void BeginMeeting()
    {
        if (InMeeting) return;
        InMeeting = true;
        ActiveTarget = PendingTarget;
        PendingTarget = null;
        Seed = 0;
    }
    public int Remaining(int limit) => limit == 0 ? -1 : Math.Max(0, limit - Used);
    public bool PitchActive(double now) => InMeeting && Seed != 0 && now < pitchUntil;
    public double PitchRemaining(double now) => PitchActive(now) ? Math.Max(0, pitchUntil - now) : 0;
    public bool CanPitch(int limit) => InMeeting && Remaining(limit) != 0;
    public bool Pitch(int sequence, int seed, long priority, int limit, double now, double seconds)
    {
        if (sequence <= lastPitch || seed <= 0 || priority <= 0 || !CanPitch(limit) ||
            !double.IsFinite(now) || !double.IsFinite(seconds) || seconds < 1 || seconds > 120) return false;
        lastPitch = sequence;
        Seed = seed;
        Priority = priority;
        pitchUntil = now + seconds;
        Used++;
        return true;
    }
    public void EndMeeting() { InMeeting = false; ActiveTarget = null; Seed = 0; pitchUntil = 0; }
    public static double PitchFor(int seed, byte speaker)
    {
        uint mixed = unchecked((uint)seed ^ ((uint)speaker + 1) * 2654435761u);
        mixed ^= mixed << 13; mixed ^= mixed >> 17; mixed ^= mixed << 5;
        int[] semitones = { -7, -5, -3, 3, 5, 7 };
        return Math.Pow(2, semitones[mixed % 6] / 12d);
    }
}

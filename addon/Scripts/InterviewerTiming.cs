using System;

namespace TanukiBCL.Roles;

public static class InterviewerTiming
{
    // Keep absolute Unix milliseconds as integers. Mixing a long timestamp with
    // float seconds loses more precision than an entire recording window.
    public static bool IsRecording(long started, long stopped, float seconds, long now) =>
        started > 0 && stopped == 0 && now < started + (long)(seconds * 1000d);

    public static long CapturedEnd(long localStarted, long sourceStarted, long sourceStopped, float seconds)
    {
        if (sourceStopped <= sourceStarted) return localStarted;
        long elapsed = Math.Min(sourceStopped - sourceStarted, (long)(seconds * 1000d));
        return localStarted + elapsed;
    }
}

namespace TanukiBCL.Roles;

public sealed class InterviewerPlaybackLimit
{
    private int used;
    private long lastRequest;

    // Zero means unlimited. Keep the last request across meetings so a delayed
    // duplicate cannot consume a use from the next meeting's allowance.
    public int Remaining(int limit) => limit <= 0 ? -1 : System.Math.Max(0, limit - used);
    public bool TryUse(int limit, long request)
    {
        if (request <= lastRequest || Remaining(limit) == 0) return false;
        lastRequest = request;
        used++;
        return true;
    }
    public void ResetMeeting() => used = 0;
}

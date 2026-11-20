using System;
using System.Collections.Generic;

namespace TanukiBCL.Roles;

// Per-ability state: placement and listening requests have independent replay guards.
public sealed class WallEarRound
{
    public sealed record Ear(int Id, float X, float Y);
    private readonly List<Ear> ears = new();
    private int lastPlacement, lastRequest;
    private double nextPlacement;
    public IReadOnlyList<Ear> Ears => ears;
    public Ear? Active { get; private set; }
    public const float UseDistance = 1.2f;
    public bool CanPlace(double now, int maximum) => double.IsFinite(now) && now >= nextPlacement &&
        maximum is >= 1 and <= 10 && ears.Count < maximum && Active == null;
    public bool Place(int sequence, float x, float y, double now, int maximum, float cooldown)
    {
        if (sequence <= lastPlacement || !CanPlace(now, maximum) || !Coordinate(x) || !Coordinate(y) ||
            !float.IsFinite(cooldown) || cooldown < 0 || cooldown > 120) return false;
        lastPlacement = sequence;
        ears.Add(new Ear(sequence, x, y));
        nextPlacement = now + cooldown;
        return true;
    }
    public bool Listen(int sequence, int earId, float x, float y)
    {
        if (sequence <= lastRequest) return false;
        if (earId == -1) { lastRequest = sequence; Stop(); return true; }
        var ear = ears.Find(e => e.Id == earId);
        if (ear == null || !Nearby(ear, x, y)) return false;
        lastRequest = sequence; Active = ear;
        return true;
    }
    public static bool Nearby(Ear ear, float x, float y) => Coordinate(x) && Coordinate(y) &&
        (ear.X - x) * (ear.X - x) + (ear.Y - y) * (ear.Y - y) <= UseDistance * UseDistance;
    public Ear? Nearest(float x, float y, Func<Ear, bool> reachable)
    {
        Ear? nearest = null;
        float best = float.PositiveInfinity;
        foreach (var ear in ears)
        {
            float distance = (ear.X - x) * (ear.X - x) + (ear.Y - y) * (ear.Y - y);
            if (!Nearby(ear, x, y) || distance >= best || !reachable(ear)) continue;
            nearest = ear; best = distance;
        }
        return nearest;
    }
    private static bool Coordinate(float value) => float.IsFinite(value) && Math.Abs(value) <= 10000;
    public void Stop() => Active = null;
    public void Clear() { Stop(); ears.Clear(); }
}

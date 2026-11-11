using System;

namespace TanukiBCL.Roles;

// The audio bank has one ten-second clip; the fake player is a separate lifetime.
public sealed class CloneRound
{
    public const int Seconds = 10;
    private int lastRecord, lastSpawn;
    public int RecordId { get; private set; }
    public long Started { get; private set; }
    public bool Ready { get; private set; }
    public int SpawnId { get; private set; }
    public int FakeId { get; private set; } = -1;
    public long PlayStarted { get; private set; }
    public double NextSummon { get; private set; }
    public bool Active => FakeId >= 256;
    public bool Recording => RecordId > 0 && !Ready;
    public double Remaining(long now) => Recording ? Math.Max(0, Seconds - (now - Started) / 1000d) : 0;
    public double CooldownRemaining(double time) => Math.Max(0, NextSummon - time);
    public bool CanSummon(double time) => Ready && !Active && time >= NextSummon;
    public bool Begin(int id, long now, double time, float cooldown)
    {
        if (id <= lastRecord || now <= 0 || Recording) return false;
        Despawn(time, cooldown);
        lastRecord = RecordId = id; Started = now; Ready = false;
        return true;
    }
    public void Complete(long now)
    {
        if (Recording && now - Started >= Seconds * 1000L) Ready = true;
    }
    public bool Summon(int id, int fakeId, long now, double time)
    {
        Complete(now);
        if (id <= lastSpawn || fakeId < 256 || now <= 0 || !CanSummon(time)) return false;
        lastSpawn = SpawnId = id; FakeId = fakeId; PlayStarted = now;
        return true;
    }
    public bool Despawn(double time, float cooldown, int expected = 0)
    {
        if (!Active || expected != 0 && expected != SpawnId) return false;
        FakeId = -1; PlayStarted = 0;
        NextSummon = time + Math.Max(0, cooldown);
        return true;
    }
    public void Pause(long now, double time, float cooldown)
    {
        Complete(now); Despawn(time, cooldown);
        if (!Recording) return;
        RecordId = 0; Started = 0;
    }
    public void Clear()
    {
        RecordId = 0; Started = 0; Ready = false; FakeId = -1; PlayStarted = 0;
    }
}

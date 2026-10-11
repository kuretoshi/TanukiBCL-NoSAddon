using System;
using System.Diagnostics;

namespace TanukiBCL.Roles;

// One local transport, independent consumers and death gates. Modifiers may
// coexist with one another and with the megaphone without stealing UDP samples.
public sealed class SharedVoiceBridge : IDisposable
{
    private sealed class Feed
    {
        public readonly VoiceBridge Bridge;
        public readonly Stopwatch Clock = Stopwatch.StartNew();
        public readonly byte Player;
        public int Readers;
        public long Sequence = -1;
        public double Time = double.NegativeInfinity;
        public double Rms;
        public bool Active;
        public Feed(byte player, string? directory, int discoveryPort) { Player = player; Bridge = new VoiceBridge(player, "tanukiVoiceRoles", directory, discoveryPort); }
    }
    private static Feed? current;
    private readonly Feed feed;
    private readonly LoudnessGate gate = new();
    private long consumed = -1;
    private bool disposed;
    public string DescriptorPath => feed.Bridge.DescriptorPath;
    public double LatestRms => feed.Bridge.LatestRms;
    public bool LatestActive => feed.Bridge.LatestActive;
    public bool LatestFromHostRelay => feed.Bridge.LatestFromHostRelay;
    public bool Connected => feed.Bridge.Connected;
    public SharedVoiceBridge(byte player, string? directory = null, int discoveryPort = 57741)
    {
        if (current == null || current.Player != player) current = new Feed(player, directory, discoveryPort);
        feed = current; feed.Readers++;
    }
    public bool TryReadSample(out long seq, out double rms, out bool active)
    {
        if (disposed) throw new ObjectDisposedException(nameof(SharedVoiceBridge));
        if (feed.Bridge.TryReadSample(out long freshSeq, out double freshRms, out bool freshActive))
        {
            feed.Sequence = freshSeq; feed.Rms = freshRms; feed.Active = freshActive;
            feed.Time = feed.Clock.Elapsed.TotalSeconds;
        }
        seq = feed.Sequence; rms = feed.Rms; active = feed.Active;
        if (seq <= consumed || feed.Clock.Elapsed.TotalSeconds - feed.Time > .15) return false;
        consumed = seq; return true;
    }
    public bool Poll(bool eligible, double threshold, double duration)
    {
        if (!eligible) gate.Reset();
        return TryReadSample(out long seq, out double rms, out bool active)
            && eligible && gate.Sample(seq, rms, active, feed.Clock.Elapsed.TotalSeconds, threshold, duration);
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (--feed.Readers == 0)
        {
            feed.Bridge.Dispose();
            if (current == feed) current = null;
        }
    }
}

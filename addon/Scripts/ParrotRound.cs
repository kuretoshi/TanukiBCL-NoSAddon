using System;
using System.Collections.Generic;
using System.Linq;

namespace TanukiBCL.Roles;

public sealed class ParrotRound
{
    public sealed class Bird
    {
        public int Id { get; }
        public float X { get; }
        public float Y { get; }
        public float Seconds { get; }
        public int RecordingMilliseconds => (int)(Seconds * 1000);
        public List<int> Clips { get; } = new();
        public int RecordId { get; private set; }
        public byte SourceId { get; private set; }
        public long Started { get; private set; }
        public int PlayId { get; private set; }
        public int PlayClip { get; private set; }
        public long PlayStarted { get; private set; }
        private readonly HashSet<byte> inside = new();
        private int lastRecord, lastPlay;
        public Bird(int id, float x, float y, float seconds = 1.5f) { Id = id; X = x; Y = y; Seconds = seconds; }
        public bool Playing(long now) => PlayStarted > 0 && now < PlayStarted + RecordingMilliseconds;
        public bool Recording(long now) => RecordId > 0 && Started > 0 && now < Started + RecordingMilliseconds;
        public bool Begin(int sequence, byte source, long now)
        {
            if (sequence <= lastRecord || now <= 0 || Recording(now)) return false;
            Complete(now);
            lastRecord = RecordId = sequence; SourceId = source; Started = now;
            return true;
        }
        public void Complete(long now)
        {
            if (PlayStarted > 0 && now >= PlayStarted + RecordingMilliseconds)
            { PlayId = PlayClip = 0; PlayStarted = 0; }
            if (RecordId == 0 || Recording(now) || Clips.Contains(RecordId)) return;
            Clips.Add(RecordId);
            if (Clips.Count > 5) Clips.RemoveAt(0);
        }
        public bool Play(int sequence, int clip, long now)
        {
            if (sequence <= lastPlay || !Clips.Contains(clip) || now <= 0 ||
                PlayStarted > 0 && now < PlayStarted + RecordingMilliseconds) return false;
            lastPlay = PlayId = sequence; PlayClip = clip; PlayStarted = now;
            return true;
        }
        public bool WasInside(byte player) => inside.Contains(player);
        public bool Visit(IEnumerable<byte> players)
        {
            var current = new HashSet<byte>(players);
            bool entered = current.Any(id => !inside.Contains(id));
            inside.Clear(); inside.UnionWith(current);
            return entered;
        }
        public void Pause(long now)
        {
            if (Recording(now)) { RecordId = 0; Started = 0; }
            else Complete(now);
            PlayId = PlayClip = 0; PlayStarted = 0; inside.Clear();
        }
    }
    private readonly List<Bird> birds = new();
    private int lastPlacement;
    private double nextPlacement;
    public IReadOnlyList<Bird> Birds => birds;
    public bool CanPlace(double now, int maximum) => double.IsFinite(now) && now >= nextPlacement && maximum is >= 1 and <= 5 && birds.Count < maximum;
    public bool Place(int sequence, float x, float y, double now, int maximum, float cooldown, float seconds = 1.5f)
    {
        if (sequence <= lastPlacement || !CanPlace(now, maximum) || !float.IsFinite(x) || !float.IsFinite(y) ||
            Math.Abs(x) > 10000 || Math.Abs(y) > 10000 || !float.IsFinite(cooldown) || cooldown < 0 || cooldown > 120 ||
            !float.IsFinite(seconds) || seconds < .5f || seconds > 5f) return false;
        lastPlacement = sequence; nextPlacement = now + cooldown;
        birds.Add(new Bird(sequence, x, y, seconds)); return true;
    }
    public Bird? Find(int id) => birds.Find(b => b.Id == id);
    public void Clear() => birds.Clear();
}

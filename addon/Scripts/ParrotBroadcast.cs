using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace TanukiBCL.Roles;

public static class ParrotBroadcast
{
    private static readonly Dictionary<byte, object[]> entries = new();
    private static readonly Dictionary<byte, object> clones = new();
    private static long lastWrite;
    public static void Set(byte owner, IReadOnlyList<ParrotRound.Bird> birds, float range)
    {
        entries[owner] = birds.Select(b => (object)new { ownerId = owner, birdId = b.Id, x = b.X, y = b.Y, range, seconds = b.Seconds,
            recordId = b.RecordId, sourceId = b.SourceId, started = b.Started, clips = b.Clips.ToArray(),
            playId = b.PlayId, playClip = b.PlayClip, playStarted = b.PlayStarted }).ToArray();
        Publish(false);
    }
    public static void Remove(byte owner) { if (entries.Remove(owner)) Publish(true); }
    public static void SetClone(byte owner, object state) { clones[owner] = state; Publish(false); }
    public static void RemoveClone(byte owner) { if (clones.Remove(owner)) Publish(true); }
    private static void Publish(bool immediate)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (!immediate && now - lastWrite < 100) return;
        lastWrite = now;
        AddonLinkState.Update("parrots", JsonSerializer.Serialize(new { players = entries.Values.SelectMany(v => v).Concat(clones.Values).ToArray() }));
    }
}

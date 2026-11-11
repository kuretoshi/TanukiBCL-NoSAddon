using TanukiBCL.Roles;

internal static class CloneTests
{
    internal static void Run(Action<bool, string> check)
    {
        var round = new CloneRound();
        check(!round.CanSummon(0), "clone requires completed recording");
        check(round.Begin(1, 100000, 0, 30), "begin ten-second recording");
        check(!round.Begin(2, 100001, 0, 30), "cannot overlap recordings");
        round.Complete(109999); check(!round.Ready && round.Remaining(109999) > 0, "not ready before ten seconds");
        round.Complete(110000); check(round.Ready && round.Remaining(110000) == 0, "ready at exactly ten seconds");
        check(round.Summon(1, 257, 110000, 10) && !round.CanSummon(100), "one clone at a time");
        check(!round.Despawn(20, 30, 99) && round.Active, "stale despawn cannot remove current clone");
        check(round.Despawn(20, 30, 1) && round.NextSummon == 50, "cooldown starts at disappearance");
        check(!round.CanSummon(49.999) && round.CanSummon(50), "cooldown exact boundary");
        check(!round.Summon(1, 513, 150000, 50), "replayed spawn rejected");
        check(round.Summon(2, 513, 150000, 50), "completed recording can be reused");
        round.Pause(151000, 51, 30);
        check(!round.Active && round.Ready && round.NextSummon == 81, "meeting removes clone but preserves completed clip");
        check(round.Begin(2, 152000, 52, 30) && !round.Ready, "rerecord overwrites old clip");
        round.Pause(153000, 53, 30);
        check(round.RecordId == 0 && !round.Ready && !round.Recording, "meeting discards unfinished clip");
        check(!round.Begin(1, 154000, 54, 30) && !round.Begin(2, 154000, 54, 30), "old record RPCs stay rejected after cancellation");
        check(round.Begin(3, 154000, 54, 30), "new recording after cancellation");
        round.Complete(164000); check(round.Summon(3, 769, 190000, 90), "summon after new recording");
        check(round.Begin(4, 191000, 91, 30) && !round.Active && round.NextSummon == 121, "rerecord removes active clone and starts cooldown");
        round.Clear(); check(!round.Active && !round.Ready && round.RecordId == 0, "death and role removal clear audio and clone");
        CheckBroadcast(check);
        Console.WriteLine("PASS clone: recording, overwrite, one clone, cooldown, meeting, stale RPCs and broadcast isolation");
    }
    private static void CheckBroadcast(Action<bool, string> check)
    {
        var birds = new ParrotRound(); birds.Place(1, 0, 0, 0, 1, 0);
        ParrotBroadcast.SetClone(1, new { mode = "clone", ownerId = 1, birdId = 1000001 });
        ParrotBroadcast.Set(2, birds.Birds, 4);
        ParrotBroadcast.Set(3, birds.Birds, 4); ParrotBroadcast.Remove(3);
        check(AddonLinkState.Snapshot(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())["parrots"].GetArrayLength() == 2, "clone and parrot share transport without replacing each other");
        ParrotBroadcast.Remove(2);
        var entries = AddonLinkState.Snapshot(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())["parrots"];
        check(entries.GetArrayLength() == 1 && entries[0].GetProperty("mode").GetString() == "clone", "removing parrots preserves clone");
        ParrotBroadcast.RemoveClone(1);
        check(AddonLinkState.Snapshot(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())["parrots"].GetArrayLength() == 0, "clone removal publishes immediate empty state");
    }
}

using TanukiBCL.Roles;

internal static class ParrotTests
{
    internal static void Run(Action<bool, string> check)
    {
        var round = new ParrotRound();
        check(round.Place(1, 0, 0, 0, 2, 15), "parrot placement");
        check(!round.Place(2, 0, 0, 14, 2, 15), "placement cooldown");
        check(round.Place(2, 0, 0, 15, 2, 15) && !round.Place(3, 0, 0, 30, 2, 15), "placement limit");
        var bird = round.Find(1)!;
        check(!bird.Play(1, 1, 1000), "new bird has no recordings");
        check(bird.Visit(new byte[] { 1 }) && !bird.Visit(new byte[] { 1 }), "only entry triggers");
        bird.Visit(Array.Empty<byte>());
        check(bird.Visit(new byte[] { 1 }), "reentry triggers");
        for (int i = 1; i <= 7; i++)
        {
            long at = 1000 + i * 2000;
            check(bird.Begin(i, 2, at) && !bird.Begin(i + 100, 3, at + 1), "one recording at a time");
            bird.Complete(at + 1499);
            check(!bird.Clips.Contains(i), "incomplete clip is not playable");
            bird.Complete(at + 1500);
        }
        check(bird.Clips.SequenceEqual(new[] { 3, 4, 5, 6, 7 }), "newest five FIFO");
        check(bird.Play(20, 3, 18000) && !bird.Play(21, 4, 18001), "play selection and overlap guard");
        check(bird.Begin(8, 1, 20000), "next capture starts");
        bird.Pause(20500);
        check(bird.RecordId == 0 && bird.PlayId == 0 && bird.Clips.Count == 5, "meeting cancels partial, preserves completed");
        check(!bird.Begin(7, 1, 22000), "old capture rejected after meeting");
        round.Clear(); check(round.Birds.Count == 0, "role removal cleanup");
        CheckSettings(check);
        Console.WriteLine("PASS parrot: placement, FIFO, synchronized selection, visits, meeting and cleanup");
    }
    private static void CheckSettings(Action<bool, string> check)
    {
        foreach (float seconds in new[] { .5f, 3f, 5f })
        {
            var round = new ParrotRound();
            check(round.Place(1, 0, 0, 0, 1, 0, seconds), "configured duration accepted");
            var bird = round.Find(1)!;
            int duration = (int)(seconds * 1000);
            check(bird.Seconds == seconds && bird.Begin(1, 2, 1000), "duration stored per bird");
            bird.Complete(1000 + duration - 1);
            check(bird.Recording(1000 + duration - 1) && bird.Clips.Count == 0, "recording uses configured end");
            bird.Complete(1000 + duration);
            check(bird.Clips.Count == 1 && !bird.Recording(1000 + duration), "clip completes at configured end");
            check(bird.Play(2, 1, 10000) && bird.Playing(10000 + duration - 1), "playback uses configured duration");
            bird.Complete(10000 + duration);
            check(!bird.Playing(10000 + duration) && bird.PlayId == 0, "playback expires at configured end");
        }
        var limited = new ParrotRound();
        check(limited.Place(1, 0, 0, 0, 1, 0) && !limited.Place(2, 0, 0, 0, 1, 0), "one-bird setting enforced");
        var maximum = new ParrotRound();
        for (int i = 1; i <= 5; i++) check(maximum.Place(i, 0, 0, 0, 5, 0), "five-bird setting permits placement");
        check(!maximum.Place(6, 0, 0, 0, 5, 0), "five-bird setting enforces cap");
        var invalid = new ParrotRound();
        foreach (float seconds in new[] { .49f, 5.01f, float.NaN, float.PositiveInfinity })
            check(!invalid.Place(1, 0, 0, 0, 2, 0, seconds), "invalid duration rejected");
    }
}

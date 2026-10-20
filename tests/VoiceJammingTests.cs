using System.Text.Json;
using TanukiBCL.Roles;

internal static class VoiceJammingTests
{
    public static void Run(Action<bool, string> check)
    {
        var round = new VoiceJammingRound();
        check(!round.Pitch(1, 123, 1000, 2, 0, 10), "Cannot scramble outside a meeting");
        check(round.Mark(1, 5) && round.Mark(2, 7), "New mark replaces the pending target");
        round.BeginMeeting();
        check(round.ActiveTarget == 7 && round.PendingTarget == null, "Only the next meeting consumes the mark");
        check(!round.Mark(3, 8), "Cannot mark during a meeting");
        check(round.Pitch(1, 123, 1000, 2, 0, 10) && round.PitchActive(9.999) && round.Remaining(2) == 1, "Pitch lasts for the configured duration");
        check(!round.PitchActive(10) && round.PitchRemaining(10) == 0, "Pitch expires at the exact duration boundary");
        check(!round.Pitch(1, 456, 1001, 2, 1, 10) && round.Seed == 123, "Duplicate pitch RPC does not reroll or spend uses");
        check(round.Pitch(2, 456, 1002, 2, 5, 10) && round.Seed == 456, "Remaining use rerolls the same meeting");
        check(round.PitchActive(14.999) && !round.PitchActive(15), "Reroll restarts the configured duration");
        round.EndMeeting();
        check(!round.PitchActive(9.999) && round.ActiveTarget == null, "Meeting end clears all active effects");
        round.BeginMeeting();
        check(round.ActiveTarget == null && round.Remaining(2) == 0 && !round.Pitch(3, 789, 1003, 2, 20, 10), "Match use limit never resets and mark never carries over");
        check(round.Pitch(3, 789, 1003, 0, 20, 10) && round.Remaining(0) == -1, "Unlimited setting accepts more uses");
        var pitches = Enumerable.Range(0, 24).Select(id => VoiceJammingRound.PitchFor(789, (byte)id)).ToArray();
        check(pitches.All(p => p is >= .5 and <= 2 && Math.Abs(p - 1) > .1) && pitches.Distinct().Count() > 1,
            "Pitch is bounded, non-neutral and randomized per speaker");
        check(pitches[7] == VoiceJammingRound.PitchFor(789, 7), "Every receiver derives the same pitch");
        CheckFile(check);
        Console.WriteLine("PASS Voice Jamminger meeting lifecycle, global limit, replay rejection and per-process effects");
    }
    private static void CheckFile(Action<bool, string> check)
    {
        var directory = Path.Combine(Path.GetTempPath(), "tanuki-jamming-" + Guid.NewGuid().ToString("N"));
        var old = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        Environment.SetEnvironmentVariable("LOCALAPPDATA", directory);
        try
        {
            VoiceJammingBroadcast.Set(9, 2, 0, 456, 1000, new byte[] { 1, 2, 9 });
            var path = Path.Combine(directory, "TanukiBCL", "NoS", "voice-jamming-" + Environment.ProcessId + ".json");
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            check(root.GetProperty("pid").GetInt32() == Environment.ProcessId && root.GetProperty("protocol").GetInt32() == 1,
                "Effects are bound to the game process");
            var players = root.GetProperty("players").EnumerateArray().ToArray();
            check(players.Count(p => p.GetProperty("muted").GetBoolean()) == 1 &&
                players.Single(p => p.GetProperty("playerId").GetInt32() == 2).GetProperty("muted").GetBoolean(), "Only the marked voice is muted");
            check(players.All(p => p.GetProperty("pitch").GetDouble() != 1), "Pitch affects the caster and everyone else");
            VoiceJammingBroadcast.Remove(9);
            check(!File.Exists(path), "Removing the ability removes its IPC file immediately");
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOCALAPPDATA", old);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}

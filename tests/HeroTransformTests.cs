using TanukiBCL.Roles;

public static class HeroTransformTests
{
    public static void Run(Action<bool, string> check)
    {
        VerifyBroadcast(check);
        var hero = new HeroTransformState();
        check(!hero.Active && !hero.Waiting, "untransformed hero starts without powers");
        check(!hero.CanHearTrigger(0), "speech before pressing the button cannot transform");
        check(!hero.IsSpeechTimeout(0, true), "idle is not a failed transformation");
        check(hero.Arm(1, 5), "button arms voice trigger");
        check(!hero.Active && hero.Waiting, "arming alone grants no invulnerability or kill");
        check(!hero.Arm(2, 5), "cannot stack arming attempts");
        check(hero.CanHearTrigger(5.999) && !hero.CanHearTrigger(6), "voice trigger has an exact deadline");
        check(!hero.IsSpeechTimeout(5.999, true) && hero.IsSpeechTimeout(6, true), "failed speech uses retry cooldown exactly at deadline");
        check(!hero.IsSpeechTimeout(6, false) && !hero.IsSpeechTimeout(double.NaN, true), "meeting/death interruption is not speech failure");
        check(hero.Transform(1, 2, 10), "voice-confirmed RPC starts transformation");
        check(hero.Active && !hero.Waiting && hero.Remaining(2) == 10, "ten seconds start from transformation, not arming");
        check(!hero.IsSpeechTimeout(12, true), "successful transformation expiry keeps the configured cooldown");
        check(!hero.Transform(2, 3, 120), "cannot extend an active transformation by another RPC");
        check(!hero.Update(11.999, true) && hero.Active, "powers last until the configured deadline");
        check(hero.Update(12, true) && !hero.Active, "powers expire at the exact deadline");
        check(!hero.Transform(1, 13, 10), "expired sequence cannot be replayed");
        check(hero.Transform(2, 13, 3), "next transformation uses its configured duration");
        check(hero.Update(14, false) && !hero.Active, "meeting, death or game end immediately removes powers");
        check(hero.Arm(15, 2) && hero.Update(17, true), "silent arming times out without transforming");
        check(!hero.Active && !hero.Waiting, "timeout grants no powers");
        check(!hero.Arm(double.NaN, 5) && !hero.Transform(3, 20, 121), "invalid times and duration are rejected");
        foreach (HeroKind kind in Enum.GetValues<HeroKind>())
        {
            var opposite = kind == HeroKind.Super ? HeroKind.Evil : HeroKind.Super;
            check(HeroTransformState.BlocksKill(kind, true, false, null), kind + " blocks ordinary killers when transformed");
            check(!HeroTransformState.BlocksKill(kind, false, false, null), kind + " has no guard before transformation");
            check(!HeroTransformState.BlocksKill(kind, true, false, opposite), kind + " can be killed by the opposite hero");
            check(HeroTransformState.BlocksKill(kind, true, false, kind), kind + " does not lose its guard against the same hero role");
            check(!HeroTransformState.BlocksKill(kind, true, true, null), kind + " cannot block its own suicide");
        }
        check(HeroTransformState.Misfires(HeroKind.Super, true), "Super Hero dies from targeting a crewmate");
        check(!HeroTransformState.Misfires(HeroKind.Super, false), "Super Hero can kill impostors and neutrals");
        check(!HeroTransformState.Misfires(HeroKind.Evil, true), "Evil Hero can kill crewmates without misfire");
        Console.WriteLine("PASS hero voice arming, exact duration, interruption, replay rejection, mutual exceptions and misfire rules");
    }
    private static void VerifyBroadcast(Action<bool, string> check)
    {
        var directory = Environment.GetEnvironmentVariable("LOCALAPPDATA") ??
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string path = Path.Combine(directory, "TanukiBCL", "NoS", "hero-transforms-" + Environment.ProcessId + ".json");
        try
        {
            HeroTransformBroadcast.Set(2, HeroKind.Super);
            HeroTransformBroadcast.Set(3, HeroKind.Evil);
            using var both = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            var frame = both.RootElement;
            check(frame.GetProperty("pid").GetInt32() == Environment.ProcessId &&
                frame.GetProperty("players").GetArrayLength() == 2, "both heroes are published for this game process");
            check(frame.GetProperty("players")[0].GetProperty("kind").GetString() == "super" &&
                frame.GetProperty("players")[1].GetProperty("kind").GetString() == "evil", "published hero kinds are explicit");
            HeroTransformBroadcast.Remove(2);
            using var remaining = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            check(remaining.RootElement.GetProperty("players").GetArrayLength() == 1 &&
                remaining.RootElement.GetProperty("players")[0].GetProperty("playerId").GetInt32() == 3,
                "ending one transformation preserves the other hero's exemption");
            HeroTransformBroadcast.Remove(3);
            using var empty = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            check(empty.RootElement.GetProperty("players").GetArrayLength() == 0,
                "ending the last transformation clears all costume exemptions");
        }
        finally { HeroTransformBroadcast.Remove(2); HeroTransformBroadcast.Remove(3); File.Delete(path); File.Delete(path + ".tmp"); }
    }
}

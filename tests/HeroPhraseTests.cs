using TanukiBCL.Roles;

static class HeroPhraseTests
{
    public static void Run(Action<bool, string> check)
    {
        check(HeroTransformPhrase.TryCreate(" 蒸着 ", "じょうちゃく", out var words) && words.SequenceEqual(new[] { "蒸着", "じょうちゃく" }), "Custom word and reading validated together");
        check(HeroTransformPhrase.TryCreate("闇落ち", "", out words) && words.Length == 1, "Reading is optional");
        foreach (var bad in new[] { "", "あ", new string('あ', 33), "<b>変身</b>", "変身|返信", "変\n身", "！！", "あ\ud800" })
            check(!HeroTransformPhrase.TryCreate(bad, "", out _), "Reject invalid custom word: " + bad);
        check(!HeroTransformPhrase.TryCreate("蒸着", "あ", out _), "Invalid reading rejects whole update");
        check(HeroTransformPhrase.TryCreate(new string('あ', 32), "", out _), "Maximum length word accepted");
        HeroTransformPhrase.TryCreate("蒸着", "じょうちゃく", out var firstPlayer);
        HeroTransformPhrase.TryCreate("闇落ち", "やみおち", out var secondPlayer);
        check(firstPlayer[0] == "蒸着" && secondPlayer[0] == "闇落ち", "Different players' phrase arrays stay independent");
        check(HeroTransformPhrase.TryCreate("蒸着", "", out words) && words.Length == 1, "Reading can be cleared");
        Console.WriteLine("PASS personal hero phrase validation and independent values");
    }
}

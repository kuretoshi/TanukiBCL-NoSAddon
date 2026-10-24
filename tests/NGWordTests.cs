using TanukiBCL.Roles;

static class NGWordTests
{
    public static void Run(Action<bool, string> check)
    {
        var entries = NGWordList.Parse("\uFEFF# test\r\nインポスター|いんぽすたー\r\n\r\nタスク\nタスク\n");
        check(entries.Length == 2 && entries[0].Words.Length == 2, "NG list reads UTF8 BOM/comments/readings and deduplicates");
        var copy = NGWordEntry.Decode(entries[0].Encode(123));
        check(copy?.Display == "インポスター" && copy.Words[1] == "いんぽすたー", "NG assignment carries host word and reading to clients");
        foreach (var bad in new[] { "# empty", "a", "<size=900>危険</size>", "タスク|", new string('あ', 33) })
        {
            bool rejected = false;
            try { NGWordList.Parse(bad); } catch (ArgumentException) { rejected = true; }
            check(rejected, "NG malformed word file rejected: " + bad);
        }
        check(NGWordEntry.Decode(new[] { 1, 60, 65, 62 }) == null, "NG invalid network arguments rejected without throwing");
    }
}

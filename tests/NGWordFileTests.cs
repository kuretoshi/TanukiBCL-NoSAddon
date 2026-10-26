using System.Text;
using TanukiBCL.Roles;

static class NGWordFileTests
{
    public static void Run(Action<bool, string> check)
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "TanukiNGWordFiles-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        try { Verify(root, check); }
        finally
        {
            if (!root.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(root).StartsWith("TanukiNGWordFiles-", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected test cleanup path.");
            Directory.Delete(root, true);
        }
    }
    private static void Verify(string root, Action<bool, string> check)
    {
        const string template = "# template\nインポスター|いんぽすたー\nタスク|たすく\n";
        var files = new NGWordFiles(Path.Combine(root, "settings"), () => template);
        files.EnsureDefault();
        check(files.Load().Length == 2, "NG default template remains compatible");
        string selected = Path.Combine(root, "選択したリスト.txt");
        const string custom = "# preserve comments and alternate readings\n会議|かいぎ|カイギ\n";
        File.WriteAllText(selected, custom, new UTF8Encoding(true));
        check(files.Select(selected) == 1, "NG browse imports UTF8 BOM and validates before selecting");
        var restarted = new NGWordFiles(Path.Combine(root, "settings"), () => template);
        check(restarted.SourcePath == selected && restarted.Load()[0].Words.Length == 3, "NG selected file persists across restart with all readings");
        string exported = Path.Combine(root, "保存した設定.txt");
        restarted.SaveCurrent(exported);
        check(File.ReadAllText(exported) == custom && restarted.SourcePath == selected, "NG current export preserves list/comments and does not change selected file");
        string templatePath = Path.Combine(root, "テンプレート.txt");
        restarted.SaveTemplate(templatePath);
        check(File.ReadAllText(templatePath) == template && File.ReadAllText(selected) == custom, "NG template export is independent of current list");
        string invalid = Path.Combine(root, "invalid.txt");
        File.WriteAllText(invalid, "# no words");
        bool rejected = false;
        try { restarted.Select(invalid); } catch (ArgumentException) { rejected = true; }
        check(rejected && restarted.SourcePath == selected, "NG invalid import retains previous selection");
        File.WriteAllText(selected, "# now invalid");
        rejected = false;
        try { restarted.SaveCurrent(exported); } catch (ArgumentException) { rejected = true; }
        check(rejected && File.ReadAllText(exported) == custom, "NG invalid current file does not overwrite export destination");
        restarted.EnsureDefault();
        check(File.ReadAllText(files.DefaultFile) == template, "NG repeated template initialization never overwrites existing default");
        Console.WriteLine("PASS NG browse/import persistence, current/template export and invalid-file preservation");
    }
}

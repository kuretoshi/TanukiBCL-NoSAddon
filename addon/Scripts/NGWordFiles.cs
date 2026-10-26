using System;
using System.IO;
using System.Text;

namespace TanukiBCL.Roles;

// File operations are independent of Unity so validation and persistence can be tested.
public sealed class NGWordFiles
{
    private readonly string directory;
    private readonly Func<string> template;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private string SelectionFile => Path.Combine(directory, "NGWordSource.txt");
    public string DefaultFile => Path.Combine(directory, "NGWords.txt");
    public NGWordFiles(string directory, Func<string> template) { this.directory = directory; this.template = template; }
    public string SourcePath => File.Exists(SelectionFile) ? ReadSmall(SelectionFile).Trim() : DefaultFile;
    public string EnsureDefault()
    {
        Directory.CreateDirectory(directory);
        if (!File.Exists(DefaultFile)) Write(DefaultFile, template(), false);
        return DefaultFile;
    }
    public string CurrentText()
    {
        EnsureDefault();
        string text = ReadSmall(SourcePath);
        NGWordList.Parse(text);
        return text;
    }
    public NGWordEntry[] Load() => NGWordList.Parse(CurrentText());
    public int Select(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string text = ReadSmall(fullPath);
        int count = NGWordList.Parse(text).Length;
        Directory.CreateDirectory(directory);
        string temporary = SelectionFile + ".tmp";
        File.WriteAllText(temporary, fullPath, Utf8);
        File.Move(temporary, SelectionFile, true);
        return count;
    }
    public void SaveCurrent(string path) => Write(path, CurrentText(), true);
    public void SaveTemplate(string path) => Write(path, template(), true);
    private static string ReadSmall(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > 131072) throw new IOException("NG word file exceeds 128 KiB.");
        using var reader = new StreamReader(stream, Utf8, true);
        return reader.ReadToEnd();
    }
    private static void Write(string path, string text, bool overwrite)
    {
        NGWordList.Parse(text);
        // Validation happens before opening the destination, including when overwriting.
        using var stream = new FileStream(path, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write);
        using var writer = new StreamWriter(stream, Utf8);
        writer.Write(text);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace TanukiBCL.Roles;

public sealed class NGWordEntry
{
    public readonly string Display;
    public readonly string[] Words;
    public NGWordEntry(string display, string[] words) { Display = display; Words = words; }
    public int[] Encode(int request) => new[] { request }.Concat(string.Join("|", Words).Select(c => (int)c)).ToArray();
    public static NGWordEntry? Decode(int[] args)
    {
        if (args.Length < 3 || args.Length > 128 || args[0] < 1 || args.Skip(1).Any(c => c < 32 || c > 65535)) return null;
        try { return NGWordList.Parse(new string(args.Skip(1).Select(c => (char)c).ToArray())).FirstOrDefault(); }
        catch (ArgumentException) { return null; }
    }
}
public static class NGWordList
{
    public static NGWordEntry[] Parse(string text)
    {
        if (text.Length > 65536) throw new ArgumentException("NG word list exceeds 64 KiB of text.");
        var entries = new List<NGWordEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim().TrimStart('\uFEFF');
            if (line.Length == 0 || line.StartsWith("#")) continue;
            if (line.Length > 126) throw new ArgumentException("NG word entry is too long.");
            var words = line.Split('|').Select(s => s.Trim()).ToArray();
            if (words.Length > 3 || words.Any(w => w.Length < 2 || w.Length > 32 || w.Any(c => char.IsControl(c) || c == '<' || c == '>')))
                throw new ArgumentException("Use 2–32 characters per word and up to two alternate readings.");
            if (seen.Add(words[0])) entries.Add(new(words[0], words));
            if (entries.Count > 256) throw new ArgumentException("NG word list exceeds 256 entries.");
        }
        if (entries.Count == 0) throw new ArgumentException("NG word list is empty.");
        return entries.ToArray();
    }
}

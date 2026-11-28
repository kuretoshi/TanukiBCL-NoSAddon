using System;
using System.Collections.Generic;
using System.Linq;

namespace TanukiBCL.Roles;

// Curated, ordinary words with explicit accepted spellings. No microphone
// transcript is stored and no substring/fuzzy matching is added to NG words.
public static class VoiceAuthenticationWords
{
    public static readonly string[][] Entries = {
        new[] { "バナナ", "ばなな" }, new[] { "リンゴ", "りんご", "林檎" },
        new[] { "ロケット", "ろけっと" }, new[] { "トマト", "とまと" },
        new[] { "ペンギン", "ぺんぎん" }, new[] { "ライオン", "らいおん" },
        new[] { "カメラ", "かめら" }, new[] { "ピアノ", "ぴあの" },
        new[] { "ラジオ", "らじお" }, new[] { "マイク", "まいく" },
        new[] { "テレビ", "てれび" }, new[] { "コーヒー", "こーひー", "珈琲" }
    };
    public static int[] Select(int count, Random random)
    {
        if (count < 1 || count > 5) throw new ArgumentOutOfRangeException(nameof(count));
        return Enumerable.Range(0, Entries.Length).OrderBy(_ => random.Next()).Take(count).ToArray();
    }
    public static bool Valid(int entry) => entry >= 0 && entry < Entries.Length;
    public static NGWordEntry[] EntriesToWords() => Entries.Select(w => new NGWordEntry(w[0], (string[])w.Clone())).ToArray();
}

// Host-only per-game snapshot. Only the selected words (at most five) travel
// over RPC; participants never need to read the host's external file.
public sealed class VoiceAuthenticationWordAssignments
{
    private readonly NGWordEntry[] catalog;
    private readonly Dictionary<(byte player, uint task), NGWordEntry[]> assigned = new();
    public VoiceAuthenticationWordAssignments(NGWordEntry[] catalog)
    {
        this.catalog = NGWordList.Parse(string.Join("\n", catalog.Select(w => string.Join("|", w.Words))));
    }
    public NGWordEntry[] GetOrCreate(byte player, uint task, int count, Random random)
    {
        if (count < 1 || count > 5) throw new ArgumentOutOfRangeException(nameof(count));
        if (!assigned.TryGetValue((player, task), out var words))
            assigned[(player, task)] = words = catalog.OrderBy(_ => random.Next()).Take(Math.Min(count, catalog.Length)).ToArray();
        return words;
    }
    public static string Encode(NGWordEntry[] words) => string.Join("\n", words.Select(w => string.Join("|", w.Words)));
    public static NGWordEntry[] Decode(string text)
    {
        if (text.Length > 635) throw new ArgumentException("Task word assignment is too large.");
        var words = NGWordList.Parse(text);
        if (words.Length > 5) throw new ArgumentException("Too many task words.");
        return words;
    }
}

// A task keeps this object when its window closes. Requests are deliberately
// separate from progress so a delayed recognition cannot complete a new step.
public sealed class VoiceAuthenticationProgress
{
    private readonly int[] entries;
    public IReadOnlyList<NGWordEntry> Words { get; }
    private int lastRequest;
    public IReadOnlyList<int> Entries => entries;
    public int Completed { get; private set; }
    public int Request { get; private set; }
    public bool Listening => Request > 0;
    public bool Finished => Completed == entries.Length;
    public int CurrentEntry => Finished ? -1 : entries[Completed];
    public VoiceAuthenticationProgress(IEnumerable<int> words, int completed = 0)
    {
        entries = words.ToArray();
        if (entries.Length < 1 || entries.Length > 5 || entries.Distinct().Count() != entries.Length ||
            entries.Any(entry => !VoiceAuthenticationWords.Valid(entry)) || completed < 0 || completed > entries.Length)
            throw new ArgumentException("Invalid voice authentication task.");
        Words = entries.Select(i => new NGWordEntry(VoiceAuthenticationWords.Entries[i][0], (string[])VoiceAuthenticationWords.Entries[i].Clone())).ToArray();
        Completed = completed;
    }
    public VoiceAuthenticationProgress(IEnumerable<NGWordEntry> words, int completed = 0)
    {
        var list = words.ToArray();
        if (list.Length < 1 || list.Length > 5 || list.Select(w => w.Display).Distinct(StringComparer.OrdinalIgnoreCase).Count() != list.Length ||
            completed < 0 || completed > list.Length) throw new ArgumentException("Invalid voice authentication task.");
        Words = VoiceAuthenticationWordAssignments.Decode(VoiceAuthenticationWordAssignments.Encode(list));
        entries = Enumerable.Range(0,list.Length).ToArray();
        Completed = completed;
    }
    public bool Begin(int request)
    {
        if (Finished || Listening || request <= lastRequest) return false;
        lastRequest = Request = request;
        return true;
    }
    public bool Accept(int request)
    {
        if (!Listening || Request != request || Finished) return false;
        Completed++;
        Pause();
        return true;
    }
    public void Pause() => Request = 0;
}

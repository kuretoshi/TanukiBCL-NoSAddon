using System;
using System.Collections.Generic;
using System.Linq;

namespace TanukiBCL.Roles;

public sealed class NGWordSubmissionRound
{
    private readonly Dictionary<byte, NGWordEntry> words = new();
    private readonly HashSet<byte> players;
    public NGWordSubmissionRound(IEnumerable<byte> roster) => players = new(roster);
    public int Count => players.Count;
    public int Confirmed => words.Count;
    public bool HasSubmitted(byte player) => words.ContainsKey(player);
    public bool Submit(byte player, string value)
    {
        if (!players.Contains(player) || words.ContainsKey(player) || !TryParse(value, out var entry)) return false;
        words.Add(player, entry!);
        return true;
    }
    public static bool TryParse(string value, out NGWordEntry? entry)
    {
        entry = null;
        if (value.Length > 98 || value.Any(c => char.IsControl(c) || char.IsSurrogate(c))) return false;
        try
        {
            var parsed = NGWordList.Parse(value);
            if (parsed.Length != 1) return false;
            entry = parsed[0];
            return true;
        }
        catch (ArgumentException) { return false; }
    }
    public void Retain(IEnumerable<byte> connected)
    {
        players.IntersectWith(connected);
        foreach (var id in words.Keys.Where(id => !players.Contains(id)).ToArray()) words.Remove(id);
    }
    public Dictionary<byte, NGWordEntry>? Allocate(Random random, int count = 0)
    {
        if (Count < 2 || Confirmed != Count) return null;
        var recipients = players.OrderBy(id => id).ToArray();
        var authors = recipients.ToArray();
        // Rejection sampling a uniform permutation gives a uniform derangement.
        do
        {
            for (int i = authors.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (authors[i], authors[j]) = (authors[j], authors[i]);
            }
        } while (authors.Where((id, i) => id == recipients[i]).Any());
        var allocation = recipients.Select((id, i) => (id, entry: words[authors[i]]));
        // Choose recipients from the full derangement: even one recipient can
        // receive another participant's word, without reusing any submission.
        int target = count <= 0 ? Count : Math.Min(count, Count);
        return allocation.OrderBy(_ => random.Next()).Take(target).ToDictionary(p => p.id, p => p.entry);
    }
}

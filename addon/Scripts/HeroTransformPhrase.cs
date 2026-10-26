using System;
using System.Linq;

namespace TanukiBCL.Roles;

public static class HeroTransformPhrase
{
    public const int MaxLength = 32;
    public static bool TryCreate(string word, string reading, out string[] words)
    {
        words = Array.Empty<string>();
        word = word.Trim(); reading = reading.Trim();
        if (!Valid(word) || (reading.Length > 0 && !Valid(reading))) return false;
        words = reading.Length == 0 || reading == word ? new[] { word } : new[] { word, reading };
        return true;
    }
    private static bool Valid(string value) => value.Length >= 2 && value.Length <= MaxLength &&
        !value.Any(c => char.IsControl(c) || char.IsSurrogate(c) || c == '<' || c == '>' || c == '|') &&
        value.Count(char.IsLetterOrDigit) >= 2;
}

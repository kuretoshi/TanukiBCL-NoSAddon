using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace TanukiBCL.Roles;

// Shared validation for recognised words and received placement messages.
public static class VoiceStoneRules
{
    public const int MaxCharacters = 12;
    public static bool TryWord(string? input, int maximum, out string word)
    {
        word = "";
        if (input == null || input.Length > 64 || maximum < 1 || maximum > MaxCharacters) return false;
        string normalized;
        try { normalized = input.Normalize(NormalizationForm.FormKC); }
        catch (ArgumentException) { return false; }
        if (normalized.Length == 0 || Count(normalized) > maximum) return false;
        foreach (var rune in normalized.EnumerateRunes())
            if (!Rune.IsLetterOrDigit(rune) && rune.Value != 'ー' && rune.Value != '々') return false;
        word = normalized;
        return true;
    }
    public static int Count(string word) => new StringInfo(word).LengthInTextElements;
    public static float Width(string word, float characterWidth) => Count(word) * characterWidth;
}

public sealed class VoiceStoneRequest
{
    public int Sequence { get; private set; }
    public double Expires { get; private set; }
    public long SpeechExpiresUtc { get; private set; }
    public long RecordStartedUtc { get; private set; }
    public long RecordUntilUtc { get; private set; }
    public double RecordUntil { get; private set; }
    public bool LocalNeural { get; private set; }
    public bool Waiting { get; private set; }
    public bool Arm(int sequence, double now, double seconds, long utcNow = 0, bool localNeural = false)
    {
        if (sequence <= Sequence || Waiting || !double.IsFinite(now) || !double.IsFinite(seconds) || seconds < 1 || seconds > 10 ||
            utcNow < 0 || utcNow > long.MaxValue - 30000) return false;
        Sequence = sequence; RecordUntil = now + seconds; LocalNeural = localNeural;
        Expires = RecordUntil + (localNeural ? 20 : 0); Waiting = true;
        RecordStartedUtc = utcNow;
        RecordUntilUtc = utcNow + (long)(seconds * 1000);
        SpeechExpiresUtc = RecordUntilUtc + (localNeural ? 20000 : 0);
        return true;
    }
    public bool Accept(int sequence, double now, string input, int maximum, out string word, double deliveryGrace = 0)
    {
        word = "";
        if (!Waiting || sequence != Sequence || !double.IsFinite(now) || !double.IsFinite(deliveryGrace) || deliveryGrace < 0 || deliveryGrace > 1 ||
            now >= Expires + deliveryGrace || !VoiceStoneRules.TryWord(input, maximum, out word)) return false;
        Cancel();
        return true;
    }
    public void Cancel() { Waiting = false; SpeechExpiresUtc = RecordUntilUtc = RecordStartedUtc = 0; LocalNeural = false; }
}

using System;

namespace TanukiBCL.Roles;

public sealed class BitCharacterProgress
{
    private readonly double[] durations;
    public double Elapsed { get; private set; }
    public int Stage { get; private set; }
    public static readonly int[] Bits = { 2, 4, 8, 16, 32 };
    // Keep the published wire value for normal voice. Stage 5 (former 64bit)
    // is retired so existing BetterCrewLink clients still clear the effect.
    public const int FinalStage = 6;
    public static bool IsValidStage(int stage) => (stage >= 0 && stage < Bits.Length) || stage == FinalStage;
    public BitCharacterProgress(params double[] durations)
    {
        if (durations.Length != Bits.Length || Array.Exists(durations, d => !double.IsFinite(d) || d <= 0))
            throw new ArgumentException("Five positive finite stage durations are required.");
        this.durations = (double[])durations.Clone();
    }
    public bool Advance(double delta, bool running)
    {
        if (!running || Stage == FinalStage || !double.IsFinite(delta) || delta <= 0) return false;
        int previous = Stage;
        Elapsed += delta;
        double boundary = 0;
        for (int i = 0; i < durations.Length; i++)
        {
            boundary += durations[i];
            if (Elapsed < boundary) break;
            Stage = i + 1 == durations.Length ? FinalStage : i + 1;
        }
        return previous != Stage;
    }
    public double Remaining
    {
        get
        {
            if (Stage == FinalStage) return 0;
            double boundary = 0;
            for (int i = 0; i <= Stage; i++) boundary += durations[i];
            return Math.Max(0, boundary - Elapsed);
        }
    }
}
